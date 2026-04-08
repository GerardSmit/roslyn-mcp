using System.ComponentModel;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using RoslynMCP.Services;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class ValidateFileTool
{
    [McpServerTool, Description("Validates a C# file using Roslyn and runs code analyzers. Accepts either a relative or absolute file path.")]
    public static async Task<string> ValidateFile(
        [Description("The path to the C# file to validate")] string filePath,
        [Description("Run analyzers (default: true)")] bool runAnalyzers = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return "Error: File path cannot be empty.";

            string systemPath = PathHelper.NormalizePath(filePath);

            if (!File.Exists(systemPath))
                return $"Error: File {systemPath} does not exist.";

            string? projectPath = await WorkspaceService.FindContainingProjectAsync(systemPath, cancellationToken);
            if (string.IsNullOrEmpty(projectPath))
                return "Error: Couldn't find a project containing this file.";

            Console.Error.WriteLine($"[ValidateFile] Validating '{systemPath}' in project '{projectPath}'");

            var outputWriter = new StringWriter();
            await ValidateFileInProjectContextAsync(
                systemPath, projectPath, outputWriter, runAnalyzers, cancellationToken);
            return outputWriter.ToString();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ValidateFile] Unhandled error: {ex}");
            return $"Error processing file: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates a C# file within its project context, reporting syntax, semantic,
    /// compilation, and (optionally) analyzer diagnostics to <paramref name="writer"/>.
    /// </summary>
    internal static async Task ValidateFileInProjectContextAsync(
        string filePath, string projectPath, TextWriter? writer = null, bool runAnalyzers = true,
        CancellationToken cancellationToken = default)
    {
        writer ??= Console.Out;

        try
        {
            var (_, project) = await WorkspaceService.GetOrOpenProjectAsync(
                projectPath, targetFilePath: filePath, diagnosticWriter: Console.Error,
                cancellationToken: cancellationToken);

            writer.WriteLine($"Loading project: {projectPath}");
            writer.WriteLine($"Project loaded successfully: {project.Name}");

            var document = WorkspaceService.FindDocumentInProject(project, filePath);

            if (document == null)
            {
                writer.WriteLine("Error: File not found in the project documents.");
                writer.WriteLine("All project documents:");
                foreach (var doc in project.Documents)
                {
                    writer.WriteLine($"  - {doc.FilePath}");
                }

                return;
            }

            writer.WriteLine($"Document found: {document.Name}");

            // Syntax diagnostics
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree == null)
            {
                writer.WriteLine("Error: Unable to obtain syntax tree for document.");
                return;
            }

            var syntaxDiagnostics = syntaxTree.GetDiagnostics();

            if (syntaxDiagnostics.Any())
            {
                writer.WriteLine("Syntax errors found:");
                foreach (var diagnostic in syntaxDiagnostics)
                {
                    var location = diagnostic.Location.GetLineSpan();
                    writer.WriteLine($"Line {location.StartLinePosition.Line + 1}: {diagnostic.GetMessage()}");
                }
            }
            else
            {
                writer.WriteLine("No syntax errors found.");
            }

            // Semantic diagnostics
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null)
            {
                writer.WriteLine("Error: Unable to obtain semantic model for document.");
                return;
            }

            var semanticDiagnostics = semanticModel.GetDiagnostics();

            if (semanticDiagnostics.Any())
            {
                writer.WriteLine("\nSemantic errors found:");
                foreach (var diagnostic in semanticDiagnostics)
                {
                    var location = diagnostic.Location.GetLineSpan();
                    writer.WriteLine($"Line {location.StartLinePosition.Line + 1}: {diagnostic.GetMessage()}");
                }
            }
            else
            {
                writer.WriteLine("No semantic errors found.");
            }

            // Compilation diagnostics
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
            {
                writer.WriteLine("Error: Unable to produce compilation for project.");
                return;
            }

            var compilationDiagnostics = compilation.GetDiagnostics()
                .Where(d => d.Location.SourceTree != null &&
                            string.Equals(d.Location.SourceTree.FilePath, filePath,
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Analyzer diagnostics
            IEnumerable<Diagnostic> analyzerDiagnostics = Array.Empty<Diagnostic>();
            if (runAnalyzers)
            {
                analyzerDiagnostics = await AnalyzerService.RunAnalyzersAsync(
                    project, compilation, filePath, writer, cancellationToken);
            }

            var allDiagnostics = compilationDiagnostics.Concat(analyzerDiagnostics);

            if (allDiagnostics.Any())
            {
                writer.WriteLine("\nCompilation and analyzer diagnostics:");
                foreach (var diagnostic in allDiagnostics.OrderBy(d => d.Severity))
                {
                    var location = diagnostic.Location.GetLineSpan();
                    var severity = diagnostic.Severity.ToString();
                    writer.WriteLine(
                        $"[{severity}] Line {location.StartLinePosition.Line + 1}: {diagnostic.Id} - {diagnostic.GetMessage()}");
                }
            }
            else
            {
                writer.WriteLine("File compiles successfully in project context with no analyzer warnings.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            writer.WriteLine($"Error validating file: {ex.Message}");
            if (ex.InnerException != null)
            {
                writer.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }
}
