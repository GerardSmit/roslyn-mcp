using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class RunCoverageTool
{
    [McpServerTool, Description(
        "Run code coverage collection on a .NET test project. Executes all tests (or a filtered subset) " +
        "with XPlat Code Coverage and caches the results. Must be called before using GetCoverage. " +
        "Re-run after making code changes that affect coverage.")]
    public static async Task<string> RunCoverage(
        [Description("Path to the test project (.csproj) or a source file in the test project.")]
        string projectPath,
        [Description("Optional test filter expression (e.g. 'ClassName.MethodName', 'FullyQualifiedName~MyTest'). If empty, all tests are run.")]
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return "Error: projectPath cannot be empty.";

            string systemPath = PathHelper.NormalizePath(projectPath);

            var result = await CoverageService.RunCoverageAsync(systemPath, filter, cancellationToken);
            return result.Message;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
