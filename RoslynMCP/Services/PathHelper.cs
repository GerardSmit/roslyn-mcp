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
}
