using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

/// <summary>
/// Navigates to the definition of a symbol by fully-qualified name,
/// without requiring a code snippet or source file that references it.
/// </summary>
[McpServerToolType]
public static class GoToDefinitionTool
{
    [McpServerTool, Description(
        "Go to the definition of a type, method, property, field, or event by its fully-qualified name. " +
        "Examples: 'System.String', 'System.String.Contains', 'MyApp.Services.UserService.GetUser'. " +
        "For members, use 'TypeName.MemberName'. Returns source context or auto-decompiled source. " +
        "Use GoToDefinitionSnippet when you have a code snippet with [| |] markers instead.")]
    public static async Task<string> GoToDefinition(
        [Description("Path to any file in the project (used to determine which project/compilation to search).")]
        string filePath,
        [Description(
            "Fully-qualified symbol name. For types: 'System.String', 'MyApp.Models.User'. " +
            "For members: 'System.String.Contains', 'MyApp.Models.User.Name'. " +
            "For nested types: 'MyApp.Outer+Inner'. Generic types use backtick arity: 'System.Collections.Generic.List`1'.")]
        string symbolName,
        IOutputFormatter fmt,
        [Description("Number of lines of context to show around the definition. Default: 5.")]
        int contextLines = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(symbolName))
                return "Error: symbolName cannot be empty.";

            var errors = new StringBuilder();
            var fileCtx = await ToolHelper.ResolveFileAsync(filePath, errors, cancellationToken);
            if (fileCtx is null)
                return errors.ToString();

            var compilation = await fileCtx.Project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
                return "Error: could not obtain compilation for project.";

            var symbol = ResolveSymbol(compilation, symbolName);
            if (symbol is null)
                return $"Error: symbol '{symbolName}' not found. " +
                       "Use fully-qualified names (e.g. 'System.String', 'MyApp.Models.User.Name'). " +
                       "For generic types use backtick arity (e.g. 'System.Collections.Generic.List`1').";

            return await GoToDefinitionSnippetTool.FormatDefinitionAsync(
                symbol, fileCtx.Project, contextLines, fmt, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GoToDefinition] Unhandled error: {ex}");
            return $"Error: {ex.Message}";
        }
    }

    private static ISymbol? ResolveSymbol(Compilation compilation, string symbolName)
    {
        // Try as a fully-qualified type first
        var type = compilation.GetTypeByMetadataName(symbolName);
        if (type is not null)
            return type;

        // Try splitting off the last segment as a member name
        int lastDot = symbolName.LastIndexOf('.');
        if (lastDot <= 0)
            return null;

        string typePart = symbolName[..lastDot];
        string memberName = symbolName[(lastDot + 1)..];

        type = compilation.GetTypeByMetadataName(typePart);
        if (type is null)
            return null;

        // Look for matching members
        var members = type.GetMembers(memberName);
        if (members.Length == 1)
            return members[0];

        if (members.Length > 1)
        {
            // Prefer non-accessor, non-implicit members
            return members.FirstOrDefault(m => m is not IMethodSymbol { AssociatedSymbol: not null }
                                                && !m.IsImplicitlyDeclared)
                   ?? members[0];
        }

        return null;
    }
}
