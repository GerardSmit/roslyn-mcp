using System.Reflection;
using Microsoft.CodeAnalysis;

namespace RoslynMCP.Services;

/// <summary>
/// Per-workspace <see cref="IAnalyzerAssemblyLoader"/> that loads shadow-copied
/// analyzer / source-generator DLLs through a collectible
/// <see cref="AnalyzerLoadContext"/> from <see cref="WorkspaceAnalyzerLoaderRegistry"/>.
/// <para>
/// The loader keeps one <see cref="WorkspaceAnalyzerLoaderRegistry.Lease"/> per
/// originating source directory it touches; all leases are released when
/// <see cref="Dispose"/> runs (called by <see cref="WorkspaceService"/> when the
/// workspace is evicted). NuGet-package analyzers in the immutable global packages
/// folder are loaded straight from disk via the default load context, matching
/// Roslyn's stock behaviour.
/// </para>
/// <para>
/// <b>Important:</b> Roslyn's <c>AnalyzerFileReference.GetMetadata()</c> opens
/// <c>FullPath</c> with a <see cref="System.Reflection.PortableExecutable.PEReader"/>
/// to enumerate analyzer types; that open completely bypasses
/// <see cref="IAnalyzerAssemblyLoader"/>. To avoid locking the original DLL the
/// rebind code in <see cref="WorkspaceService"/> calls <see cref="Register"/> to
/// shadow-copy the DLL up front and constructs each <see cref="AnalyzerFileReference"/>
/// with the returned shadow path as its <c>FullPath</c>.
/// </para>
/// </summary>
internal sealed class ShadowCopyAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader, IDisposable
{
    private readonly Lock _lock = new();
    private readonly ShadowCopyManager _shadowCopy = ShadowCopyService.Instance;

    /// <summary>Originating source directory → (ALC handle, registry lease).</summary>
    private readonly Dictionary<string, (WorkspaceAnalyzerLoaderRegistry.AlcHandle Handle, WorkspaceAnalyzerLoaderRegistry.Lease Lease)> _bySourceDir =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Shadow assembly path → originating source directory. Populated by
    /// <see cref="Register"/> during rebind; consulted in <see cref="LoadFromPath"/>
    /// so the registry key matches the source-directory tracking in
    /// <see cref="WorkspaceService"/> and the <see cref="ShadowCopyManager"/>
    /// rebuild event.
    /// </summary>
    private readonly Dictionary<string, string> _shadowToSourceDir =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loaded assembly cache keyed on shadow path.</summary>
    private readonly Dictionary<string, Assembly> _loaded =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    /// <summary>
    /// Shadow-copies the DLL at <paramref name="originalPath"/> up front and returns
    /// the resulting shadow path. The rebind code in <see cref="WorkspaceService"/>
    /// uses the returned path as the <c>FullPath</c> for the replacement
    /// <see cref="AnalyzerFileReference"/> so that both Roslyn's metadata reader and
    /// our own assembly loader operate on the shadow copy, leaving the original DLL
    /// completely unlocked.
    /// </summary>
    public string Register(string originalPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_shadowCopy.NeedsShadowCopy(originalPath))
            return originalPath;

        string sourceDir = Path.GetDirectoryName(Path.GetFullPath(originalPath))!;
        string shadowPath = _shadowCopy.GetLoadPath(originalPath);

        lock (_lock)
        {
            _shadowToSourceDir[shadowPath] = sourceDir;
        }

        return shadowPath;
    }

    /// <inheritdoc />
    public void AddDependencyLocation(string fullPath)
    {
        // No-op: dependencies are resolved through the per-dir AnalyzerLoadContext
        // which probes the shadow directory and reads .deps.json itself.
    }

    /// <inheritdoc />
    public Assembly LoadFromPath(string fullPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // NuGet-cache analyzers don't need shadow-copying — the cache is immutable
        // and Roslyn's default loader handles them fine.
        if (!_shadowCopy.NeedsShadowCopy(fullPath))
            return Assembly.LoadFrom(fullPath);

        lock (_lock)
        {
            if (_loaded.TryGetValue(fullPath, out var cached))
                return cached;

            // Prefer the originating source dir recorded by Register so the registry
            // key matches the source-dir-keyed eviction signal from ShadowCopyManager.
            if (!_shadowToSourceDir.TryGetValue(fullPath, out var sourceDir))
                sourceDir = Path.GetDirectoryName(Path.GetFullPath(fullPath))!;

            if (!_bySourceDir.TryGetValue(sourceDir, out var entry))
            {
                var (handle, lease) = WorkspaceAnalyzerLoaderRegistry.Acquire(sourceDir, fullPath);
                entry = (handle, lease);
                _bySourceDir[sourceDir] = entry;
            }

            var assembly = entry.Handle.LoadFromPath(fullPath);
            _loaded[fullPath] = assembly;
            return assembly;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var (_, lease) in _bySourceDir.Values)
                lease.Dispose();
            _bySourceDir.Clear();
            _shadowToSourceDir.Clear();
            _loaded.Clear();
        }
    }
}
