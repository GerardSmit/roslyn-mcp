namespace RoslynMCP.Services;

/// <summary>
/// Process-wide singleton accessor for <see cref="ShadowCopyManager"/>.
/// Shared by <see cref="AnalyzerHost"/> (explicit analyzer loads) and
/// <see cref="WorkspaceAnalyzerLoaderRegistry"/> (workspace-driven source-generator loads).
/// </summary>
internal static class ShadowCopyService
{
    private static readonly Lazy<ShadowCopyManager> s_instance =
        new(() => new ShadowCopyManager(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static ShadowCopyManager Instance => s_instance.Value;

    public static void DisposeIfCreated()
    {
        if (s_instance.IsValueCreated)
            s_instance.Value.Dispose();
    }
}
