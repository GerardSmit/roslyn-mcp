using RoslynMCP.Tools;
using Xunit;

namespace RoslynMCP.Tests;

public class ValidateFileToolTests
{
    [Fact]
    public async Task WhenEmptyPathProvidedThenReturnsError()
    {
        var result = await ValidateFileTool.ValidateFile(filePath: "", runAnalyzers: false);

        Assert.Contains("Error", result);
        Assert.Contains("empty", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenWhitespacePathProvidedThenReturnsError()
    {
        var result = await ValidateFileTool.ValidateFile(filePath: "   ", runAnalyzers: false);

        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task WhenNonExistentFileProvidedThenReturnsFileNotFoundError()
    {
        var result = await ValidateFileTool.ValidateFile(
            filePath: Path.Combine(FixturePaths.SampleProjectDir, "DoesNotExist.cs"),
            runAnalyzers: false);

        Assert.Contains("does not exist", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenValidFileProvidedThenReportsNoSyntaxErrors()
    {
        var result = await ValidateFileTool.ValidateFile(
            filePath: FixturePaths.CalculatorFile,
            runAnalyzers: false);

        Assert.Contains("No syntax errors found", result);
    }

    [Fact]
    public async Task WhenValidFileProvidedThenReportsProjectLoaded()
    {
        var result = await ValidateFileTool.ValidateFile(
            filePath: FixturePaths.CalculatorFile,
            runAnalyzers: false);

        Assert.Contains("Project loaded successfully", result);
    }

    [Fact]
    public async Task WhenValidFileProvidedThenReportsDocumentFound()
    {
        var result = await ValidateFileTool.ValidateFile(
            filePath: FixturePaths.CalculatorFile,
            runAnalyzers: false);

        Assert.Contains("Document found", result);
    }

    [Fact]
    public async Task WhenBrokenSyntaxFileProvidedThenReportsSyntaxErrors()
    {
        var result = await ValidateFileTool.ValidateFile(
            filePath: FixturePaths.BrokenSyntaxFile,
            runAnalyzers: false);

        Assert.Contains("Syntax errors found", result);
        Assert.Contains("Line", result);
    }

    [Fact]
    public async Task WhenBrokenSemanticFileProvidedThenReportsSemanticAndCompilationErrors()
    {
        var result = await ValidateFileTool.ValidateFile(
            filePath: FixturePaths.BrokenSemanticFile,
            runAnalyzers: false);

        Assert.Contains("Semantic errors found", result);
        Assert.Contains("Compilation and analyzer diagnostics", result);
        Assert.Contains("CS", result);
    }

    [Fact]
    public async Task WhenAnalyzersEnabledThenValidationIncludesAnalyzerDiagnostics()
    {
        var result = await ValidateFileTool.ValidateFile(
            filePath: FixturePaths.WarningsFile,
            runAnalyzers: true);

        Assert.Contains("Running code analyzers", result);
        Assert.Contains("Compilation and analyzer diagnostics", result);
        Assert.Contains("CA", result);
    }

    [Fact]
    public async Task WhenFileIsNotPartOfProjectThenValidationListsProjectDocuments()
    {
        var writer = new StringWriter();

        await ValidateFileTool.ValidateFileInProjectContextAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cs"),
            FixturePaths.SampleProjectFile,
            writer,
            runAnalyzers: false);

        var result = writer.ToString();

        Assert.Contains("Error: File not found in the project documents.", result);
        Assert.Contains("All project documents:", result);
    }
}
