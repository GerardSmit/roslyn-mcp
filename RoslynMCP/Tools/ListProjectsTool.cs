using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class ListProjectsTool
{
    [McpServerTool, Description(
        "List all projects in a solution or directory. Discovers .sln files and enumerates " +
        "their projects, or searches a directory for .csproj files.")]
    public static async Task<string> ListProjects(
        [Description(
            "Path to a .sln file, .csproj file, any source file, or a directory to search for projects.")]
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Error: Path cannot be empty.";

            string systemPath = PathHelper.NormalizePath(path);

            // If it's a .sln file, parse it for project entries
            if (systemPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) && File.Exists(systemPath))
                return await FormatSolutionProjectsAsync(systemPath, cancellationToken);

            // If it's a file, find the nearest .sln or list .csproj files in the directory tree
            if (File.Exists(systemPath))
                systemPath = Path.GetDirectoryName(systemPath)!;

            if (!Directory.Exists(systemPath))
                return $"Error: Path '{path}' does not exist.";

            // Search for .sln files in the directory
            var slnFiles = Directory.GetFiles(systemPath, "*.sln", SearchOption.TopDirectoryOnly);
            if (slnFiles.Length > 0)
                return await FormatSolutionProjectsAsync(slnFiles[0], cancellationToken);

            // No .sln found — list all .csproj files
            return FormatDiscoveredProjects(systemPath);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ListProjects] Unhandled error: {ex}");
            return $"Error: {ex.Message}";
        }
    }

    private static async Task<string> FormatSolutionProjectsAsync(
        string slnPath, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var slnDir = Path.GetDirectoryName(slnPath)!;
        sb.AppendLine($"# Solution: {Path.GetFileName(slnPath)}");
        sb.AppendLine();

        var lines = await File.ReadAllLinesAsync(slnPath, cancellationToken);
        var projects = new List<(string Name, string RelativePath, string Type)>();

        foreach (var line in lines)
        {
            // Format: Project("{FAE04EC0-...}") = "Name", "Path\To\Project.csproj", "{GUID}"
            if (!line.StartsWith("Project(", StringComparison.Ordinal)) continue;

            var parts = line.Split('"');
            if (parts.Length < 6) continue;

            string name = parts[3];
            string relativePath = parts[5];

            // Skip solution folders
            if (!relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
                continue;

            string fullPath = Path.GetFullPath(Path.Combine(slnDir, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            string type = DetectProjectType(fullPath);

            projects.Add((name, relativePath.Replace('\\', '/'), type));
        }

        if (projects.Count == 0)
        {
            sb.AppendLine("No projects found in the solution.");
            return sb.ToString();
        }

        sb.AppendLine($"Found **{projects.Count}** project(s):");
        sb.AppendLine();
        sb.AppendLine("| # | Project | Path | Type |");
        sb.AppendLine("|---|---------|------|------|");

        int index = 1;
        foreach (var (name, relativePath, type) in projects)
        {
            sb.AppendLine($"| {index} | {name} | {relativePath} | {type} |");
            index++;
        }

        return sb.ToString();
    }

    private static string FormatDiscoveredProjects(string directory)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Projects in: {directory}");
        sb.AppendLine();

        var csprojFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(directory, f);
                var first = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                return !first.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                       !first.Equals("obj", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f)
            .ToList();

        if (csprojFiles.Count == 0)
        {
            sb.AppendLine("No .csproj files found.");
            return sb.ToString();
        }

        sb.AppendLine($"Found **{csprojFiles.Count}** project(s):");
        sb.AppendLine();
        sb.AppendLine("| # | Project | Path | Type |");
        sb.AppendLine("|---|---------|------|------|");

        int index = 1;
        foreach (var file in csprojFiles)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string relativePath = Path.GetRelativePath(directory, file).Replace('\\', '/');
            string type = DetectProjectType(file);
            sb.AppendLine($"| {index} | {name} | {relativePath} | {type} |");
            index++;
        }

        return sb.ToString();
    }

    private static string DetectProjectType(string csprojPath)
    {
        if (!File.Exists(csprojPath)) return "?";

        try
        {
            var content = File.ReadAllText(csprojPath);
            bool isTest = content.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("NUnit", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("MSTest", StringComparison.OrdinalIgnoreCase);

            bool isExe = content.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase);
            bool isTool = content.Contains("<PackAsTool>true</PackAsTool>", StringComparison.OrdinalIgnoreCase);

            if (isTest) return "Test";
            if (isTool) return "Tool";
            if (isExe) return "Exe";
            return "Library";
        }
        catch
        {
            return "?";
        }
    }
}
