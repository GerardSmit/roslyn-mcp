using RoslynMCP.Tools;
using Xunit;

namespace RoslynMCP.Tests;

public sealed class ExpandVarTypesToolTests
{
    [Fact]
    public async Task WhenVarMethod_ExplicitTypesAreReturned()
    {
        var result = await ExpandVarTypesTool.ExpandVarTypes(
            FixturePaths.VarUsagesFile, "VarMethod");

        Assert.Contains("Calculator? calc", result);
        Assert.Contains("int result", result);
        Assert.Contains("string? text", result);
        Assert.DoesNotContain("var calc", result);
        Assert.DoesNotContain("var result", result);
        Assert.DoesNotContain("var text", result);
    }

    [Fact]
    public async Task WhenForeachMethod_ExplicitTypesAreReturned()
    {
        var result = await ExpandVarTypesTool.ExpandVarTypes(
            FixturePaths.VarUsagesFile, "ForeachMethod");

        Assert.Contains("int[]? numbers", result);
        Assert.Contains("foreach (int n", result);
        Assert.Contains("int doubled", result);
        Assert.DoesNotContain("var numbers", result);
    }

    [Fact]
    public async Task WhenMethodNotFound_ReturnsError()
    {
        var result = await ExpandVarTypesTool.ExpandVarTypes(
            FixturePaths.VarUsagesFile, "NonExistentMethod");

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task WhenMultipleOverloads_ListsThemWithoutHintLine()
    {
        var result = await ExpandVarTypesTool.ExpandVarTypes(
            FixturePaths.VarUsagesFile, "VarMethod");

        Assert.DoesNotContain("hintLine", result);
    }
}