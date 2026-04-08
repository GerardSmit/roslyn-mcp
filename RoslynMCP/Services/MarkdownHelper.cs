namespace RoslynMCP.Services;

internal static class MarkdownHelper
{
    public static string EscapeTableCell(string text) =>
        text.Replace("|", "\\|")
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ");
}
