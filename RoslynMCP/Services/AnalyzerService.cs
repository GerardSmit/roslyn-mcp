using System.Collections.Immutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynMCP.Services;

/// <summary>
/// Discovers, loads, and executes Roslyn diagnostic analyzers from the project context.
/// Analyzer DLLs are resolved from the <see cref="Project.AnalyzerReferences"/>
/// populated by MSBuildWorkspace, then loaded in an isolated collectible
/// <see cref="AnalyzerLoadContext"/> via <see cref="AnalyzerHost"/>.
/// </summary>
internal static class AnalyzerService
{
    private static readonly AnalyzerHost s_analyzerHost = new();

    static AnalyzerService()
    {
        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DisposeHost();
        AssemblyLoadContext.Default.Unloading += static _ => DisposeHost();
    }

    /// <summary>
    /// Discovers analyzer DLL paths from the Roslyn <see cref="Project"/> context.
    /// Uses <see cref="Project.AnalyzerReferences"/> which MSBuildWorkspace populates
    /// from NuGet package analyzer assets and explicit <c>&lt;Analyzer&gt;</c> items.
    /// </summary>
    internal static List<string> DiscoverAnalyzerPathsFromProject(Project project)
    {
        var paths = new List<string>();

        foreach (var reference in project.AnalyzerReferences)
        {
            var fullPath = reference.FullPath;
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                paths.Add(fullPath);
        }

        return paths;
    }

    /// <summary>
    /// Loads analyzers specific to the given <paramref name="project"/> by reading its
    /// resolved analyzer references and loading them in an isolated ALC.
    /// Results are cached by <see cref="AnalyzerHost"/> keyed on project identity
    /// and the resolved DLL set with file metadata.
    /// </summary>
    public static ImmutableArray<DiagnosticAnalyzer> LoadAnalyzersForProject(Project project)
    {
        var analyzerPaths = DiscoverAnalyzerPathsFromProject(project);

        if (analyzerPaths.Count == 0)
            return ImmutableArray<DiagnosticAnalyzer>.Empty;

        string projectKey = project.FilePath ?? project.Name;
        return s_analyzerHost.GetOrLoadAnalyzers(projectKey, analyzerPaths);
    }

    /// <summary>
    /// Runs project-specific analyzers against a compilation and returns diagnostics
    /// filtered to the specified <paramref name="filePath"/>.
    /// Analyzer DLLs are discovered from the <paramref name="project"/>'s analyzer
    /// references rather than a global NuGet directory.
    /// Progress and errors are written to <paramref name="writer"/>.
    /// </summary>
    public static async Task<IEnumerable<Diagnostic>> RunAnalyzersAsync(
        Project project, Compilation compilation, string filePath, TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        writer.WriteLine("\nRunning code analyzers...");

        try
        {
            var analyzers = LoadAnalyzersForProject(project);

            if (analyzers.Length > 0)
            {
                writer.WriteLine(
                    $"Found {analyzers.Length} analyzer(s) from {project.AnalyzerReferences.Count} project analyzer reference(s)");

                var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

                var allDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);

                return allDiagnostics.Where(d =>
                    d.Location.SourceTree != null &&
                    string.Equals(d.Location.SourceTree.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                writer.WriteLine(
                    "No analyzer references found in project. " +
                    "Analyzers are discovered from project-level NuGet packages and <Analyzer> items.");
                return Array.Empty<Diagnostic>();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            writer.WriteLine($"Error running analyzers: {ex.Message}");
            return Array.Empty<Diagnostic>();
        }
    }

    /// <summary>
    /// Evicts cached analyzer contexts for a specific project path,
    /// unloading the associated collectible <see cref="AnalyzerLoadContext"/>.
    /// </summary>
    public static void EvictAnalyzersForProject(string projectPath) =>
        s_analyzerHost.EvictForProject(projectPath);

    /// <summary>
    /// Unloads all cached analyzer contexts, forcing a fresh load on next use.
    /// </summary>
    public static void UnloadAnalyzers() => s_analyzerHost.UnloadAll();

    public static void DisposeHost() => s_analyzerHost.Dispose();
}
