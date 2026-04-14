using System.Diagnostics;
using System.Net.Http;

namespace RoslynMCP.Services;

/// <summary>
/// Locates the MSBuild executable for building legacy .NET Framework projects.
/// Uses vswhere first, then falls back to known Visual Studio installation paths.
/// vswhere itself is auto-provisioned from GitHub releases when not found in the
/// standard VS installer location.
/// </summary>
internal static class MsBuildLocator
{
    private static readonly string s_toolsDirectory = Path.Combine(
        Path.GetTempPath(), "RoslynMCP", "Tools");

    private static string? _cachedPath;
    private static bool _searched;
    private static string? _cachedVsWherePath;
    private static bool _vsWhereSearched;

    /// <summary>
    /// Returns the full path to MSBuild.exe, or null if not found.
    /// Result is cached after the first call.
    /// </summary>
    public static string? FindMsBuild()
    {
        if (_searched) return _cachedPath;
        _searched = true;
        _cachedPath = LocateMsBuild();
        return _cachedPath;
    }

    /// <summary>
    /// Returns the path to vswhere.exe, downloading it from GitHub if not already installed.
    /// vswhere is Windows-only; returns null on non-Windows platforms.
    /// The result is cached after the first call.
    /// </summary>
    public static string? EnsureVsWhere()
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (_vsWhereSearched) return _cachedVsWherePath;
        _vsWhereSearched = true;
        _cachedVsWherePath = FindOrDownloadVsWhere();
        return _cachedVsWherePath;
    }

    private static string? FindOrDownloadVsWhere()
    {
        // 1. Standard VS installer location
        var standardPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            @"Microsoft Visual Studio\Installer\vswhere.exe");
        if (File.Exists(standardPath)) return standardPath;

        // 2. Tools cache (previously auto-downloaded)
        var cachedPath = Path.Combine(s_toolsDirectory, "vswhere", "vswhere.exe");
        if (File.Exists(cachedPath)) return cachedPath;

        // 3. Auto-download from GitHub releases
        Console.Error.WriteLine("[MsBuildLocator] vswhere.exe not found — downloading from GitHub releases...");
        return DownloadVsWhere(cachedPath);
    }

    private static string? DownloadVsWhere(string targetPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            // Use synchronous Send() so this can be called from a static constructor.
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RoslynSense/1.0");

            // GitHub redirects /releases/latest/download/ to the actual asset URL.
            const string downloadUrl =
                "https://github.com/microsoft/vswhere/releases/latest/download/vswhere.exe";

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            using var response = http.Send(request);
            response.EnsureSuccessStatusCode();

            using var fileStream = File.Create(targetPath);
            response.Content.ReadAsStream().CopyTo(fileStream);

            Console.Error.WriteLine($"[MsBuildLocator] vswhere.exe downloaded to '{targetPath}'.");
            return targetPath;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MsBuildLocator] Failed to download vswhere.exe: {ex.Message}");
            return null;
        }
    }

    private static string? LocateMsBuild()
    {
        // Try vswhere first — the authoritative way to find VS tools
        var vswherePath = EnsureVsWhere();

        if (vswherePath is not null)
        {
            var msbuild = RunVsWhere(vswherePath);
            if (msbuild is not null) return msbuild;
        }

        // Fallback: probe known VS installation paths
        return ProbeKnownPaths();
    }

    private static string? RunVsWhere(string vswherePath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = vswherePath,
                    Arguments = "-latest -requires Microsoft.Component.MSBuild " +
                                "-find MSBuild\\**\\Bin\\MSBuild.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            // vswhere may return multiple lines; pick the first valid one
            foreach (var line in output.Split('\n'))
            {
                var candidate = line.Trim();
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // Ignore — fall through to probe
        }

        return null;
    }

    private static string? ProbeKnownPaths()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var editions = new[] { "Enterprise", "Professional", "Community", "BuildTools", "Preview" };
        var versions = new[] { "18", "2022", "2019", "2017" };
        var relativePath = @"MSBuild\Current\Bin\MSBuild.exe";

        foreach (var version in versions)
        {
            foreach (var edition in editions)
            {
                foreach (var root in new[] { programFiles, programFilesX86 })
                {
                    var candidate = Path.Combine(root, "Microsoft Visual Studio", version, edition, relativePath);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }

        // MSBuild shipped standalone in VS 2017 Build Tools at a different path
        foreach (var root in new[] { programFiles, programFilesX86 })
        {
            var candidate = Path.Combine(root, "Microsoft Visual Studio", "2017", "BuildTools",
                @"MSBuild\15.0\Bin\MSBuild.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Returns the full output path of the built assembly for a legacy project,
    /// using MSBuild's <c>/getProperty:TargetPath</c>. Returns null if MSBuild
    /// is not found or the property cannot be evaluated.
    /// </summary>
    public static string? GetTargetPath(string csprojPath)
    {
        var msbuild = FindMsBuild();
        if (msbuild is null) return null;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = msbuild,
                    Arguments = $"\"{csprojPath}\" /nologo /v:minimal /getProperty:TargetPath",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(csprojPath)!
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            // Output is the TargetPath value — pick the last non-empty line (MSBuild may print warnings)
            foreach (var line in output.Split('\n').Reverse())
            {
                var candidate = line.Trim();
                if (candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }
        catch { }

        return null;
    }
}
