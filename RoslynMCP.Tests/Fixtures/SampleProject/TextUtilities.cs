namespace SampleProject;

public class TextUtilities
{
    /// <summary>
    /// Uppercases the first character of the provided text.
    /// </summary>
    public string UppercaseFirstCharacter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    public string NormalizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return UppercaseFirstCharacter(value.Trim());
    }
}
