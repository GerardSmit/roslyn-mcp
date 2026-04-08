using RoslynMCP.Prompts;
using Xunit;

namespace RoslynMCP.Tests;

public class ValidateAfterEditPromptTests
{
    [Fact]
    public void WhenFilePathProvidedThenReturnsNonEmptyPrompt()
    {
        var prompt = ValidateAfterEditPrompt.ValidateAfterEdit(filePath: "C:/src/MyFile.cs");

        Assert.NotEmpty(prompt);
    }

    [Fact]
    public void WhenFilePathProvidedThenPromptContainsValidationInstructions()
    {
        var prompt = ValidateAfterEditPrompt.ValidateAfterEdit(filePath: "C:/src/MyFile.cs");

        Assert.Contains("Please validate my changes", prompt);
    }

    [Fact]
    public void WhenFilePathProvidedThenPromptContainsFilePath()
    {
        const string path = "C:/src/MyFile.cs";
        var prompt = ValidateAfterEditPrompt.ValidateAfterEdit(filePath: path);

        Assert.Contains(path, prompt);
    }

    [Fact]
    public void WhenFilePathProvidedThenPromptMentionsGetRoslynDiagnostics()
    {
        var prompt = ValidateAfterEditPrompt.ValidateAfterEdit(filePath: "C:/src/MyFile.cs");

        Assert.Contains("GetRoslynDiagnostics", prompt);
    }

    [Fact]
    public void WhenFilePathProvidedThenPromptMentionsGetFileOutline()
    {
        var prompt = ValidateAfterEditPrompt.ValidateAfterEdit(filePath: "C:/src/MyFile.cs");

        Assert.Contains("GetFileOutline", prompt);
    }

    [Fact]
    public void WhenNullFilePathProvidedThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ValidateAfterEditPrompt.ValidateAfterEdit(filePath: null!));
    }
}
