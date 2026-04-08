using Microsoft.CodeAnalysis;

namespace RoslynMCP.Services;

/// <summary>
/// Centralizes file-path normalization used by every MCP tool.
/// </summary>
internal static class PathHelper
{
    /// <summary>
    /// Normalizes a file path by resolving it to a full absolute path.
    /// </summary>
    public static string NormalizePath(string filePath) =>
        Path.GetFullPath(filePath);

    /// <summary>
    /// Resolves a project path, file path, or directory to the containing .csproj file.
    /// Walks up directories from source files. Returns null if not found.
    /// </summary>
    public static string? ResolveCsprojPath(string projectPath)
    {
        var normalized = NormalizePath(projectPath);

        if (normalized.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && File.Exists(normalized))
            return normalized;

        if (File.Exists(normalized))
        {
            var dir = Path.GetDirectoryName(normalized);
            while (dir is not null)
            {
                var csprojs = Directory.GetFiles(dir, "*.csproj");
                if (csprojs.Length >= 1) return csprojs[0];
                dir = Path.GetDirectoryName(dir);
            }
        }

        if (Directory.Exists(normalized))
        {
            var csprojs = Directory.GetFiles(normalized, "*.csproj");
            if (csprojs.Length >= 1) return csprojs[0];
        }

        return null;
    }

    /// <summary>
    /// Parses a severity filter string ("error", "warning", "info", "hidden", "all")
    /// into a DiagnosticSeverity. Returns true if valid; result is null for "all".
    /// </summary>
    public static bool TryParseSeverityFilter(string filter, out DiagnosticSeverity? result)
    {
        switch (filter.ToLowerInvariant())
        {
            case "error": result = DiagnosticSeverity.Error; return true;
            case "warning": result = DiagnosticSeverity.Warning; return true;
            case "info": result = DiagnosticSeverity.Info; return true;
            case "hidden": result = DiagnosticSeverity.Hidden; return true;
            case "all": result = null; return true;
            default: result = null; return false;
        }
    }
}
