using Xunit;

namespace RoslynMCP.Tests;

/// <summary>
/// Skips the test unless Docker is available (for Testcontainers SQL Server).
/// </summary>
public sealed class RequiresMssqlFactAttribute : FactAttribute
{
    public RequiresMssqlFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
            Skip = "Docker is not available. Start Docker Desktop / Engine to run SQL Server integration tests.";
    }
}
