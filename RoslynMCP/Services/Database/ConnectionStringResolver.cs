using System.Text.Json;
using System.Xml;
using System.Xml.XPath;

namespace RoslynMCP.Services.Database;

/// <summary>
/// Expands config-file-backed connection strings.
/// <br/>Supported forms:
/// <list type="bullet">
/// <item><c>xml:&lt;path&gt;#&lt;name&gt;</c> — look up <c>configuration/connectionStrings/add[@name=name]/@connectionString</c>.</item>
/// <item><c>xml:&lt;path&gt;#&lt;xpath&gt;</c> — arbitrary XPath starting with <c>/</c> or <c>//</c>. Returns attribute value or element inner text.</item>
/// <item><c>json:&lt;path&gt;#&lt;name&gt;</c> — look up <c>$.ConnectionStrings.&lt;name&gt;</c>.</item>
/// <item><c>json:&lt;path&gt;#$.a.b</c> — dotted JSON path. Returns the string value at that location.</item>
/// <item>Anything else is returned unchanged (raw connection string).</item>
/// </list>
/// </summary>
public static class ConnectionStringResolver
{
    public static string Resolve(string value)
    {
        if (value.StartsWith("xml:", StringComparison.OrdinalIgnoreCase))
            return ResolveXml(value[4..]);
        if (value.StartsWith("json:", StringComparison.OrdinalIgnoreCase))
            return ResolveJson(value[5..]);
        return value;
    }

    private static (string path, string query) SplitOnHash(string value, string kind)
    {
        var hash = value.IndexOf('#');
        if (hash <= 0)
            throw new ArgumentException($"{kind} reference must be in the form '{kind.ToLowerInvariant()}:<path>#<query>'. Got '{value}'.");
        return (value[..hash], value[(hash + 1)..]);
    }

    private static string ResolveXml(string value)
    {
        var (path, query) = SplitOnHash(value, "XML");
        path = PathVariableExpander.ResolveFilePath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"XML config file not found: {path}");

        var doc = new XmlDocument();
        doc.Load(path);

        string xpath = (query.StartsWith('/'))
            ? query
            : $"/configuration/connectionStrings/add[@name='{EscapeXPathLiteral(query)}']/@connectionString";

        var node = doc.CreateNavigator()?.SelectSingleNode(xpath);
        if (node is null)
            throw new InvalidOperationException($"XPath '{xpath}' returned no match in '{path}'.");

        var result = node.Value;
        if (string.IsNullOrEmpty(result))
            throw new InvalidOperationException($"XPath '{xpath}' matched but value was empty in '{path}'.");
        return result;
    }

    private static string ResolveJson(string value)
    {
        var (path, query) = SplitOnHash(value, "JSON");
        path = PathVariableExpander.ResolveFilePath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"JSON config file not found: {path}");

        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

        string[] segments;
        if (query.StartsWith("$.", StringComparison.Ordinal))
            segments = query[2..].Split('.');
        else if (query == "$")
            segments = [];
        else
            segments = ["ConnectionStrings", query];

        var el = doc.RootElement;
        foreach (var seg in segments)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(seg, out var next))
                throw new InvalidOperationException($"JSON path '{query}' did not resolve in '{path}' (segment '{seg}').");
            el = next;
        }

        if (el.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"JSON path '{query}' in '{path}' is not a string value (kind: {el.ValueKind}).");

        return el.GetString() ?? throw new InvalidOperationException($"JSON path '{query}' in '{path}' resolved to null.");
    }

    private static string EscapeXPathLiteral(string value)
    {
        // XPath 1.0 has no escape — but connection-string names don't normally contain single quotes.
        if (value.Contains('\''))
            throw new ArgumentException($"Connection-string name '{value}' contains a single quote; use a full XPath query instead.");
        return value;
    }
}
