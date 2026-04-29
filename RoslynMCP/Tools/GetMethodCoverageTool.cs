using System.Buffers;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class GetMethodCoverageTool
{
    [McpServerTool, Description(
        "Get per-line coverage detail for a specific method. Shows every executable line with hit count " +
        "and source code. Lines marked with ! have partial branch coverage (condition executed but not " +
        "all paths tested). Requires RunCoverage to have been called first.")]
    public static async Task<string> GetMethodCoverage(
        IOutputFormatter fmt,
        [Description("Method name to look up (partial match).")]
        string methodName,
        [Description("Optional file path to narrow results when multiple methods match (partial match).")]
        string? filePath = null,
        [Description("Optional class name to narrow results when multiple methods match (partial match).")]
        string? className = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = CoverageService.GetCachedCoverage(out _, out var cachedAt);
            if (data is null)
                return "Error: No coverage data available. Run `RunCoverage` first to collect coverage data.";

            var methods = CoverageService.FindMethodCoverage(methodName);

            if (!string.IsNullOrWhiteSpace(filePath))
                methods = methods
                    .Where(m => m.FilePath.Contains(filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (!string.IsNullOrWhiteSpace(className))
                methods = methods
                    .Where(m => m.FullName.Contains(className, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (methods.Count == 0)
                return $"No coverage data found for method matching '{methodName}'. " +
                       "The method may not be covered by any test, or run `RunCoverage` to update data.";

            var sb = new StringBuilder();
            fmt.AppendHeader(sb, $"Per-Line Coverage: {methodName}");
            fmt.AppendField(sb, "Coverage data from", $"{cachedAt:yyyy-MM-dd HH:mm:ss} UTC");
            fmt.AppendSeparator(sb);

            foreach (var method in methods)
            {
                string[] sourceLines = File.Exists(method.FilePath)
                    ? File.ReadAllLines(method.FilePath)
                    : [];

                bool methodChanged = false;
                bool hashUnchanged = false;
                if (!string.IsNullOrEmpty(method.SourceHash) && sourceLines.Length > 0 && method.Lines.Count > 0)
                {
                    // Use raw coverage line numbers (no offset) — ComputeSourceHashes stored the
                    // hash against these same lines. If raw-line hash matches, content at method
                    // is unchanged regardless of any Coverlet/Roslyn first-line disagreement.
                    int hashMinLine = method.Lines.Min(l => l.LineNumber);
                    int hashMaxLine = method.Lines.Max(l => l.LineNumber);
                    int rangeStart = Math.Max(0, hashMinLine - 1);
                    int rangeEnd = Math.Min(sourceLines.Length - 1, hashMaxLine - 1);

                    int maxBytes = 1;
                    for (int i = rangeStart; i <= rangeEnd; i++)
                        maxBytes = Math.Max(maxBytes, Encoding.UTF8.GetMaxByteCount(sourceLines[i].Length));

                    byte[] hashBuf = ArrayPool<byte>.Shared.Rent(maxBytes);
                    try
                    {
                        bool match = CoverageService.HashMethodLines(sourceLines, hashMinLine, hashMaxLine, hashBuf)
                            == method.SourceHash;
                        hashUnchanged = match;
                        methodChanged = !match;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(hashBuf);
                    }
                }

                // Only compute Roslyn offset when raw-line hash mismatches — otherwise Coverlet's
                // recorded line is authoritative and any Roslyn disagreement is harmless noise.
                int offset = hashUnchanged ? 0 : await ComputeLineOffsetAsync(method, cancellationToken);

                fmt.AppendHeader(sb, method.FullName, 2);
                fmt.AppendField(sb, "File", Path.GetFileName(method.FilePath));
                fmt.AppendField(sb, "Line Coverage", $"{method.LineCoverageRate:P1} ({method.CoveredLines}/{method.TotalLines})");
                if (method.TotalBranches > 0)
                    fmt.AppendField(sb, "Branch Coverage", $"{method.BranchCoverageRate:P1} ({method.CoveredBranches}/{method.TotalBranches})");

                if (methodChanged)
                    sb.AppendLine("⚠️ Method source has changed since coverage was collected — line numbers may be incorrect.");
                else if (offset != 0)
                    sb.AppendLine($"⚠️ File modified since coverage was collected — line numbers shifted by {offset:+0;-0;0}.");

                fmt.AppendSeparator(sb);

                if (method.Lines.Count == 0)
                {
                    sb.AppendLine("No line data available for this method.");
                    sb.AppendLine();
                    continue;
                }

                bool hasPartialBranches = method.Lines
                    .Any(l => l.IsBranch && l.ConditionCoverage is not null && !l.IsFullBranchCoverage);

                if (hasPartialBranches)
                    sb.AppendLine("! = partial branch coverage (some conditions not exercised)");

                int maxHits = method.Lines.Max(l => l.Hits);
                int hitsWidth = Math.Max(maxHits.ToString().Length, 1);
                int maxDisplayLine = method.Lines.Max(l => l.LineNumber + offset);
                int lineNumWidth = Math.Max(maxDisplayLine.ToString().Length, 2);

                sb.AppendLine();
                foreach (var line in method.Lines.OrderBy(l => l.LineNumber))
                {
                    int displayLine = line.LineNumber + offset;
                    bool isPartialBranch = line.IsBranch
                        && line.ConditionCoverage is not null
                        && !line.IsFullBranchCoverage;

                    string marker = isPartialBranch ? "!" : " ";
                    string hitsStr = $"{line.Hits.ToString().PadLeft(hitsWidth)} hits {marker}";

                    string sourceCode = "";
                    int sourceIndex = displayLine - 1;
                    if (sourceLines.Length > 0 && sourceIndex >= 0 && sourceIndex < sourceLines.Length)
                        sourceCode = sourceLines[sourceIndex];

                    sb.AppendLine($"{displayLine.ToString().PadLeft(lineNumWidth)} | {hitsStr} | {sourceCode}");
                }

                sb.AppendLine();
            }

            fmt.AppendHints(sb,
                "Use FindTests to find tests that cover this method",
                "Use RunCoverage to refresh coverage after adding tests");

            return sb.ToString();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static async Task<int> ComputeLineOffsetAsync(
        MethodCoverage method, CancellationToken cancellationToken)
    {
        if (method.Lines.Count == 0)
            return 0;

        try
        {
            var ctx = await ToolHelper.ResolveFileAsync(method.FilePath, null, cancellationToken);
            if (ctx?.Document is null)
                return 0;

            var root = await ctx.Document.GetSyntaxRootAsync(cancellationToken);
            if (root is null)
                return 0;

            int storedFirstLine = method.Lines.Min(l => l.LineNumber);

            var candidates = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == method.Name)
                .ToList();

            if (candidates.Count == 0)
                return 0;

            int bestDiff = int.MaxValue;
            int bestFirstLine = 0;

            foreach (var candidate in candidates)
            {
                int candidateFirstLine = GetMethodFirstExecutableLine(candidate);
                if (candidateFirstLine <= 0)
                    continue;

                int diff = Math.Abs(candidateFirstLine - storedFirstLine);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestFirstLine = candidateFirstLine;
                }
            }

            return bestFirstLine > 0 ? bestFirstLine - storedFirstLine : 0;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return 0;
        }
    }

    private static int GetMethodFirstExecutableLine(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody is not null)
            return method.ExpressionBody.Expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var firstStatement = method.Body?.Statements.FirstOrDefault();
        if (firstStatement is not null)
            return firstStatement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        return method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }
}
