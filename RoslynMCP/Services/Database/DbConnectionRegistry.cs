using System.Collections.Concurrent;

namespace RoslynMCP.Services.Database;

public sealed class DbConnectionRegistry
{
    private readonly ConcurrentDictionary<string, IDbProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public DbConnectionRegistry(IEnumerable<IDbProvider> providers)
    {
        foreach (var p in providers)
            _providers[p.Alias] = p;
    }

    public IReadOnlyList<IDbProvider> All =>
        _providers.Values.OrderBy(p => p.Alias, StringComparer.OrdinalIgnoreCase).ToList();

    public IDbProvider? Get(string alias) =>
        _providers.TryGetValue(alias, out var p) ? p : null;

    public bool TryAdd(IDbProvider provider) => _providers.TryAdd(provider.Alias, provider);

    public void AddOrReplace(IDbProvider provider) => _providers[provider.Alias] = provider;

    public bool Remove(string alias) => _providers.TryRemove(alias, out _);
}
