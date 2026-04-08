using System.Reflection;
using System.Runtime.Loader;

namespace RoslynMCP.Services;

/// <summary>
/// Collectible <see cref="AssemblyLoadContext"/> that isolates diagnostic-analyzer
/// (and future source-generator) assemblies from the default load context.
/// Roslyn and runtime assemblies are forwarded to the default context so that
/// shared types like <see cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer"/>
/// retain type identity across the boundary.
/// </summary>
internal sealed class AnalyzerLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _analyzerDirectory;

    public AnalyzerLoadContext(string mainAssemblyPath)
        : base($"AnalyzerALC({Path.GetFileName(mainAssemblyPath)})", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _analyzerDirectory = Path.GetDirectoryName(mainAssemblyPath) ?? string.Empty;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Roslyn / runtime types must come from the host to preserve type identity
        if (ShouldDeferToDefault(assemblyName.Name))
            return null;

        // Try the .deps.json-based resolver first
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null)
            return LoadFromAssemblyPath(path);

        // Probe the analyzer directory for co-located dependencies
        if (assemblyName.Name != null)
        {
            string candidate = Path.Combine(_analyzerDirectory, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
                return LoadFromAssemblyPath(candidate);
        }

        return null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path != null ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }

    private static bool ShouldDeferToDefault(string? name)
    {
        if (name is null)
            return false;

        // Roslyn assemblies must be shared for DiagnosticAnalyzer / ISourceGenerator identity
        if (name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
            return true;

        // Runtime / BCL assemblies
        if (name.StartsWith("System.", StringComparison.Ordinal) || name == "System")
            return true;

        if (name.StartsWith("netstandard", StringComparison.Ordinal))
            return true;

        return false;
    }
}
