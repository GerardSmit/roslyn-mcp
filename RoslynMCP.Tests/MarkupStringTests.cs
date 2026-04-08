using RoslynMCP.Services;
using Xunit;

namespace RoslynMCP.Tests;

public class MarkupStringTests
{
    [Fact]
    public void WhenValidMarkupThenPlainTextHasMarkersRemoved()
    {
        var result = MarkupString.Parse("var x = [|Foo|].Bar();");

        Assert.Equal("var x = Foo.Bar();", result.PlainText);
    }

    [Fact]
    public void WhenValidMarkupThenSpanStartIsCorrect()
    {
        var result = MarkupString.Parse("var x = [|Foo|].Bar();");

        Assert.Equal(8, result.SpanStart);
    }

    [Fact]
    public void WhenValidMarkupThenSpanLengthIsCorrect()
    {
        var result = MarkupString.Parse("var x = [|Foo|].Bar();");

        Assert.Equal(3, result.SpanLength);
    }

    [Fact]
    public void WhenValidMarkupThenMarkedTextIsCorrect()
    {
        var result = MarkupString.Parse("var x = [|Foo|].Bar();");

        Assert.Equal("Foo", result.MarkedText);
    }

    [Fact]
    public void WhenValidMarkupThenMarkedSpanMatchesStartAndLength()
    {
        var result = MarkupString.Parse("var x = [|Foo|].Bar();");

        Assert.Equal(8, result.MarkedSpan.Start);
        Assert.Equal(3, result.MarkedSpan.Length);
    }

    [Fact]
    public void WhenMarkerAtStartThenSpanStartIsZero()
    {
        var result = MarkupString.Parse("[|Foo|].Bar();");

        Assert.Equal(0, result.SpanStart);
        Assert.Equal("Foo.Bar();", result.PlainText);
    }

    [Fact]
    public void WhenMarkerAtEndThenSpanCoversTrailingText()
    {
        var result = MarkupString.Parse("var x = [|Foo|]");

        Assert.Equal("var x = Foo", result.PlainText);
        Assert.Equal("Foo", result.MarkedText);
    }

    [Fact]
    public void WhenNullInputThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse(null!));
    }

    [Fact]
    public void WhenEmptyInputThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse(""));
    }

    [Fact]
    public void WhenNoMarkersThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse("var x = Foo.Bar();"));
    }

    [Fact]
    public void WhenOpenMarkerOnlyThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse("var x = [|Foo.Bar();"));
    }

    [Fact]
    public void WhenEmptySpanThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse("var x = [||].Bar();"));
    }

    [Fact]
    public void WhenNestedMarkersThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse("var x = [|Fo[|o|]|].Bar();"));
    }

    [Fact]
    public void WhenMultipleMarkerPairsThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse("[|Foo|].[|Bar|]()"));
    }

    [Fact]
    public void WhenExtraCloseMarkerThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MarkupString.Parse("[|Foo|] |] extra"));
    }

    [Fact]
    public void WhenTryParseValidInputThenReturnsTrue()
    {
        bool success = MarkupString.TryParse("var x = [|Foo|];", out var result, out var error);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Null(error);
        Assert.Equal("Foo", result.MarkedText);
    }

    [Fact]
    public void WhenTryParseInvalidInputThenReturnsFalse()
    {
        bool success = MarkupString.TryParse("no markers here", out var result, out var error);

        Assert.False(success);
        Assert.Null(result);
        Assert.NotNull(error);
    }
}
