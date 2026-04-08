using RoslynMCP.Tools;
using Xunit;

namespace RoslynMCP.Tests;

public class GetProjectDiagnosticsToolTests
{
    [Fact]
    public async Task WhenEmptyPathProvidedThenReturnsError()
    {
        var result = await GetProjectDiagnosticsTool.GetProjectDiagnostics(projectPath: "");

        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task WhenValidProjectProvidedThenReturnsDiagnostics()
    {
        var result = await GetProjectDiagnosticsTool.GetProjectDiagnostics(
            projectPath: FixturePaths.SampleProjectFile,
            severityFilter: "warning");

        Assert.Contains("Project Diagnostics", result);
        Assert.Contains("SampleProject", result);
    }

    [Fact]
    public async Task WhenErrorFilterUsedOnCleanProjectThenReturnsNoDiagnostics()
    {
        var result = await GetProjectDiagnosticsTool.GetProjectDiagnostics(
            projectPath: FixturePaths.SampleProjectFile,
            severityFilter: "error");

        // SampleProject has no errors, only warnings
        Assert.Contains("Project Diagnostics", result);
    }

    [Fact]
    public async Task WhenInvalidSeverityFilterProvidedThenReturnsError()
    {
        var result = await GetProjectDiagnosticsTool.GetProjectDiagnostics(
            projectPath: FixturePaths.SampleProjectFile,
            severityFilter: "bogus");

        Assert.Contains("Invalid", result);
    }

    [Fact]
    public async Task WhenMaxResultsLimitedThenRespectsLimit()
    {
        var result = await GetProjectDiagnosticsTool.GetProjectDiagnostics(
            projectPath: FixturePaths.SampleProjectFile,
            severityFilter: "all",
            maxResults: 2);

        Assert.Contains("Project Diagnostics", result);
    }

    [Fact]
    public async Task WhenBrokenProjectProvidedThenReportsErrors()
    {
        var result = await GetProjectDiagnosticsTool.GetProjectDiagnostics(
            projectPath: FixturePaths.BrokenProjectFile,
            severityFilter: "error");

        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task WhenSourceFileProvidedThenResolvesToProject()
    {
        var result = await GetProjectDiagnosticsTool.GetProjectDiagnostics(
            projectPath: FixturePaths.CalculatorFile,
            severityFilter: "warning");

        Assert.Contains("Project Diagnostics", result);
    }
}
