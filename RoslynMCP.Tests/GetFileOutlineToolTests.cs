using RoslynMCP.Tools;
using Xunit;

namespace RoslynMCP.Tests;

public class GetFileOutlineToolTests
{
    [Fact]
    public async Task WhenEmptyPathProvidedThenReturnsError()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: "");

        Assert.Contains("Error", result);
        Assert.Contains("empty", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenNonExistentFileProvidedThenReturnsError()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: "Z:/nonexistent/file.cs");

        Assert.Contains("Error", result);
        Assert.Contains("does not exist", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenCalculatorFileProvidedThenShowsNamespace()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.CalculatorFile);

        Assert.Contains("namespace SampleProject", result);
    }

    [Fact]
    public async Task WhenCalculatorFileProvidedThenShowsClassDeclaration()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.CalculatorFile);

        Assert.Contains("public class Calculator", result);
    }

    [Fact]
    public async Task WhenCalculatorFileProvidedThenShowsMethods()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.CalculatorFile);

        Assert.Contains("Add", result);
        Assert.Contains("Subtract", result);
        Assert.Contains("Compute", result);
    }

    [Fact]
    public async Task WhenCalculatorFileProvidedThenShowsLineNumbers()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.CalculatorFile);

        // Line numbers should appear as "N:" prefix
        Assert.Matches(@"\d+:\s+", result);
    }

    [Fact]
    public async Task WhenRecordFileProvidedThenShowsRecordDeclaration()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.ResultFile);

        Assert.Contains("record", result);
        Assert.Contains("Result", result);
    }

    [Fact]
    public async Task WhenCalculatorFileProvidedThenIncludesOutlineHeader()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.CalculatorFile);

        Assert.Contains("# Outline:", result);
        Assert.Contains("Calculator.cs", result);
    }

    [Fact]
    public async Task WhenOutlineShowcaseProvidedThenTopLevelDeclarationsAreIncluded()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.OutlineShowcaseFile);

        Assert.Contains("delegate string ValueFormatter(int value)", result);
        Assert.Contains("public enum OutlineKind", result);
        Assert.Contains("public record OutlineRecord(int Value)", result);
    }

    [Fact]
    public async Task WhenOutlineShowcaseProvidedThenFieldsPropertiesAndEventsAreIncluded()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.OutlineShowcaseFile);

        Assert.Contains("public const int DefaultValue", result);
        Assert.Contains("private readonly List<int> _values", result);
        Assert.Contains("public event EventHandler? Changed", result);
        Assert.Contains("public event EventHandler? Routed", result);
        Assert.Contains("public string Name { get; init; }", result);
        Assert.Contains("public int this[int index] { get; set; }", result);
    }

    [Fact]
    public async Task WhenOutlineShowcaseProvidedThenConstructorsAndOperatorsAreIncluded()
    {
        var result = await GetFileOutlineTool.GetFileOutline(filePath: FixturePaths.OutlineShowcaseFile);

        Assert.Contains("public OutlineShowcase()", result);
        Assert.Contains("~OutlineShowcase()", result);
        Assert.Contains("public static OutlineShowcase operator +(OutlineShowcase left, OutlineShowcase right)", result);
        Assert.Contains("public static implicit operator int(OutlineShowcase value)", result);
    }
}
