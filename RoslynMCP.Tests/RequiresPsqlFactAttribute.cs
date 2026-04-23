using Xunit;

namespace RoslynMCP.Tests;

/// <summary>
/// Skips the test unless Docker is available (for Testcontainers PostgreSQL).
/// </summary>
public sealed class RequiresPsqlFactAttribute : FactAttribute
{
    public RequiresPsqlFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
            Skip = "Docker is not available. Start Docker Desktop / Engine to run PostgreSQL integration tests.";
    }
}
