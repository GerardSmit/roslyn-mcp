using Xunit;

namespace RoslynMCP.Tests;

public class WorkflowConfigurationTests
{
    [Fact]
    public void CiWorkflowExistsAndUsesDotnet10()
    {
        string content = File.ReadAllText(GetRepoPath(".github", "workflows", "ci.yml"));

        Assert.Contains("dotnet-version: 10.0.x", content, StringComparison.Ordinal);
        Assert.Contains("dotnet build RoslynMCP.sln --configuration Release --no-restore --nologo", content, StringComparison.Ordinal);
        Assert.Contains("dotnet test RoslynMCP.sln --configuration Release --no-build --nologo -p:UseAppHost=false", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishWorkflowExistsAndPublishesRoslynMcpToNuGet()
    {
        string content = File.ReadAllText(GetRepoPath(".github", "workflows", "publish.yml"));

        Assert.Contains("branches:", content, StringComparison.Ordinal);
        Assert.Contains("- main", content, StringComparison.Ordinal);
        Assert.Contains("fetch-depth: 0", content, StringComparison.Ordinal);
        Assert.Contains("Compute package version", content, StringComparison.Ordinal);
        Assert.Contains("git describe --tags --match 'v*' --abbrev=0", content, StringComparison.Ordinal);
        Assert.Contains("git rev-list \"${latest_tag}..HEAD\" --count", content, StringComparison.Ordinal);
        Assert.Contains("git rev-list HEAD --count", content, StringComparison.Ordinal);
        Assert.Contains("package_version=\"$major.$minor.$((patch + commit_count))\"", content, StringComparison.Ordinal);
        Assert.Contains("dotnet pack RoslynMCP/RoslynMCP.csproj", content, StringComparison.Ordinal);
        Assert.Contains("/p:PackageVersion=${{ steps.version.outputs.package_version }}", content, StringComparison.Ordinal);
        Assert.Contains("roslyn-mcp", content, StringComparison.Ordinal);
        Assert.Contains("dotnet nuget push", content, StringComparison.Ordinal);
        Assert.Contains("NUGET_API_KEY", content, StringComparison.Ordinal);
        Assert.DoesNotContain("GITHUB_RUN_NUMBER", content, StringComparison.Ordinal);
    }

    private static string GetRepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string solutionPath = Path.Combine(directory.FullName, "RoslynMCP.sln");
            if (File.Exists(solutionPath))
                return Path.Combine([directory.FullName, .. parts]);

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
