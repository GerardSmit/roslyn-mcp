using RoslynMCP.Tools;
using Xunit;

namespace RoslynMCP.Tests;

public class FindSymbolToolTests
{
    [Fact]
    public async Task WhenEmptyPathProvidedThenReturnsError()
    {
        var result = await FindSymbolTool.FindSymbol(filePath: "", symbolName: "Add");

        Assert.Contains("Error", result);
        Assert.Contains("empty", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenEmptySymbolNameProvidedThenReturnsError()
    {
        var result = await FindSymbolTool.FindSymbol(
            filePath: FixturePaths.CalculatorFile, symbolName: "");

        Assert.Contains("Error", result);
        Assert.Contains("empty", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenMethodNameSearchedThenFindsMethod()
    {
        var result = await FindSymbolTool.FindSymbol(
            filePath: FixturePaths.CalculatorFile, symbolName: "Add");

        Assert.Contains("Symbol Search", result);
        Assert.Contains("Add", result);
        Assert.Contains("Method", result);
        Assert.Contains("Calculator.cs", result);
    }

    [Fact]
    public async Task WhenTypeNameSearchedThenFindsType()
    {
        var result = await FindSymbolTool.FindSymbol(
            filePath: FixturePaths.CalculatorFile, symbolName: "Calculator");

        Assert.Contains("Calculator", result);
        Assert.Contains("NamedType", result);
    }

    [Fact]
    public async Task WhenNonExistentSymbolSearchedThenReportsNoResults()
    {
        var result = await FindSymbolTool.FindSymbol(
            filePath: FixturePaths.CalculatorFile, symbolName: "ZZZDoesNotExistZZZ");

        Assert.Contains("No matching symbols found", result);
    }

    [Fact]
    public async Task WhenPatternMatchedThenFindsMultipleSymbols()
    {
        // "Calc" should match "Calculator" via substring/pattern matching
        var result = await FindSymbolTool.FindSymbol(
            filePath: FixturePaths.CalculatorFile, symbolName: "Calc");

        Assert.Contains("Calculator", result);
    }

    [Fact]
    public async Task WhenResultTypeSearchedThenFindsRecordInOtherFile()
    {
        var result = await FindSymbolTool.FindSymbol(
            filePath: FixturePaths.CalculatorFile, symbolName: "Result");

        Assert.Contains("Result", result);
        Assert.Contains("Result.cs", result);
    }
}
