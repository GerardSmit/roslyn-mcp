using System.Data.Common;
using Npgsql;

namespace RoslynMCP.Services.Database;

public sealed class PostgresDbProvider : DbProviderBase
{
    public PostgresDbProvider(string alias, string connectionString)
        : base(alias, "psql", connectionString) { }

    protected override DbConnection CreateConnection() => new NpgsqlConnection(ConnectionString);

    protected override DbCommand CreateCommand(string sql, DbConnection conn) =>
        new NpgsqlCommand(sql, (NpgsqlConnection)conn);

    public override Task<DbSchemaResult> GetTablesAsync(string? schema, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            const string sql =
                "SELECT table_schema, table_name, table_type FROM information_schema.tables " +
                "WHERE table_schema NOT IN ('pg_catalog','information_schema') " +
                "ORDER BY table_schema, table_name";
            return RunSchemaQueryAsync(sql, null, ct);
        }

        const string sqlWithSchema =
            "SELECT table_schema, table_name, table_type FROM information_schema.tables " +
            "WHERE table_schema = @schema ORDER BY table_name";
        return RunSchemaQueryAsync(sqlWithSchema,
            new Dictionary<string, object?> { ["@schema"] = schema }, ct);
    }

    public override Task<DbSchemaResult> DescribeTableAsync(string tableName, CancellationToken ct)
    {
        // Accept "schema.table" or just "table".
        string? schema = null;
        var name = tableName;
        var dot = tableName.IndexOf('.');
        if (dot > 0)
        {
            schema = tableName[..dot];
            name = tableName[(dot + 1)..];
        }

        var sql =
            "SELECT column_name, data_type, is_nullable, column_default " +
            "FROM information_schema.columns " +
            "WHERE table_name = @name" +
            (schema is null ? "" : " AND table_schema = @schema") +
            " ORDER BY ordinal_position";

        var parameters = new Dictionary<string, object?> { ["@name"] = name };
        if (schema is not null) parameters["@schema"] = schema;
        return RunSchemaQueryAsync(sql, parameters, ct);
    }
}
