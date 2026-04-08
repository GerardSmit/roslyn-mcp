using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

/// <summary>
/// Returns diagnostics across all files in a project, grouped by severity and file.
/// </summary>
[McpServerToolType]
public static class GetProjectDiagnosticsTool
{
    [McpServerTool, Description(
        "Get Roslyn diagnostics (errors, warnings) across all files in a C# project. " +
        "Returns a summary with counts per severity and a table of diagnostics. " +
        "Useful for project health checks and finding all compilation errors at once.")]
    public static async Task<string> GetProjectDiagnostics(
        [Description("Path to the .csproj file or any source file in the project.")]
        string projectPath,
        [Description("Severity filter: error, warning, info, or all (default: error).")] string severityFilter = "error",
        [Description("Maximum number of diagnostics to return. Default: 50.")]
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return "Error: projectPath cannot be empty.";

            string systemPath = PathHelper.NormalizePath(projectPath);

            string csprojPath;
            if (systemPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && File.Exists(systemPath))
            {
                csprojPath = systemPath;
            }
            else
            {
                var found = RunTestsTool.ResolveCsprojPath(systemPath)
                    ?? await WorkspaceService.FindContainingProjectAsync(systemPath, cancellationToken);
                if (string.IsNullOrEmpty(found))
                    return "Error: Couldn't find a project for this path.";
                csprojPath = found;
            }

            if (!TryParseSeverityFilter(severityFilter, out DiagnosticSeverity? filter))
                return $"Error: Invalid severity filter '{severityFilter}'. Use: error, warning, info, or all.";

            var (_, project) = await WorkspaceService.GetOrOpenProjectAsync(
                csprojPath, cancellationToken: cancellationToken);

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
                return "Error: Unable to produce compilation for project.";

            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Location.IsInSource)
                .Where(d => d.Severity != DiagnosticSeverity.Hidden)
                .ToList();

            if (filter is not null)
                diagnostics = diagnostics.Where(d => d.Severity == filter.Value).ToList();

            return FormatProjectDiagnostics(diagnostics, project, csprojPath, maxResults);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GetProjectDiagnostics] Unhandled error: {ex}");
            return $"Error: {ex.Message}";
        }
    }

    private static bool TryParseSeverityFilter(string filter, out DiagnosticSeverity? result)
    {
        switch (filter.ToLowerInvariant())
        {
            case "error": result = DiagnosticSeverity.Error; return true;
            case "warning": result = DiagnosticSeverity.Warning; return true;
            case "info": result = DiagnosticSeverity.Info; return true;
            case "all": result = null; return true;
            default: result = null; return false;
        }
    }

    private static string FormatProjectDiagnostics(
        List<Diagnostic> diagnostics, Project project, string projectPath, int maxResults)
    {
        var sb = new StringBuilder();
        string? projectDir = Path.GetDirectoryName(projectPath);

        sb.AppendLine($"# Project Diagnostics: {project.Name}");
        sb.AppendLine();

        int errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        int warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        int info = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);

        sb.AppendLine($"**Errors**: {errors} | **Warnings**: {warnings} | **Info**: {info}");
        sb.AppendLine();

        if (diagnostics.Count == 0)
        {
            sb.AppendLine("No diagnostics found. Project compiles cleanly.");
            return sb.ToString();
        }

        // Group by file for summary
        var byFile = diagnostics
            .GroupBy(d => d.Location.SourceTree?.FilePath ?? "unknown")
            .OrderByDescending(g => g.Count(d => d.Severity == DiagnosticSeverity.Error))
            .ThenByDescending(g => g.Count())
            .ToList();

        sb.AppendLine($"**Files with issues**: {byFile.Count}");
        sb.AppendLine();

        // Detail table (capped by maxResults)
        var ordered = diagnostics
            .OrderBy(d => d.Severity)
            .ThenBy(d => d.Location.SourceTree?.FilePath)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .Take(maxResults)
            .ToList();

        sb.AppendLine("| Severity | ID | File | Line | Message |");
        sb.AppendLine("|----------|------|------|------|---------|");

        foreach (var d in ordered)
        {
            var span = d.Location.GetLineSpan();
            int line = span.StartLinePosition.Line + 1;
            string severity = d.Severity switch
            {
                DiagnosticSeverity.Error => "Error",
                DiagnosticSeverity.Warning => "Warning",
                DiagnosticSeverity.Info => "Info",
                _ => d.Severity.ToString(),
            };
            string file = projectDir is not null
                ? Path.GetRelativePath(projectDir, span.Path)
                : Path.GetFileName(span.Path);

            sb.AppendLine(
                $"| {severity} | {d.Id} | {MarkdownHelper.EscapeTableCell(file)} | {line} | {MarkdownHelper.EscapeTableCell(d.GetMessage())} |");
        }

        if (diagnostics.Count > maxResults)
        {
            sb.AppendLine();
            sb.AppendLine($"_Showing first {maxResults} of {diagnostics.Count} diagnostics. Use `maxResults` to see more._");
        }

        return sb.ToString();
    }
}
