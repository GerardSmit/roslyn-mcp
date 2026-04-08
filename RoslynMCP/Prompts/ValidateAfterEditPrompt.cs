using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RoslynMCP.Prompts;

/// <summary>
/// Generates a structured validation workflow prompt for use after editing C# files.
/// Guides the LLM through running Roslyn validation, reviewing diagnostics, and
/// deciding on next steps.
/// </summary>
[McpServerPromptType]
public static class ValidateAfterEditPrompt
{
    /// <summary>
    /// Produces a user-role prompt instructing the model to validate the specified file
    /// using the available Roslyn tools and handle any diagnostics found.
    /// </summary>
    [McpServerPrompt(Name = "validate-after-edit"), Description(
        "Workflow prompt for validating a C# file after editing. " +
        "Produces step-by-step instructions to run Roslyn validation, " +
        "review diagnostics, and fix issues.")]
    public static string ValidateAfterEdit(
        [Description("Path to the C# file that was edited.")] string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        return $$"""
            I just edited the C# file: {{filePath}}

            Please validate my changes by following these steps:

            1. **Run diagnostics** — Call the `GetRoslynDiagnostics` tool on `{{filePath}}` with `runAnalyzers=true` to get a compact table of errors, warnings, and info diagnostics.

            2. **Review results**:
               - If there are **errors**, show me each one with its line number and message, then suggest specific fixes.
               - If there are only **warnings**, list them and recommend whether they should be addressed or are acceptable.
               - If the file is **clean**, confirm that the edit introduced no new issues.

            3. **Check the outline** — Call `GetFileOutline` on `{{filePath}}` to verify the file's structure is intact (no accidentally deleted members, broken nesting, etc.).

            4. **Summarize** — Provide a brief pass/fail verdict with the diagnostic counts.
            """;
    }
}
