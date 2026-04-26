using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RoslynMCP.Services;
using RoslynMCP.Services.Database;
using Xunit;

namespace RoslynMCP.Tests;

/// <summary>
/// Tests that the MCP server correctly registers and advertises its tools.
///
/// Root cause of the original bug: calling .WithTools(toolTypes) where toolTypes is Type[]
/// caused C# overload resolution to pick the generic WithTools&lt;T&gt;(T target) overload
/// (identity conversion from Type[] beats the implicit IEnumerable&lt;Type&gt; conversion),
/// which scanned typeof(Type[]) for [McpServerTool] methods, found none, and silently
/// registered nothing. The initialize response therefore omitted the "tools" capability,
/// causing Copilot (which respects MCP capabilities) to refuse to call tools/list.
/// The fix is to cast: .WithTools((IEnumerable&lt;Type&gt;)toolTypes).
/// </summary>
public class McpProtocolCapabilitiesTests
{
    [Fact]
    public void McpServerToolsAreRegisteredInDI()
    {
        // Replicate the tool type filtering done in Program.cs
        var toolTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .ToArray();

        Assert.NotEmpty(toolTypes);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IOutputFormatter>(new MarkdownFormatter());
        builder.Services.AddSingleton<ProfilingSessionStore>();
        builder.Services.AddSingleton<BackgroundTaskStore>();
        builder.Services.AddSingleton<BuildWarningsStore>();
        builder.Services.AddSingleton(new DbConnectionRegistry([]));

        // The cast to IEnumerable<Type> is required to call the correct overload.
        // Without it, C# resolves to WithTools<Type[]>(Type[] target), which finds
        // no tools and silently registers nothing.
        builder.Services
            .AddMcpServer()
            .WithTools((IEnumerable<Type>)toolTypes);

        var toolDescriptors = builder.Services
            .Count(sd => sd.ServiceType == typeof(McpServerTool));
        Assert.True(toolDescriptors > 0, $"WithTools registered 0 McpServerTool descriptors — overload resolution may have silently picked the wrong overload");

        var host = builder.Build();
        var tools = host.Services.GetRequiredService<IEnumerable<McpServerTool>>().ToList();
        Assert.NotEmpty(tools);
    }

    [Fact]
    public async Task McpInitializeResponseIncludesToolsCapability()
    {
        var (fileName, arguments) = FindServerLaunchArgs();
        Assert.True(fileName is not null, "Server binary/dll not found in any known output directory");

        var initMsg = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""";

        var psi = new System.Diagnostics.ProcessStartInfo(fileName!)
        {
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = System.Diagnostics.Process.Start(psi)!;
        await proc.StandardInput.WriteLineAsync(initMsg);
        await Task.Delay(500);
        proc.Kill();

        var stdout = await proc.StandardOutput.ReadToEndAsync();

        // The initialize response must declare the "tools" capability.
        // When this is absent, Copilot refuses to call tools/list.
        Assert.NotEmpty(stdout);
        Assert.Contains("\"tools\"", stdout, StringComparison.Ordinal);
    }

    private static (string? fileName, string arguments) FindServerLaunchArgs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RoslynMCP.sln")))
            {
                var repoRoot = dir.FullName;
                var configurations = new[] { "Release", "Debug" };

                foreach (var config in configurations)
                {
                    var outputDir = Path.Combine(repoRoot, "RoslynMCP", "bin", config, "net10.0");

                    // Prefer native app host (.exe on Windows, no extension on Linux/macOS)
                    var exeExt = OperatingSystem.IsWindows() ? ".exe" : "";
                    var exe = Path.Combine(outputDir, $"RoslynMCP{exeExt}");
                    if (File.Exists(exe))
                        return (exe, "");

                    // Fall back to dotnet <dll> (used when UseAppHost=false)
                    var dll = Path.Combine(outputDir, "RoslynMCP.dll");
                    if (File.Exists(dll))
                        return ("dotnet", dll);
                }

                return (null, "");
            }
            dir = dir.Parent!;
        }
        return (null, "");
    }
}