using RoslynMCP.Services;
using RoslynMCP.Tools;
using Xunit;

namespace RoslynMCP.Tests;

public class SourceGeneratedFilesToolTests
{
    [Fact]
    public async Task ListSourceGeneratedFiles_WhenEmptyPath_ThenReturnsError()
    {
        var result = await SourceGeneratedFilesTool.ListSourceGeneratedFiles("", new MarkdownFormatter());
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task ListSourceGeneratedFiles_WhenNonExistentPath_ThenReturnsError()
    {
        var result = await SourceGeneratedFilesTool.ListSourceGeneratedFiles(
            @"C:\nonexistent\project.csproj", new MarkdownFormatter());
        Assert.Contains("does not exist", result);
    }

    [Fact]
    public async Task ListSourceGeneratedFiles_WhenProjectHasNoGenerators_ThenReturnsEmpty()
    {
        var result = await SourceGeneratedFilesTool.ListSourceGeneratedFiles(
            FixturePaths.SampleProjectFile, new MarkdownFormatter());

        // SampleProject has no source generators
        Assert.Contains("No source-generated files", result);
    }

    [Fact]
    public async Task ListSourceGeneratedFiles_WhenBlazorProject_ThenListsRazorGeneratedFiles()
    {
        var result = await SourceGeneratedFilesTool.ListSourceGeneratedFiles(
            FixturePaths.BlazorProjectFile, new MarkdownFormatter());

        Assert.Contains("Source Generated Files", result);
        Assert.Contains("hintName", result);
    }

    [Fact]
    public async Task ListSourceGeneratedFiles_WhenSourceFileProvided_ThenResolvesProject()
    {
        var result = await SourceGeneratedFilesTool.ListSourceGeneratedFiles(
            FixturePaths.BlazorAppHelperFile, new MarkdownFormatter());

        Assert.Contains("Source Generated Files", result);
    }

    [Fact]
    public async Task GetSourceGeneratedFileContent_WhenEmptyPath_ThenReturnsError()
    {
        var result = await SourceGeneratedFilesTool.GetSourceGeneratedFileContent("", "test.g.cs");
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task GetSourceGeneratedFileContent_WhenEmptyHintName_ThenReturnsError()
    {
        var result = await SourceGeneratedFilesTool.GetSourceGeneratedFileContent(
            FixturePaths.BlazorProjectFile, "");
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task GetSourceGeneratedFileContent_WhenInvalidHintName_ThenReturnsError()
    {
        var result = await SourceGeneratedFilesTool.GetSourceGeneratedFileContent(
            FixturePaths.BlazorProjectFile, "NonExistent_File_That_Cannot_Exist.g.cs");
        Assert.Contains("No source-generated file matching", result);
    }

    [Fact]
    public async Task GetSourceGeneratedFileContent_WhenValidHintName_ThenReturnsContent()
    {
        // First list to discover a valid hint name
        var listResult = await SourceGeneratedFilesTool.ListSourceGeneratedFiles(
            FixturePaths.BlazorProjectFile, new MarkdownFormatter());

        Assert.Contains("Source Generated Files", listResult);

        // Get the generated docs directly to find a valid hint name
        var project = await RoslynTestHelpers.OpenProjectAsync(FixturePaths.BlazorProjectFile);
        var generatedDocs = (await project.GetSourceGeneratedDocumentsAsync()).ToList();
        Assert.NotEmpty(generatedDocs);

        var firstDoc = generatedDocs[0];
        var hintName = firstDoc.HintName ?? firstDoc.Name;

        var result = await SourceGeneratedFilesTool.GetSourceGeneratedFileContent(
            FixturePaths.BlazorProjectFile, hintName);

        Assert.Contains("Source-generated file", result);
        Assert.Contains("Generator", result);
        Assert.Contains("Lines", result);
        // Should have line numbers
        Assert.Contains("    1. ", result);
    }

    [Fact]
    public async Task GetSourceGeneratedFileContent_WhenPartialHintName_ThenMatchesByContains()
    {
        var project = await RoslynTestHelpers.OpenProjectAsync(FixturePaths.BlazorProjectFile);
        var generatedDocs = (await project.GetSourceGeneratedDocumentsAsync()).ToList();
        Assert.NotEmpty(generatedDocs);

        // Use just the last part of the hint name (partial match)
        var firstDoc = generatedDocs[0];
        var fullHintName = firstDoc.HintName ?? firstDoc.Name ?? "";
        var partial = Path.GetFileNameWithoutExtension(fullHintName);

        var result = await SourceGeneratedFilesTool.GetSourceGeneratedFileContent(
            FixturePaths.BlazorProjectFile, partial);

        Assert.Contains("Source-generated file", result);
    }
}
