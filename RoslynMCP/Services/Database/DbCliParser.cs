namespace RoslynMCP.Services.Database;

public static class DbCliParser
{
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
                if (DbProviderFactory.IsKnownProviderToken(maybeProvider))
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
        if (!DbProviderFactory.TryCanonicalize(providerToken, out var canonical))
            throw new ArgumentException($"--db value '{value}' has unknown provider '{providerToken}'. Use psql, mssql, or sqlite.");

        var connRef = rest[(firstColon + 1)..];
        if (string.IsNullOrWhiteSpace(connRef))
            throw new ArgumentException($"--db value '{value}' has empty connection string.");

        var connStr = ConnectionStringResolver.Resolve(connRef);
        alias ??= canonical;

        return DbProviderFactory.Create(canonical, alias, connStr);
    }
}
