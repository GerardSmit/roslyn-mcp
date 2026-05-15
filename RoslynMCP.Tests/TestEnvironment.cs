using Microsoft.CodeAnalysis.MSBuild;
using RoslynMCP.Services;

namespace RoslynMCP.Tests;

/// <summary>
/// Provides information about the test execution environment.
/// </summary>
internal static class TestEnvironment
{
    /// <summary>
    /// Returns <c>true</c> when Visual Studio or Build Tools MSBuild was registered
    /// by <see cref="WorkspaceService"/>, enabling legacy .csproj support.
    /// </summary>
    public static bool HasVisualStudioMSBuild => WorkspaceService.IsLegacyProjectSupported;

    /// <summary>
    /// Probes whether the bundled Razor source generator can load and produce
    /// source-generated documents for the Blazor fixture project. Result is cached.
    /// </summary>
    public static readonly Lazy<bool> IsRazorSourceGeneratorAvailable = new(ProbeRazorSourceGenerator);

    private static bool ProbeRazorSourceGenerator()
    {
        try
        {
            // Trigger MSBuildLocator registration before creating a bare workspace.
            WorkspaceService.EnsureRegistered();
            using var workspace = MSBuildWorkspace.Create();
            var project = workspace.OpenProjectAsync(FixturePaths.BlazorProjectFile).GetAwaiter().GetResult();
            var docs = project.GetSourceGeneratedDocumentsAsync().GetAwaiter().GetResult();
            return docs.Any();
        }
        catch
        {
            return false;
        }
    }
}
