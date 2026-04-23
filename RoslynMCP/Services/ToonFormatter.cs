using System.Text;

namespace RoslynMCP.Services;

/// <summary>
/// Formats tool output as TOON (Token-Optimized Object Notation)
/// for reduced token usage when consumed by LLMs.
/// </summary>
public sealed class ToonFormatter : IOutputFormatter
{
    // Headers are omitted in TOON — data is self-describing
    public void AppendHeader(StringBuilder sb, string text, int level = 1) { }

    public void AppendField(StringBuilder sb, string key, object? value)
    {
        sb.AppendLine($"{key}: {value}");
    }

    // Fields are already line-separated in TOON
    public void AppendSeparator(StringBuilder sb) { }

    public void AppendTable(StringBuilder sb, string name, string[] columns, List<string[]> rows, int? totalCount = null)
    {
        BeginTable(sb, name, columns, totalCount ?? rows.Count);
        foreach (var row in rows)
            AddRow(sb, row);
        EndTable(sb);
    }

    public void BeginTable(StringBuilder sb, string name, string[] columns, int? totalCount = null)
    {
        if (totalCount is not null)
            sb.AppendLine($"{name}[{totalCount}]{{{string.Join(',', columns)}}}:");
        else
            sb.AppendLine($"{name}{{{string.Join(',', columns)}}}:");
    }

    private int _cellIndex;

    public void AddRow(StringBuilder sb, params ReadOnlySpan<string> values)
    {
        BeginRow(sb);
        foreach (var val in values)
            WriteCell(sb, val);
        EndRow(sb);
    }

    public void BeginRow(StringBuilder sb)
    {
        sb.Append("  ");
        _cellIndex = 0;
    }

    public void WriteCell(StringBuilder sb, string value)
    {
        if (_cellIndex++ > 0) sb.Append(',');
        sb.Append(Escape(value));
    }

    public void WriteCell(StringBuilder sb, int value)
    {
        if (_cellIndex++ > 0) sb.Append(',');
        sb.Append(value);
    }

    public void EndRow(StringBuilder sb) => sb.AppendLine();

    public void EndTable(StringBuilder sb) { }

    public void AppendHints(StringBuilder sb, params string[] hints)
    {
        if (hints.Length == 0) return;
        sb.AppendLine($"help[{hints.Length}]:");
        foreach (var hint in hints)
            sb.AppendLine($"  {hint}");
    }

    public void AppendEmpty(StringBuilder sb, string message)
    {
        sb.AppendLine(message);
    }

    public void AppendTruncation(StringBuilder sb, int shown, int total, string paramName = "maxResults")
    {
        if (shown >= total) return;
        sb.AppendLine($"truncated: showing {shown} of {total}");
    }

    public string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        bool needsQuoting = text.Contains(',') ||
                            text.Contains('\n') ||
                            text.Contains('\r') ||
                            text.Contains('"') ||
                            text[0] == ' ' ||
                            text[^1] == ' ';

        if (!needsQuoting)
            return text;

        return '"' + text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r") + '"';
    }
}
