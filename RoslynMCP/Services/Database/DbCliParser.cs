namespace RoslynMCP.Services.Database;

public static class DbCliParser
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

    public static IReadOnlyList<IDbProvider> Parse(string[] args)
    {
        var providers = new List<IDbProvider>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--db", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 >= args.Length)
                throw new ArgumentException("--db flag requires a value: <alias>=<provider>:<connstr>");
            var value = args[++i];
            providers.Add(ParseOne(value));
        }
        return providers;
    }

    internal static IDbProvider ParseOne(string value)
    {
        // Find provider token: look for `<alias>=<provider>:` or `<provider>:` prefix.
        string? alias = null;
        string rest = value;

        var eq = value.IndexOf('=');
        if (eq > 0)
        {
            var leftOfEq = value[..eq];
            var afterEq = value[(eq + 1)..];
            var colon = afterEq.IndexOf(':');
            if (colon > 0)
            {
                var maybeProvider = afterEq[..colon];
                if (s_providerAliases.ContainsKey(maybeProvider))
                {
                    alias = leftOfEq;
                    rest = afterEq;
                }
            }
        }

        // Now `rest` is either `<provider>:<connstr>` or a plain connection string.
        var firstColon = rest.IndexOf(':');
        if (firstColon <= 0)
            throw new ArgumentException($"--db value '{value}' is missing a provider prefix (e.g. 'psql:', 'mssql:', 'sqlite:').");

        var providerToken = rest[..firstColon];
        if (!s_providerAliases.TryGetValue(providerToken, out var canonical))
            throw new ArgumentException($"--db value '{value}' has unknown provider '{providerToken}'. Use psql, mssql, or sqlite.");

        var connRef = rest[(firstColon + 1)..];
        if (string.IsNullOrWhiteSpace(connRef))
            throw new ArgumentException($"--db value '{value}' has empty connection string.");

        var connStr = ConnectionStringResolver.Resolve(connRef);
        alias ??= canonical;

        return canonical switch
        {
            "psql" => new PostgresDbProvider(alias, connStr),
            "mssql" => new MssqlDbProvider(alias, connStr),
            "sqlite" => new SqliteDbProvider(alias, connStr),
            _ => throw new InvalidOperationException($"Unreachable provider '{canonical}'."),
        };
    }
}
