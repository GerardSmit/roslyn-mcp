namespace RoslynMCP.Services.Database;

public sealed class DbConnectionRegistry
{
    public DbConnectionRegistry(IEnumerable<IDbProvider> providers)
    {
        All = providers.ToList();
    }

    public IReadOnlyList<IDbProvider> All { get; }

    public IDbProvider? Get(string alias) =>
        All.FirstOrDefault(p => string.Equals(p.Alias, alias, StringComparison.OrdinalIgnoreCase));
}
