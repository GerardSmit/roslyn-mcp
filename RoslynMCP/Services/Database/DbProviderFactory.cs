namespace RoslynMCP.Services.Database;

public static class DbProviderFactory
{
    private static readonly Dictionary<string, string> s_providerAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["psql"] = "psql",
        ["postgres"] = "psql",
        ["postgresql"] = "psql",
        ["mssql"] = "mssql",
        ["sqlserver"] = "mssql",
        ["sql"] = "mssql",
        ["sqlite"] = "sqlite",
    };

    public static bool TryCanonicalize(string providerToken, out string canonical) =>
        s_providerAliases.TryGetValue(providerToken, out canonical!);

    public static bool IsKnownProviderToken(string token) => s_providerAliases.ContainsKey(token);

    public static IReadOnlyCollection<string> CanonicalProviders { get; } =
        s_providerAliases.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IDbProvider Create(string providerToken, string alias, string connectionString)
    {
        if (!s_providerAliases.TryGetValue(providerToken, out var canonical))
            throw new ArgumentException(
                $"Unknown provider '{providerToken}'. Use psql, mssql, or sqlite.");
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias cannot be empty.");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty.");

        return canonical switch
        {
            "psql" => new PostgresDbProvider(alias, connectionString),
            "mssql" => new MssqlDbProvider(alias, connectionString),
            "sqlite" => new SqliteDbProvider(alias, connectionString),
            _ => throw new InvalidOperationException($"Unreachable provider '{canonical}'."),
        };
    }
}
