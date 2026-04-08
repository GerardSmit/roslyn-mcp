using Microsoft.CodeAnalysis.Text;

namespace RoslynMCP.Services;

/// <summary>
/// Parses an LLM-friendly markup string that uses <c>[|</c> and <c>|]</c> delimiters
/// to mark exactly one target span inside a code snippet.
/// <para>
/// Example: <c>var x = [|Foo|].Bar();</c> → plain text is <c>var x = Foo.Bar();</c>
/// and the marked span covers "Foo" at offset 8..11.
/// </para>
/// </summary>
public sealed class MarkupString
{
    private const string MarkerOpen = "[|";
    private const string MarkerClose = "|]";

    /// <summary>The snippet text with markers removed.</summary>
    public string PlainText { get; }

    /// <summary>Start offset of the marked span inside <see cref="PlainText"/>.</summary>
    public int SpanStart { get; }

    /// <summary>Length of the marked span.</summary>
    public int SpanLength { get; }

    /// <summary>The text inside the markers.</summary>
    public string MarkedText => PlainText.Substring(SpanStart, SpanLength);

    /// <summary>The span inside <see cref="PlainText"/> that was marked.</summary>
    public TextSpan MarkedSpan => new(SpanStart, SpanLength);

    private MarkupString(string plainText, int spanStart, int spanLength)
    {
        PlainText = plainText;
        SpanStart = spanStart;
        SpanLength = spanLength;
    }

    /// <summary>
    /// Parses a markup string that must contain exactly one <c>[| |]</c> pair.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the input is null/empty, has no markers, has nested/multiple markers,
    /// or has an empty marked span.
    /// </exception>
    public static MarkupString Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Markup input cannot be null or empty.", nameof(input));

        int openIndex = input.IndexOf(MarkerOpen, StringComparison.Ordinal);
        if (openIndex < 0)
            throw new ArgumentException(
                $"Markup input must contain exactly one '{MarkerOpen}...{MarkerClose}' pair. No opening marker found.",
                nameof(input));

        // Check for a second opening marker before the close
        int secondOpen = input.IndexOf(MarkerOpen, openIndex + MarkerOpen.Length, StringComparison.Ordinal);

        int closeIndex = input.IndexOf(MarkerClose, openIndex + MarkerOpen.Length, StringComparison.Ordinal);
        if (closeIndex < 0)
            throw new ArgumentException(
                $"Markup input has an opening '{MarkerOpen}' but no matching '{MarkerClose}'.",
                nameof(input));

        if (secondOpen >= 0 && secondOpen < closeIndex)
            throw new ArgumentException(
                $"Markup input contains nested or multiple '{MarkerOpen}' markers. Exactly one pair is required.",
                nameof(input));

        // Check for additional markers after the first pair
        if (secondOpen >= 0)
            throw new ArgumentException(
                $"Markup input contains multiple '{MarkerOpen}...{MarkerClose}' pairs. Exactly one is required.",
                nameof(input));

        int secondClose = input.IndexOf(MarkerClose, closeIndex + MarkerClose.Length, StringComparison.Ordinal);
        if (secondClose >= 0)
            throw new ArgumentException(
                $"Markup input contains extra '{MarkerClose}' markers.",
                nameof(input));

        int markedContentStart = openIndex + MarkerOpen.Length;
        int markedContentLength = closeIndex - markedContentStart;

        if (markedContentLength == 0)
            throw new ArgumentException(
                "Marked span is empty. Place the target identifier between '[|' and '|]'.",
                nameof(input));

        // Build the plain text by stripping markers
        string plainText = string.Concat(
            input.AsSpan(0, openIndex),
            input.AsSpan(markedContentStart, markedContentLength),
            input.AsSpan(closeIndex + MarkerClose.Length));

        return new MarkupString(plainText, openIndex, markedContentLength);
    }

    /// <summary>
    /// Tries to parse markup input, returning <c>false</c> on invalid input
    /// instead of throwing.
    /// </summary>
    public static bool TryParse(string input, out MarkupString? result, out string? error)
    {
        try
        {
            result = Parse(input);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }
}
