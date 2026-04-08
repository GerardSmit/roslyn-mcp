using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynMCP.Services;

/// <summary>
/// Manages isolated loading of diagnostic analyzers (and later source generators) via
/// collectible <see cref="AnalyzerLoadContext"/> instances.
/// <para>
/// Entries are keyed by project identity combined with the sorted set of analyzer DLL
/// paths plus file metadata (size + last-write time) so that an updated NuGet package
/// produces a new key and therefore a fresh load context.
/// </para>
/// <para>
/// Idle entries are automatically evicted after <see cref="IdleTimeout"/>, mirroring
/// the workspace cache eviction lifecycle in <see cref="WorkspaceService"/>.
/// </para>
/// </summary>
internal sealed class AnalyzerHost : IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan EvictionInterval = TimeSpan.FromMinutes(2);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, AnalyzerCacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly Timer _evictionTimer;
    private bool _disposed;

    public AnalyzerHost()
    {
        _evictionTimer = new Timer(EvictExpiredEntries, null, EvictionInterval, EvictionInterval);
    }

    /// <summary>
    /// Returns cached analyzers for the given project and DLL paths, loading them
    /// in an isolated <see cref="AnalyzerLoadContext"/> on first access.
    /// The cache is keyed on <paramref name="projectKey"/> combined with
    /// the DLL set and file metadata.
    /// </summary>
    public ImmutableArray<DiagnosticAnalyzer> GetOrLoadAnalyzers(
        string projectKey, IReadOnlyList<string> analyzerPaths)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(projectKey);

        if (analyzerPaths.Count == 0)
            return ImmutableArray<DiagnosticAnalyzer>.Empty;

        string key = ComputeKey(projectKey, analyzerPaths);

        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                existing.LastAccessedUtc = DateTime.UtcNow;
                return existing.Analyzers;
            }

            var entry = LoadIsolated(analyzerPaths);
            _entries[key] = entry;
            Console.Error.WriteLine(
                $"[AnalyzerHost] Loaded {entry.Analyzers.Length} analyzer(s) from {analyzerPaths.Count} DLL(s) for project '{Path.GetFileName(projectKey)}'.");
            return entry.Analyzers;
        }
    }

    /// <summary>
    /// Evicts all cached entries whose project key matches <paramref name="projectKey"/>.
    /// Called when the associated workspace is evicted.
    /// </summary>
    public void EvictForProject(string projectKey)
    {
        lock (_lock)
        {
            string prefix = projectKey + "||";
            var toRemove = _entries
                .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    _entries.Remove(key);
                    entry.Dispose();
                    Console.Error.WriteLine(
                        $"[AnalyzerHost] Evicted analyzer context for project '{Path.GetFileName(projectKey)}'.");
                }
            }
        }
    }

    /// <summary>
    /// Unloads all cached load contexts and clears the cache.
    /// Subsequent <see cref="GetOrLoadAnalyzers"/> calls will re-create contexts as needed.
    /// </summary>
    public void UnloadAll()
    {
        lock (_lock)
        {
            foreach (var entry in _entries.Values)
                entry.Dispose();
            _entries.Clear();
            Console.Error.WriteLine("[AnalyzerHost] All analyzer contexts unloaded.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _evictionTimer.Dispose();
        UnloadAll();
    }

    private static AnalyzerCacheEntry LoadIsolated(IReadOnlyList<string> analyzerPaths)
    {
        var context = new AnalyzerLoadContext(analyzerPaths[0]);
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

        foreach (var path in analyzerPaths)
        {
            try
            {
                var assembly = context.LoadFromAssemblyPath(path);
                CollectAnalyzers(assembly, analyzers);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[AnalyzerHost] Failed to load assembly '{path}': {ex.Message}");
            }
        }

        return new AnalyzerCacheEntry(context, analyzers.ToImmutable());
    }

    private static void CollectAnalyzers(
        Assembly assembly, ImmutableArray<DiagnosticAnalyzer>.Builder target)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Partial load: work with whichever types loaded successfully
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || !typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
                continue;

            try
            {
                if (Activator.CreateInstance(type) is DiagnosticAnalyzer analyzer)
                    target.Add(analyzer);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[AnalyzerHost] Failed to instantiate '{type.FullName}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Builds a cache key from the project identity, sorted DLL paths, and file metadata
    /// so that a NuGet update (which changes file size or timestamp) produces a different key.
    /// </summary>
    private static string ComputeKey(string projectKey, IReadOnlyList<string> paths)
    {
        var sorted = paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.Append(projectKey).Append("||");

        foreach (var path in sorted)
        {
            sb.Append(path);
            var info = new FileInfo(path);
            if (info.Exists)
            {
                sb.Append(':').Append(info.Length);
                sb.Append(':').Append(info.LastWriteTimeUtc.Ticks);
            }
            sb.Append('|');
        }

        return sb.ToString();
    }

    private void EvictExpiredEntries(object? state)
    {
        if (_disposed) return;

        if (!_lock.TryEnter())
            return; // Skip this cycle if another operation holds the lock

        try
        {
            var now = DateTime.UtcNow;
            var expired = _entries
                .Where(kvp => (now - kvp.Value.LastAccessedUtc) > IdleTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    _entries.Remove(key);
                    entry.Dispose();
                    Console.Error.WriteLine($"[AnalyzerHost] Evicted idle analyzer context.");
                }
            }
        }
        finally
        {
            _lock.Exit();
        }
    }

    private sealed class AnalyzerCacheEntry : IDisposable
    {
        private readonly AnalyzerLoadContext _context;

        public ImmutableArray<DiagnosticAnalyzer> Analyzers { get; }
        public DateTime LastAccessedUtc { get; set; }

        public AnalyzerCacheEntry(AnalyzerLoadContext context, ImmutableArray<DiagnosticAnalyzer> analyzers)
        {
            _context = context;
            Analyzers = analyzers;
            LastAccessedUtc = DateTime.UtcNow;
        }

        public void Dispose()
        {
            _context.Unload();
        }
    }
}
