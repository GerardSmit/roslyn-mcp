using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class FindUsagesTool
{
    private const int MaxCodeContextReferences = 3;

    /// <summary>
    /// Finds all references to a symbol identified by a markup snippet with
    /// <c>[| |]</c> delimiters around the target symbol.
    /// </summary>
    [McpServerTool, Description(
        "Find all references to a symbol. Provide a code snippet from the file with " +
        "[| |] delimiters around the target symbol, e.g. 'var x = [|Foo|].Bar();'. " +
        "Whitespace differences between the snippet and the file are tolerated.")]
    public static async Task<string> FindUsages(
        [Description("Path to the file.")] string filePath,
        [Description(
            "Code snippet with [| |] markers around the target symbol, " +
            "e.g. 'var x = [|Foo|].Bar();'.")]
        string markupSnippet,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return "Error: File path cannot be empty.";

            if (string.IsNullOrWhiteSpace(markupSnippet))
                return "Error: markupSnippet cannot be empty.";

            string systemPath = PathHelper.NormalizePath(filePath);

            if (!File.Exists(systemPath))
                return $"Error: File {systemPath} does not exist.";

            string? projectPath = await WorkspaceService.FindContainingProjectAsync(systemPath, cancellationToken);
            if (string.IsNullOrEmpty(projectPath))
                return "Error: Couldn't find a project containing this file.";

            var (workspace, project) = await WorkspaceService.GetOrOpenProjectAsync(
                projectPath, targetFilePath: systemPath, cancellationToken: cancellationToken);
            var document = WorkspaceService.FindDocumentInProject(project, systemPath);

            if (document == null) return "Error: File not found in project.";

            var (symbol, searchSummary, error) = await ResolveViaMarkupAsync(
                document, workspace, markupSnippet, cancellationToken);
            if (error != null) return error;

            var references = await SymbolFinder.FindReferencesAsync(
                symbol!, project.Solution, cancellationToken);

            return await FormatResultsAsync(
                symbol!, references, systemPath, searchSummary!, projectPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FindUsages] Unhandled error: {ex}");
            return $"Error finding usages: {ex.Message}";
        }
    }

    /// <summary>
    /// Resolves a symbol via the markup snippet path using <see cref="MarkupSymbolResolver"/>.
    /// </summary>
    private static async Task<(ISymbol? Symbol, string? Description, string? Error)> ResolveViaMarkupAsync(
        Document document, Workspace workspace, string markupSnippet,
        CancellationToken cancellationToken)
    {
        if (!MarkupString.TryParse(markupSnippet, out var markup, out string? parseError))
            return (null, null, $"Error: Invalid markup snippet. {parseError}");

        var result = await MarkupSymbolResolver.ResolveAsync(
            document, workspace, markup!, cancellationToken);

        return result.Kind switch
        {
            MarkupResolutionResult.ResultKind.Resolved =>
                (result.Symbol, $"Markup target: `{markup!.MarkedText}`", null),

            MarkupResolutionResult.ResultKind.NoSymbol =>
                (null, null, $"No symbol found at markup target. {result.Message}"),

            MarkupResolutionResult.ResultKind.Ambiguous =>
                (null, null, $"Ambiguous markup match. {result.Message}"),

            MarkupResolutionResult.ResultKind.NoMatch =>
                (null, null, $"Snippet not found in file. {result.Message}"),

            _ => (null, null, $"Error: {result.Message}"),
        };
    }

    /// <summary>
    /// Formats the symbol and reference data into the markdown report returned to the caller.
    /// </summary>
    private static async Task<string> FormatResultsAsync(
        ISymbol symbol,
        IEnumerable<ReferencedSymbol> references,
        string filePath,
        string searchSummary,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var refList = references.ToList();
        var results = new StringBuilder();

        results.AppendLine("# Symbol Usage Analysis");
        results.AppendLine();
        results.AppendLine("## Search Information");
        results.AppendLine($"- **File**: {filePath}");
        results.AppendLine($"- **Position**: {searchSummary}");
        results.AppendLine($"- **Project**: {Path.GetFileName(projectPath)}");
        results.AppendLine();

        AppendSymbolDetails(results, symbol);

        int totalLocations = refList.Sum(r => r.Locations.Count());

        results.AppendLine("## References");
        results.AppendLine(
            $"Found {totalLocations} references in {refList.Count} locations.");
        results.AppendLine();

        int referenceCount = 1;
        foreach (var reference in refList)
        {
            results.AppendLine($"### Reference Definition: {reference.Definition.ToDisplayString()}");

            foreach (var location in reference.Locations)
            {
                await AppendReferenceLocationAsync(
                    results,
                    location,
                    referenceCount,
                    includeCodeContext: referenceCount <= MaxCodeContextReferences,
                    cancellationToken);
                referenceCount++;
            }
        }

        if (totalLocations > MaxCodeContextReferences)
        {
            results.AppendLine(
                $"_Code context shown for the first {MaxCodeContextReferences} references; " +
                $"omitted for the remaining {totalLocations - MaxCodeContextReferences}._");
            results.AppendLine();
        }

        results.AppendLine("## Summary");
        results.AppendLine(
            $"Symbol `{symbol.Name}` of type `{symbol.Kind}` has " +
            $"{totalLocations} references across {refList.Count} locations.");

        return results.ToString();
    }

    private static void AppendSymbolDetails(StringBuilder results, ISymbol symbol)
    {
        results.AppendLine("## Symbol Details");
        results.AppendLine($"- **Name**: {symbol.Name}");
        results.AppendLine($"- **Kind**: {symbol.Kind}");
        results.AppendLine($"- **Full Name**: {symbol.ToDisplayString()}");

        if (symbol.ContainingType != null)
            results.AppendLine($"- **Containing Type**: {symbol.ContainingType.ToDisplayString()}");

        if (symbol.ContainingNamespace is { IsGlobalNamespace: false })
            results.AppendLine($"- **Namespace**: {symbol.ContainingNamespace.ToDisplayString()}");

        results.AppendLine($"- **Accessibility**: {symbol.DeclaredAccessibility}");

        switch (symbol)
        {
            case IMethodSymbol method:
                results.AppendLine($"- **Return Type**: {method.ReturnType.ToDisplayString()}");
                results.AppendLine($"- **Is Extension Method**: {method.IsExtensionMethod}");
                results.AppendLine($"- **Parameter Count**: {method.Parameters.Length}");
                break;
            case IPropertySymbol property:
                results.AppendLine($"- **Property Type**: {property.Type.ToDisplayString()}");
                results.AppendLine($"- **Has Getter**: {property.GetMethod != null}");
                results.AppendLine($"- **Has Setter**: {property.SetMethod != null}");
                break;
            case IFieldSymbol field:
                results.AppendLine($"- **Field Type**: {field.Type.ToDisplayString()}");
                results.AppendLine($"- **Is Const**: {field.IsConst}");
                results.AppendLine($"- **Is Static**: {field.IsStatic}");
                break;
            case IEventSymbol evt:
                results.AppendLine($"- **Event Type**: {evt.Type.ToDisplayString()}");
                break;
            case IParameterSymbol param:
                results.AppendLine($"- **Parameter Type**: {param.Type.ToDisplayString()}");
                results.AppendLine($"- **Is Optional**: {param.IsOptional}");
                break;
            case ILocalSymbol local:
                results.AppendLine($"- **Local Type**: {local.Type.ToDisplayString()}");
                results.AppendLine($"- **Is Const**: {local.IsConst}");
                break;
        }

        results.AppendLine();
    }

    private static async Task AppendReferenceLocationAsync(
        StringBuilder results, ReferenceLocation location, int referenceCount,
        bool includeCodeContext,
        CancellationToken cancellationToken)
    {
        var linePosition = location.Location.GetLineSpan();
        int refLine = linePosition.StartLinePosition.Line + 1;
        int refColumn = linePosition.StartLinePosition.Character + 1;
        string locationPath = location.Document.FilePath ?? linePosition.Path;
        string formattedLocation = $"{locationPath}:{refLine}:{refColumn}";

        results.AppendLine($"#### Reference {referenceCount}: {formattedLocation}");

        if (!includeCodeContext)
        {
            results.AppendLine();
            return;
        }

        try
        {
            var refSourceText = await location.Document.GetTextAsync(cancellationToken);
            int contextStart = Math.Max(0, refLine - 3);
            int contextEnd = Math.Min(refSourceText.Lines.Count - 1, refLine + 1);

            results.AppendLine("```csharp");

            for (int i = contextStart; i <= contextEnd; i++)
            {
                string lineText = refSourceText.Lines[i].ToString();
                int lineNumber = i + 1;

                results.AppendLine(lineNumber == refLine
                    ? $"{lineNumber}: > {lineText}"
                    : $"{lineNumber}:   {lineText}");
            }

            results.AppendLine("```");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            results.AppendLine($"Code context unavailable: {ex.Message}");
        }

        results.AppendLine();
    }
}
