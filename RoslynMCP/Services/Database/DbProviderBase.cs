using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;

namespace RoslynMCP.Services.Database;

public abstract class DbProviderBase : IDbProvider
{
    protected DbProviderBase(string alias, string providerName, string connectionString)
    {
        Alias = alias;
        ProviderName = providerName;
        ConnectionString = connectionString;
    }

    public string Alias { get; }
    public string ProviderName { get; }
    protected string ConnectionString { get; }

    protected abstract DbConnection CreateConnection();
    protected abstract DbCommand CreateCommand(string sql, DbConnection conn);
    protected virtual Task OnConnectionOpenedAsync(DbConnection conn, CancellationToken ct) => Task.CompletedTask;

    public abstract Task<DbSchemaResult> GetTablesAsync(string? schema, CancellationToken ct);
    public abstract Task<DbSchemaResult> DescribeTableAsync(string tableName, CancellationToken ct);

    public async Task<DbQueryResult> ExecuteQueryAsync(
        string sql, Dictionary<string, object?>? parameters, int maxRows, CancellationToken ct)
    {
        if (maxRows < 1) maxRows = 1;
        var sw = Stopwatch.StartNew();
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await OnConnectionOpenedAsync(conn, ct).ConfigureAwait(false);
        await using var cmd = CreateCommand(sql, conn);
        BindParameters(cmd, parameters);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var (columns, rows, truncated) = await ReadRowsAsync(reader, maxRows, ct).ConfigureAwait(false);
        sw.Stop();
        return new DbQueryResult(columns, rows, truncated, sw.Elapsed);
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql, Dictionary<string, object?>? parameters, CancellationToken ct)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await OnConnectionOpenedAsync(conn, ct).ConfigureAwait(false);
        await using var cmd = CreateCommand(sql, conn);
        BindParameters(cmd, parameters);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    protected static void BindParameters(DbCommand cmd, Dictionary<string, object?>? parameters)
    {
        if (parameters is null) return;
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    protected static async Task<(string[] columns, List<string[]> rows, bool truncated)> ReadRowsAsync(
        DbDataReader reader, int maxRows, CancellationToken ct)
    {
        var columns = new string[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
            columns[i] = reader.GetName(i);

        var rows = new List<string[]>(Math.Min(maxRows, 64));
        bool truncated = false;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }
            var row = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = FormatValue(reader.IsDBNull(i) ? null : reader.GetValue(i));
            rows.Add(row);
        }
        return (columns, rows, truncated);
    }

    protected static string FormatValue(object? value)
    {
        return value switch
        {
            null => "(null)",
            byte[] bytes => $"<binary {bytes.Length} bytes>",
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    protected async Task<DbSchemaResult> RunSchemaQueryAsync(string sql, Dictionary<string, object?>? parameters, CancellationToken ct)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await OnConnectionOpenedAsync(conn, ct).ConfigureAwait(false);
        await using var cmd = CreateCommand(sql, conn);
        BindParameters(cmd, parameters);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var (columns, rows, _) = await ReadRowsAsync(reader, int.MaxValue, ct).ConfigureAwait(false);
        return new DbSchemaResult(columns, rows);
    }
}
