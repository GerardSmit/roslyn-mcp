using Docker.DotNet;

namespace RoslynMCP.Tests;

internal static class DockerAvailability
{
    private static readonly Lazy<bool> s_isAvailable = new(Probe);

    public static bool IsAvailable => s_isAvailable.Value;

    private static bool Probe()
    {
        try
        {
            using var cfg = new DockerClientConfiguration();
            using var client = cfg.CreateClient();
            client.System.PingAsync().GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
