namespace RoslynMCP.Services.Database;

public interface IDbProvider
{
    string Alias { get; }
    string ProviderName { get; }

    Task<DbQueryResult> ExecuteQueryAsync(
        string sql,
        Dictionary<string, object?>? parameters,
        int maxRows,
        CancellationToken ct);

    Task<int> ExecuteNonQueryAsync(
        string sql,
        Dictionary<string, object?>? parameters,
        CancellationToken ct);

    Task<DbSchemaResult> GetTablesAsync(string? schema, CancellationToken ct);

    Task<DbSchemaResult> DescribeTableAsync(string tableName, CancellationToken ct);
}
