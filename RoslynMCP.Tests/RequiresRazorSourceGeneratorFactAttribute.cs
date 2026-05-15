using Xunit;

namespace RoslynMCP.Tests;

/// <summary>
/// Skips the test when the bundled Razor source generator cannot load — typically
/// because the host SDK ships a newer Roslyn (e.g. 5.5.x) than the
/// Microsoft.CodeAnalysis NuGet packages we restore (currently capped at 5.3.0).
/// In that case <see cref="Microsoft.CodeAnalysis.Diagnostics.AnalyzerFileReference"/>
/// rejects the SG analyzer with 'ReferencesNewerCompiler' and the project exposes
/// zero source-generated documents.
/// </summary>
public sealed class RequiresRazorSourceGeneratorFactAttribute : FactAttribute
{
    public RequiresRazorSourceGeneratorFactAttribute()
    {
        if (!TestEnvironment.IsRazorSourceGeneratorAvailable.Value)
            Skip = "Razor source generator cannot load in this environment (host Roslyn is newer than the referenced Microsoft.CodeAnalysis NuGet package).";
    }
}
