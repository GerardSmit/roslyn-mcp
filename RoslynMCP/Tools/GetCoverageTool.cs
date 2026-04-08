using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class GetCoverageTool
{
    [McpServerTool, Description(
        "Query code coverage data. Without filters, shows project-wide coverage by type. " +
        "Filter by method, class, or file for detailed views. " +
        "Requires RunCoverage to have been called first.")]
    public static Task<string> GetCoverage(
        [Description("Path to the source file to get coverage for. Leave empty for project overview.")]
        string? filePath = null,
        [Description("Optional method name to filter results (partial match).")]
        string? methodName = null,
        [Description("Optional class name to filter results (partial match).")]
        string? className = null,
        [Description("Show line-by-line coverage detail for uncovered lines. Default: true.")]
        bool showUncoveredLines = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = CoverageService.GetCachedCoverage(out var projectPath, out var cachedAt);
            if (data is null)
                return Task.FromResult(
                    "Error: No coverage data available. Run `RunCoverage` first to collect coverage data.");

            // If a specific method is requested
            if (!string.IsNullOrWhiteSpace(methodName))
            {
                return Task.FromResult(FormatMethodCoverage(methodName, showUncoveredLines, cachedAt));
            }

            // If a specific class is requested
            if (!string.IsNullOrWhiteSpace(className))
            {
                return Task.FromResult(FormatClassCoverage(className, showUncoveredLines, cachedAt));
            }

            // File-level coverage
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult(FormatFileCoverage(filePath, showUncoveredLines, cachedAt));
            }

            // Project-wide overview
            return Task.FromResult(FormatProjectCoverage(data, projectPath, cachedAt));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }

    private static string FormatMethodCoverage(string methodName, bool showUncoveredLines, DateTime cachedAt)
    {
        var methods = CoverageService.FindMethodCoverage(methodName);
        if (methods.Count == 0)
            return $"No coverage data found for method matching '{methodName}'. " +
                   "The method may not be covered by any test, or run `RunCoverage` to update data.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Method Coverage: {methodName}");
        sb.AppendLine($"_Coverage data from {cachedAt:yyyy-MM-dd HH:mm:ss} UTC_");
        sb.AppendLine();

        foreach (var method in methods)
        {
            sb.AppendLine($"## {method.FullName}");
            sb.AppendLine($"**File**: {Path.GetFileName(method.FilePath)}");
            sb.AppendLine($"**Line Coverage**: {method.LineCoverageRate:P1} ({method.CoveredLines}/{method.TotalLines})");
            if (method.TotalBranches > 0)
                sb.AppendLine($"**Branch Coverage**: {method.BranchCoverageRate:P1} ({method.CoveredBranches}/{method.TotalBranches})");
            sb.AppendLine();

            if (showUncoveredLines && method.Lines.Count > 0)
            {
                // Show branch details for partially covered branches
                var partialBranches = method.Lines
                    .Where(l => l.IsBranch && l.ConditionCoverage is not null && !l.ConditionCoverage.StartsWith("100%"))
                    .ToList();
                if (partialBranches.Count > 0)
                {
                    sb.AppendLine("**Partial branches** (not all paths tested):");
                    foreach (var line in partialBranches)
                    {
                        sb.AppendLine($"  - Line {line.LineNumber}: {line.ConditionCoverage}");
                    }
                    sb.AppendLine();
                }

                var uncovered = method.Lines.Where(l => l.Hits == 0).ToList();
                if (uncovered.Count > 0)
                {
                    sb.AppendLine("**Uncovered lines:**");
                    foreach (var line in uncovered)
                    {
                        sb.AppendLine($"  - Line {line.LineNumber}");
                    }
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("All lines covered. ✅");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    private static string FormatClassCoverage(string className, bool showUncoveredLines, DateTime cachedAt)
    {
        var classes = CoverageService.FindClassCoverage(className);
        if (classes.Count == 0)
            return $"No coverage data found for class matching '{className}'. " +
                   "Run `RunCoverage` to update data.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Class Coverage: {className}");
        sb.AppendLine($"_Coverage data from {cachedAt:yyyy-MM-dd HH:mm:ss} UTC_");
        sb.AppendLine();

        foreach (var cls in classes)
        {
            sb.AppendLine($"## {cls.FullName}");
            sb.AppendLine($"**File**: {Path.GetFileName(cls.FilePath)}");
            sb.AppendLine($"**Line Coverage**: {cls.LineCoverageRate:P1}");
            sb.AppendLine($"**Branch Coverage**: {cls.BranchCoverageRate:P1}");
            sb.AppendLine();

            if (cls.Methods.Count > 0)
            {
                sb.AppendLine("| Method | Lines | Branches |");
                sb.AppendLine("|--------|-------|----------|");
                foreach (var method in cls.Methods)
                {
                    string lineIcon = method.LineCoverageRate >= 1.0 ? "✅" :
                                      method.LineCoverageRate >= 0.5 ? "⚠️" : "❌";
                    string branchCol = method.TotalBranches > 0
                        ? $"{method.BranchCoverageRate:P0} ({method.CoveredBranches}/{method.TotalBranches})"
                        : "—";
                    sb.AppendLine(
                        $"| {lineIcon} {method.Name} | {method.LineCoverageRate:P0} ({method.CoveredLines}/{method.TotalLines}) | {branchCol} |");
                }
                sb.AppendLine();
            }

            if (showUncoveredLines)
            {
                var uncovered = cls.Lines.Where(l => l.Hits == 0).ToList();
                if (uncovered.Count > 0)
                {
                    sb.AppendLine($"**Uncovered lines** ({uncovered.Count}):");
                    foreach (var line in uncovered.Take(20))
                    {
                        sb.AppendLine($"  - Line {line.LineNumber}");
                    }
                    if (uncovered.Count > 20)
                        sb.AppendLine($"  _... and {uncovered.Count - 20} more_");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    private static string FormatFileCoverage(string filePath, bool showUncoveredLines, DateTime cachedAt)
    {
        string normalized = PathHelper.NormalizePath(filePath);
        var fileCov = CoverageService.GetFileCoverage(normalized);
        if (fileCov is null)
            return $"No coverage data found for file '{Path.GetFileName(filePath)}'. " +
                   "The file may not be covered by any test, or run `RunCoverage` to update data.";

        var sb = new StringBuilder();
        sb.AppendLine($"# File Coverage: {Path.GetFileName(filePath)}");
        sb.AppendLine($"_Coverage data from {cachedAt:yyyy-MM-dd HH:mm:ss} UTC_");
        sb.AppendLine();

        int totalLines = fileCov.Lines.Count;
        int coveredLines = fileCov.Lines.Count(kv => kv.Value.Hits > 0);
        double rate = totalLines > 0 ? (double)coveredLines / totalLines : 1.0;
        int totalBranches = fileCov.Methods.Sum(m => m.TotalBranches);
        int coveredBranches = fileCov.Methods.Sum(m => m.CoveredBranches);

        sb.AppendLine($"**Line Coverage**: {rate:P1} ({coveredLines}/{totalLines})");
        if (totalBranches > 0)
        {
            double branchRate = (double)coveredBranches / totalBranches;
            sb.AppendLine($"**Branch Coverage**: {branchRate:P1} ({coveredBranches}/{totalBranches})");
        }
        sb.AppendLine($"**Classes**: {fileCov.Classes.Count} | **Methods**: {fileCov.Methods.Count}");
        sb.AppendLine();

        if (fileCov.Methods.Count > 0)
        {
            sb.AppendLine("## Methods");
            sb.AppendLine();
            sb.AppendLine("| Method | Lines | Branches |");
            sb.AppendLine("|--------|-------|----------|");

            foreach (var method in fileCov.Methods.OrderBy(m => m.LineCoverageRate))
            {
                string icon = method.LineCoverageRate >= 1.0 ? "✅" :
                              method.LineCoverageRate >= 0.5 ? "⚠️" : "❌";
                string branchCol = method.TotalBranches > 0
                    ? $"{method.BranchCoverageRate:P0} ({method.CoveredBranches}/{method.TotalBranches})"
                    : "—";
                sb.AppendLine(
                    $"| {icon} {method.Name} | {method.LineCoverageRate:P0} ({method.CoveredLines}/{method.TotalLines}) | {branchCol} |");
            }
            sb.AppendLine();
        }

        if (showUncoveredLines)
        {
            // Show partial branches first
            var partialBranches = fileCov.Lines.Values
                .Where(l => l.IsBranch && l.ConditionCoverage is not null && !l.ConditionCoverage.StartsWith("100%"))
                .OrderBy(l => l.LineNumber)
                .ToList();
            if (partialBranches.Count > 0)
            {
                sb.AppendLine("## Partial Branches");
                sb.AppendLine();
                foreach (var line in partialBranches.Take(20))
                {
                    sb.AppendLine($"  - Line {line.LineNumber}: {line.ConditionCoverage}");
                }
                if (partialBranches.Count > 20)
                    sb.AppendLine($"  _... and {partialBranches.Count - 20} more_");
                sb.AppendLine();
            }

            var uncovered = fileCov.Lines
                .Where(kv => kv.Value.Hits == 0)
                .OrderBy(kv => kv.Key)
                .ToList();

            if (uncovered.Count > 0)
            {
                sb.AppendLine($"## Uncovered Lines ({uncovered.Count})");
                sb.AppendLine();

                // Group consecutive lines into ranges for readability
                var ranges = GetLineRanges(uncovered.Select(kv => kv.Key).ToList());
                foreach (var range in ranges.Take(30))
                {
                    if (range.Start == range.End)
                        sb.AppendLine($"  - Line {range.Start}");
                    else
                        sb.AppendLine($"  - Lines {range.Start}-{range.End}");
                }

                if (ranges.Count > 30)
                    sb.AppendLine($"  _... and more uncovered ranges_");
            }
        }

        return sb.ToString();
    }

    private static List<(int Start, int End)> GetLineRanges(List<int> lines)
    {
        if (lines.Count == 0) return [];

        var ranges = new List<(int Start, int End)>();
        int start = lines[0];
        int end = lines[0];

        for (int i = 1; i < lines.Count; i++)
        {
            if (lines[i] == end + 1)
            {
                end = lines[i];
            }
            else
            {
                ranges.Add((start, end));
                start = lines[i];
                end = lines[i];
            }
        }
        ranges.Add((start, end));
        return ranges;
    }

    private static string FormatProjectCoverage(CoverageData data, string? projectPath, DateTime cachedAt)
    {
        var sb = new StringBuilder();
        string projectName = projectPath is not null ? Path.GetFileNameWithoutExtension(projectPath) : "Project";
        sb.AppendLine($"# Coverage: {projectName}");
        sb.AppendLine($"_Coverage data from {cachedAt:yyyy-MM-dd HH:mm:ss} UTC_");
        sb.AppendLine();

        // Overall summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"**Line Coverage**: {data.LineCoverageRate:P1} ({data.LinesCovered}/{data.LinesValid})");
        sb.AppendLine($"**Branch Coverage**: {data.BranchCoverageRate:P1} ({data.BranchesCovered}/{data.BranchesValid})");
        sb.AppendLine($"**Files**: {data.Files.Count}");

        int totalClasses = data.Files.Values.Sum(f => f.Classes.Count);
        int totalMethods = data.Files.Values.Sum(f => f.Methods.Count);
        sb.AppendLine($"**Classes**: {totalClasses} | **Methods**: {totalMethods}");
        sb.AppendLine();

        // Per-class coverage table, sorted by coverage (worst first)
        var allClasses = data.Files.Values
            .SelectMany(f => f.Classes)
            .OrderBy(c => c.LineCoverageRate)
            .ToList();

        if (allClasses.Count > 0)
        {
            sb.AppendLine("## Coverage by Type");
            sb.AppendLine();
            sb.AppendLine("| Type | Lines | Branches | Methods |");
            sb.AppendLine("|------|-------|----------|---------|");

            foreach (var cls in allClasses)
            {
                string icon = cls.LineCoverageRate >= 1.0 ? "✅" :
                              cls.LineCoverageRate >= 0.5 ? "⚠️" : "❌";

                int clsTotalBranches = cls.Methods.Sum(m => m.TotalBranches);
                int clsCoveredBranches = cls.Methods.Sum(m => m.CoveredBranches);
                string branchCol = clsTotalBranches > 0
                    ? $"{(double)clsCoveredBranches / clsTotalBranches:P0} ({clsCoveredBranches}/{clsTotalBranches})"
                    : "—";

                int clsTotalLines = cls.Lines.Count;
                int clsCoveredLines = cls.Lines.Count(l => l.Hits > 0);
                string lineCol = clsTotalLines > 0
                    ? $"{(double)clsCoveredLines / clsTotalLines:P0} ({clsCoveredLines}/{clsTotalLines})"
                    : "—";

                int coveredMethods = cls.Methods.Count(m => m.LineCoverageRate >= 1.0);
                string methodCol = $"{coveredMethods}/{cls.Methods.Count}";

                sb.AppendLine($"| {icon} {cls.FullName} | {lineCol} | {branchCol} | {methodCol} |");
            }
            sb.AppendLine();
        }

        // Methods with lowest coverage (actionable)
        var lowCoverageMethods = data.Files.Values
            .SelectMany(f => f.Methods)
            .Where(m => m.LineCoverageRate < 1.0 && m.TotalLines > 0)
            .OrderBy(m => m.LineCoverageRate)
            .Take(10)
            .ToList();

        if (lowCoverageMethods.Count > 0)
        {
            sb.AppendLine("## Lowest Coverage Methods");
            sb.AppendLine();
            sb.AppendLine("| Method | Lines | Branches | File |");
            sb.AppendLine("|--------|-------|----------|------|");

            foreach (var method in lowCoverageMethods)
            {
                string branchCol = method.TotalBranches > 0
                    ? $"{method.BranchCoverageRate:P0} ({method.CoveredBranches}/{method.TotalBranches})"
                    : "—";
                sb.AppendLine(
                    $"| ❌ {method.FullName} | {method.LineCoverageRate:P0} ({method.CoveredLines}/{method.TotalLines}) | {branchCol} | {Path.GetFileName(method.FilePath)} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
