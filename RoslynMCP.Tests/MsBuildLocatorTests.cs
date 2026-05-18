using RoslynMCP.Services;
using Xunit;

namespace RoslynMCP.Tests;

public class MsBuildLocatorTests
{
    [Theory]
    [InlineData(@"D:\Programs\Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
                @"D:\Programs\Visual Studio\18\Community")]
    [InlineData(@"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise")]
    [InlineData(@"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Community")]
    [InlineData(@"D:\VS\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe",
                @"D:\VS\2017\BuildTools")]
    public void GetVsInstallDir_ExtractsCorrectPath(string msbuildPath, string expectedVsDir)
    {
        var result = MsBuildLocator.GetVsInstallDir(msbuildPath);
        Assert.Equal(expectedVsDir, result);
    }

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\sdk\8.0.100\MSBuild.dll")]
    [InlineData(@"C:\tools\msbuild.exe")]
    public void GetVsInstallDir_ReturnsNullForNonVsPaths(string path)
    {
        Assert.Null(MsBuildLocator.GetVsInstallDir(path));
    }
}
