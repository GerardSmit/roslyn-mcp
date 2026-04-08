using RoslynMCP.Tools;
using Xunit;

namespace RoslynMCP.Tests;

public class GoToDefinitionToolTests
{
    [Fact]
    public async Task WhenEmptyPathProvidedThenReturnsError()
    {
        var result = await GoToDefinitionTool.GoToDefinition(filePath: "", markupSnippet: "[|Add|]");

        Assert.Contains("Error", result);
        Assert.Contains("empty", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenEmptyMarkupProvidedThenReturnsError()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.CalculatorFile, markupSnippet: "");

        Assert.Contains("Error", result);
        Assert.Contains("empty", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenInvalidMarkupProvidedThenReturnsError()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.CalculatorFile, markupSnippet: "no markers here");

        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenMethodSymbolTargetedThenReturnsDefinitionLocation()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.CalculatorFile,
            markupSnippet: "return new Result([|Add|](a, b), Subtract(a, b));");

        Assert.Contains("Definition: Add", result);
        Assert.Contains("Method", result);
        Assert.Contains("Calculator.cs", result);
    }

    [Fact]
    public async Task WhenTypeSymbolTargetedThenReturnsDefinitionLocation()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.CalculatorFile,
            markupSnippet: "new [|Result|](");

        Assert.Contains("Definition: Result", result);
        Assert.Contains("Result.cs", result);
    }

    [Fact]
    public async Task WhenDefinitionFoundThenIncludesCodeContext()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.CalculatorFile,
            markupSnippet: "public int [|Add|](int a, int b)");

        Assert.Contains("Source Location", result);
        Assert.Contains("```csharp", result);
    }

    [Fact]
    public async Task WhenSnippetNotFoundThenReturnsNoMatch()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.CalculatorFile,
            markupSnippet: "void [|DoesNotExist|]()");

        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenFrameworkMethodTargetedThenReturnsMetadataPreview()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.FrameworkReferencesFile,
            markupSnippet: "Console.[|WriteLine|](value);");

        Assert.Contains("Decompiled Source", result);
        Assert.Contains("auto-decompiled", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("System.Console", result);
        Assert.Contains("WriteLine", result);
        Assert.Contains("```csharp", result);
    }

    [Fact]
    public async Task WhenFrameworkTypeTargetedThenReturnsDecompiledTypeSource()
    {
        var result = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.FrameworkReferencesFile,
            markupSnippet: "new [|StringBuilder|]();");

        Assert.Contains("Decompiled Source", result);
        Assert.Contains("StringBuilder", result);
        Assert.Contains("System.Text", result);
        Assert.Contains("```csharp", result);
    }

    [Fact]
    public async Task WhenExternalDependencyTargetedThenNestedGoToDefinitionWorks()
    {
        var externalDefinition = await GoToDefinitionTool.GoToDefinition(
            filePath: FixturePaths.ExternalReferencesFile,
            markupSnippet: "return math.[|AddTen|](value);");

        Assert.Contains("Definition: AddTen", externalDefinition);
        Assert.Contains("Decompiled Source", externalDefinition);
        Assert.Contains("Aardvark.Empty", externalDefinition);

        string decompiledFilePath = ExtractDefinitionFilePath(externalDefinition);

        var nestedDefinition = await GoToDefinitionTool.GoToDefinition(
            filePath: decompiledFilePath,
            markupSnippet: "return [|ApplyOffset|](value, 10);");

        Assert.Contains("Definition: ApplyOffset", nestedDefinition);
        Assert.Contains("Source Location", nestedDefinition);
        Assert.Contains(decompiledFilePath, nestedDefinition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("```csharp", nestedDefinition);
    }

    private static string ExtractDefinitionFilePath(string result)
    {
        const string prefix = "**File**: ";
        string? line = result
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));

        Assert.False(string.IsNullOrWhiteSpace(line));
        return line![prefix.Length..];
    }
}
