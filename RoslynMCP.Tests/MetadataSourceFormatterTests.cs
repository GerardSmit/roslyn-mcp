using RoslynMCP.Services;
using Xunit;

namespace RoslynMCP.Tests;

public class MetadataSourceFormatterTests
{
    [Fact]
    public async Task WhenFormattingExternalMethodThenMetadataPreviewIncludesTargetMember()
    {
        var symbol = await RoslynTestHelpers.ResolveSymbolAsync(
            FixturePaths.FrameworkReferencesFile,
            "Console.[|WriteLine|](value);");

        var result = MetadataSourceFormatter.FormatExternalDefinition(symbol);

        Assert.Contains("External Definition", result);
        Assert.Contains("System.Console", result);
        Assert.Contains("WriteLine", result);
        Assert.Contains("// target symbol", result);
    }

    [Fact]
    public async Task WhenFormattingExternalTypeThenPreviewIncludesContainingMembers()
    {
        var symbol = await RoslynTestHelpers.ResolveSymbolAsync(
            FixturePaths.FrameworkReferencesFile,
            "new [|StringBuilder|]();");

        var result = MetadataSourceFormatter.FormatExternalDefinition(symbol);

        Assert.Contains("StringBuilder", result);
        Assert.Contains("metadata-as-source", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Append", result);
    }

    [Fact]
    public async Task WhenFormattingRecordTypeThenRecordDeclarationIsIncluded()
    {
        var symbol = await RoslynTestHelpers.GetNamedTypeAsync(
            FixturePaths.SampleProjectFile,
            "SampleProject.OutlineRecord");

        var result = MetadataSourceFormatter.FormatExternalDefinition(symbol);

        Assert.Contains("record class OutlineRecord", result);
    }

    [Fact]
    public async Task WhenFormattingEnumTypeThenEnumMembersAreListed()
    {
        var symbol = await RoslynTestHelpers.GetNamedTypeAsync(
            FixturePaths.SampleProjectFile,
            "SampleProject.OutlineKind");

        var result = MetadataSourceFormatter.FormatExternalDefinition(symbol);

        Assert.Contains("enum OutlineKind", result);
        Assert.Contains("Basic,", result);
        Assert.Contains("Advanced,", result);
    }

    [Fact]
    public async Task WhenFormattingDelegateTypeThenDelegateSignatureIsIncluded()
    {
        var symbol = await RoslynTestHelpers.GetNamedTypeAsync(
            FixturePaths.SampleProjectFile,
            "SampleProject.ValueFormatter");

        var result = MetadataSourceFormatter.FormatExternalDefinition(symbol);

        Assert.Contains("delegate string ValueFormatter(int value);", result);
    }

    [Fact]
    public async Task WhenFormattingFocusedSourceMemberThenContainingTypePreviewMarksTarget()
    {
        var symbol = await RoslynTestHelpers.ResolveSymbolAsync(
            FixturePaths.OutlineShowcaseFile,
            "public string [|Name|] { get; init; } = string.Empty;");

        var result = MetadataSourceFormatter.FormatExternalDefinition(symbol);

        Assert.Contains("class OutlineShowcase", result);
        Assert.Contains("// target symbol", result);
        Assert.Contains("string Name", result);
    }
}
