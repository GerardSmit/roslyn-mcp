using RoslynMCP.Services;
using Xunit;

namespace RoslynMCP.Tests;

public class WorkspaceServiceTests
{
    [Fact]
    public async Task WhenMultipleProjectFilesExistThenContainingProjectResolutionUsesActualOwner()
    {
        await WorkspaceService.EvictAllAsync();

        string? projectPath = await WorkspaceService.FindContainingProjectAsync(FixturePaths.CalculatorFile);

        Assert.Equal(
            Path.GetFullPath(FixturePaths.SampleProjectFile),
            Path.GetFullPath(projectPath!),
            ignoreCase: true);
    }

    [Fact]
    public async Task WhenSameProjectOpenedTwiceThenCachedWorkspaceIsReused()
    {
        await WorkspaceService.EvictAllAsync();

        var first = await WorkspaceService.GetOrOpenProjectAsync(
            FixturePaths.SampleProjectFile,
            targetFilePath: FixturePaths.CalculatorFile);
        var second = await WorkspaceService.GetOrOpenProjectAsync(
            FixturePaths.SampleProjectFile,
            targetFilePath: FixturePaths.CalculatorFile);

        Assert.Same(first.Workspace, second.Workspace);
        Assert.Equal(first.Project.Id, second.Project.Id);
    }

    [Fact]
    public async Task WhenProjectTimestampChangesThenCachedWorkspaceIsInvalidated()
    {
        await WorkspaceService.EvictAllAsync();

        DateTime originalWriteTime = File.GetLastWriteTimeUtc(FixturePaths.SampleProjectFile);
        var first = await WorkspaceService.GetOrOpenProjectAsync(
            FixturePaths.SampleProjectFile,
            targetFilePath: FixturePaths.CalculatorFile);

        try
        {
            File.SetLastWriteTimeUtc(FixturePaths.SampleProjectFile, DateTime.UtcNow.AddMinutes(5));

            var second = await WorkspaceService.GetOrOpenProjectAsync(
                FixturePaths.SampleProjectFile,
                targetFilePath: FixturePaths.CalculatorFile);

            Assert.NotSame(first.Workspace, second.Workspace);
        }
        finally
        {
            File.SetLastWriteTimeUtc(FixturePaths.SampleProjectFile, originalWriteTime);
            await WorkspaceService.EvictAllAsync();
        }
    }
}
