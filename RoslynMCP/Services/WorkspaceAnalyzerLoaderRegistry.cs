using System.Reflection;

namespace RoslynMCP.Services;

/// <summary>
/// Process-wide registry of collectible <see cref="AnalyzerLoadContext"/> instances
/// keyed by source-generator / analyzer source directory.
/// <para>
/// Each per-workspace <see cref="ShadowCopyAnalyzerAssemblyLoader"/> calls
/// <see cref="Acquire"/> the first time it needs to load an assembly from a given
/// source directory, then loads any number of DLLs through the returned
/// <see cref="AlcHandle"/>. The workspace disposes the <see cref="Lease"/> when it
/// is evicted; the ALC unloads once all leases for a (dir, generation) tuple have
/// been released, allowing <c>dotnet build</c> to overwrite the source directory's
/// binaries on the next rebuild.
/// </para>
/// <para>
/// When <see cref="ShadowCopyManager"/> reports a rebuild via its
/// <see cref="ShadowCopyManager.AnalyzerDirectoryChanged"/> event the registry marks
/// the current entry stale (removing it from the active map). New <see cref="Acquire"/>
/// calls for that directory get a fresh ALC and a fresh shadow copy. The stale entry
/// stays alive until its in-flight leases drop, at which point its ALC unloads and
/// the temp DLLs become deletable.
/// </para>
/// </summary>
internal static class WorkspaceAnalyzerLoaderRegistry
{
    private static readonly Lock s_lock = new();
    private static readonly Dictionary<string, Entry> s_active = new(StringComparer.OrdinalIgnoreCase);
    private static int s_subscribed;

    /// <summary>
    /// Acquires (or creates) the collectible ALC for <paramref name="sourceDir"/>.
    /// The returned <see cref="AlcHandle"/> exposes <see cref="AlcHandle.LoadFromPath"/>
    /// to load arbitrary DLLs from that directory; the <see cref="Lease"/> must be
    /// disposed when the owning workspace is evicted.
    /// <paramref name="mainShadowPath"/> seeds the ALC's <see cref="AssemblyDependencyResolver"/>
    /// when the entry is created and is ignored for cache hits.
    /// </summary>
    public static (AlcHandle Handle, Lease Lease) Acquire(string sourceDir, string mainShadowPath)
    {
        EnsureSubscribed();

        lock (s_lock)
        {
            if (!s_active.TryGetValue(sourceDir, out var entry))
            {
                var context = new AnalyzerLoadContext(mainShadowPath);
                entry = new Entry(sourceDir, context);
                s_active[sourceDir] = entry;
                Console.Error.WriteLine(
                    $"[WorkspaceLoaderRegistry] Created ALC for '{sourceDir}'.");
            }

            entry.RefCount++;
            return (new AlcHandle(entry.Context), new Lease(entry));
        }
    }

    /// <summary>
    /// Marks every currently active entry for <paramref name="sourceDir"/> as stale.
    /// Stale entries are removed from the active map so future <see cref="Acquire"/>
    /// calls produce a fresh ALC; the stale ALC is unloaded once its outstanding leases drop.
    /// </summary>
    private static void MarkStale(string sourceDir)
    {
        Entry? stale = null;
        lock (s_lock)
        {
            if (s_active.TryGetValue(sourceDir, out var entry))
            {
                s_active.Remove(sourceDir);
                stale = entry;
            }
        }

        if (stale is not null)
        {
            Console.Error.WriteLine(
                $"[WorkspaceLoaderRegistry] Marked ALC stale for '{sourceDir}' (refcount={stale.RefCount}).");

            // Attempt unload now — succeeds when no leases remain. Otherwise the last
            // lease's Dispose() triggers the actual unload.
            stale.TryUnloadIfUnreferenced();
        }
    }

    private static void EnsureSubscribed()
    {
        if (Interlocked.CompareExchange(ref s_subscribed, 1, 0) == 0)
            ShadowCopyService.Instance.AnalyzerDirectoryChanged += MarkStale;
    }

    /// <summary>
    /// Thin wrapper around an <see cref="AnalyzerLoadContext"/> handed out by the registry.
    /// Loads are deduplicated so repeated <see cref="LoadFromPath"/> calls for the same
    /// shadow path return the same <see cref="Assembly"/> instance.
    /// </summary>
    public sealed class AlcHandle
    {
        private readonly AnalyzerLoadContext _context;

        internal AlcHandle(AnalyzerLoadContext context) => _context = context;

        public Assembly LoadFromPath(string shadowPath) => _context.LoadFromAssemblyPath(shadowPath);
    }

    /// <summary>
    /// Reference token returned by <see cref="Acquire"/>. Disposing decrements the
    /// owning entry's refcount and unloads the ALC if the entry is stale and unreferenced.
    /// </summary>
    public sealed class Lease : IDisposable
    {
        private Entry? _entry;

        internal Lease(Entry entry) => _entry = entry;

        public void Dispose()
        {
            var entry = Interlocked.Exchange(ref _entry, null);
            if (entry is null) return;

            bool unloadNow;
            lock (s_lock)
            {
                entry.RefCount--;
                bool isActive = s_active.TryGetValue(entry.SourceDir, out var current) && ReferenceEquals(current, entry);
                unloadNow = !isActive && entry.RefCount <= 0 && !entry.Unloaded;
                if (unloadNow)
                    entry.Unloaded = true;
            }

            if (unloadNow)
            {
                entry.Context.Unload();
                Console.Error.WriteLine(
                    $"[WorkspaceLoaderRegistry] Unloaded ALC for '{entry.SourceDir}'.");
            }
        }
    }

    internal sealed class Entry
    {
        public string SourceDir { get; }
        public AnalyzerLoadContext Context { get; }
        public int RefCount;
        public bool Unloaded;

        public Entry(string sourceDir, AnalyzerLoadContext context)
        {
            SourceDir = sourceDir;
            Context = context;
        }

        public void TryUnloadIfUnreferenced()
        {
            bool unloadNow;
            lock (s_lock)
            {
                unloadNow = RefCount <= 0 && !Unloaded;
                if (unloadNow)
                    Unloaded = true;
            }

            if (unloadNow)
            {
                Context.Unload();
                Console.Error.WriteLine(
                    $"[WorkspaceLoaderRegistry] Unloaded ALC for '{SourceDir}' (no leases).");
            }
        }
    }
}
