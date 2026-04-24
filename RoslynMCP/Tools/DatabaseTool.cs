using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynMCP.Services;
using RoslynMCP.Services.Database;

namespace RoslynMCP.Tools;

[McpServerToolType]
public static class DatabaseTool
{
    [McpServerTool, Description(
        "Run a SELECT query on a configured database connection. Returns rows as a table. " +
        "Always use the parameters argument for user-supplied values to prevent SQL injection.")]
    public static async Task<string> DbQuery(
        [Description("Alias of the database connection (see db_list_connections).")]
        string alias,
        [Description("SQL SELECT statement.")]
        string sql,
        DbConnectionRegistry db,
        IOutputFormatter fmt,
        [Description("Parameters as a JSON object, e.g. {\"@id\": 42, \"@name\": \"abc\"}. Optional.")]
        string? parameters = null,
        [Description("Maximum rows to return. Default 200.")]
        int maxRows = 200,
        CancellationToken cancellationToken = default)
    {
        var provider = db.Get(alias);
        if (provider is null) return NoConnection(db, alias);

        if (!TryParseParameters(parameters, out var paramsDict, out var parseError))
            return parseError;

        try
        {
            var result = await provider.ExecuteQueryAsync(sql, paramsDict, maxRows, cancellationToken)
                .ConfigureAwait(false);
            var sb = new StringBuilder();
            fmt.AppendField(sb, "Connection", $"{provider.Alias} ({provider.ProviderName})");
            fmt.AppendField(sb, "Elapsed", $"{result.Elapsed.TotalMilliseconds:F0} ms");
            if (result.Rows.Count == 0)
            {
                fmt.AppendEmpty(sb, "Query returned no rows.");
                return sb.ToString();
            }
            fmt.AppendTable(sb, "Results", result.Columns, result.Rows,
                totalCount: result.Truncated ? result.Rows.Count + 1 : result.Rows.Count);
            if (result.Truncated)
                fmt.AppendTruncation(sb, result.Rows.Count, result.Rows.Count + 1, "maxRows");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return SqlError(ex, sql);
        }
    }

    [McpServerTool, Description(
        "Run a non-query SQL statement (INSERT/UPDATE/DELETE/DDL) on a configured database. Returns affected row count.")]
    public static async Task<string> DbExecute(
        [Description("Alias of the database connection.")]
        string alias,
        [Description("SQL statement to execute.")]
        string sql,
        DbConnectionRegistry db,
        IOutputFormatter fmt,
        [Description("Parameters as a JSON object. Optional.")]
        string? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var provider = db.Get(alias);
        if (provider is null) return NoConnection(db, alias);

        if (!TryParseParameters(parameters, out var paramsDict, out var parseError))
            return parseError;

        try
        {
            var affected = await provider.ExecuteNonQueryAsync(sql, paramsDict, cancellationToken)
                .ConfigureAwait(false);
            var sb = new StringBuilder();
            fmt.AppendField(sb, "Connection", $"{provider.Alias} ({provider.ProviderName})");
            fmt.AppendField(sb, "Rows affected", affected);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return SqlError(ex, sql);
        }
    }

    [McpServerTool, Description(
        "Register a new database connection at runtime. Provider must be one of: psql, mssql, sqlite. " +
        "The connection string may also use the xml:/json: config-file syntax (see ConnectionStringResolver). " +
        "If the alias already exists the call fails unless replaceExisting is true.")]
    public static async Task<string> DbAddConnection(
        [Description("Alias to register the connection under. Used by subsequent db_* calls.")]
        string alias,
        [Description("Provider token: psql (postgres/postgresql), mssql (sqlserver/sql), or sqlite.")]
        string provider,
        [Description(
            "Connection string, or a 'xml:<path>#<name>' / 'json:<path>#<name>' reference.")]
        string connectionString,
        DbConnectionRegistry db,
        IOutputFormatter fmt,
        [Description("If true, replaces an existing alias. Default false.")]
        bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(alias)) return "Error: alias cannot be empty.";
        if (string.IsNullOrWhiteSpace(provider)) return "Error: provider cannot be empty.";
        if (string.IsNullOrWhiteSpace(connectionString)) return "Error: connectionString cannot be empty.";

        if (!replaceExisting && db.Get(alias) is not null)
            return $"Error: alias '{alias}' already exists. Pass replaceExisting=true to replace it.";

        string resolved;
        try { resolved = ConnectionStringResolver.Resolve(connectionString); }
        catch (Exception ex) { return $"Error resolving connection string: {ex.Message}"; }

        IDbProvider newProvider;
        try { newProvider = DbProviderFactory.Create(provider, alias, resolved); }
        catch (ArgumentException ex) { return $"Error: {ex.Message}"; }

        try
        {
            await newProvider.GetTablesAsync(schema: null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return $"Error: connection test failed: {ex.Message}";
        }

        if (replaceExisting) db.AddOrReplace(newProvider);
        else if (!db.TryAdd(newProvider))
            return $"Error: alias '{alias}' already exists. Pass replaceExisting=true to replace it.";

        var sb = new StringBuilder();
        fmt.AppendField(sb, "Added", $"{newProvider.Alias} ({newProvider.ProviderName})");
        fmt.AppendField(sb, "Total connections", db.All.Count);
        return sb.ToString();
    }

    [McpServerTool, Description("Remove a database connection registered at runtime or via --db.")]
    public static string DbRemoveConnection(
        [Description("Alias of the connection to remove.")]
        string alias,
        DbConnectionRegistry db,
        IOutputFormatter fmt)
    {
        if (string.IsNullOrWhiteSpace(alias)) return "Error: alias cannot be empty.";
        if (!db.Remove(alias))
            return $"Error: no connection with alias '{alias}'.";

        var sb = new StringBuilder();
        fmt.AppendField(sb, "Removed", alias);
        fmt.AppendField(sb, "Total connections", db.All.Count);
        return sb.ToString();
    }

    [McpServerTool, Description("List all configured database connections.")]
    public static string DbListConnections(DbConnectionRegistry db, IOutputFormatter fmt)
    {
        var sb = new StringBuilder();
        if (db.All.Count == 0)
        {
            fmt.AppendEmpty(sb, "No database connections configured. Register one with db_add_connection, or pass --db <alias>=<provider>:<connstr> to the server.");
            return sb.ToString();
        }
        var rows = db.All
            .Select(p => new[] { p.Alias, p.ProviderName })
            .ToList();
        fmt.AppendTable(sb, "Connections", ["Alias", "Provider"], rows);
        return sb.ToString();
    }

    [McpServerTool, Description("List tables and views in a database, optionally filtered by schema.")]
    public static async Task<string> DbListTables(
        [Description("Alias of the database connection.")]
        string alias,
        DbConnectionRegistry db,
        IOutputFormatter fmt,
        [Description("Schema name filter. Optional; ignored for SQLite.")]
        string? schema = null,
        CancellationToken cancellationToken = default)
    {
        var provider = db.Get(alias);
        if (provider is null) return NoConnection(db, alias);

        try
        {
            var result = await provider.GetTablesAsync(schema, cancellationToken).ConfigureAwait(false);
            var sb = new StringBuilder();
            fmt.AppendField(sb, "Connection", $"{provider.Alias} ({provider.ProviderName})");
            if (result.Rows.Count == 0)
            {
                fmt.AppendEmpty(sb, "No tables found.");
                return sb.ToString();
            }
            fmt.AppendTable(sb, "Tables", result.Columns, result.Rows);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return SqlError(ex, "GetTables");
        }
    }

    [McpServerTool, Description("Show columns, data types, nullability, and defaults for a table.")]
    public static async Task<string> DbDescribeTable(
        [Description("Alias of the database connection.")]
        string alias,
        [Description("Table name (optionally prefixed with schema, e.g. 'public.users').")]
        string tableName,
        DbConnectionRegistry db,
        IOutputFormatter fmt,
        CancellationToken cancellationToken = default)
    {
        var provider = db.Get(alias);
        if (provider is null) return NoConnection(db, alias);

        try
        {
            var result = await provider.DescribeTableAsync(tableName, cancellationToken).ConfigureAwait(false);
            var sb = new StringBuilder();
            fmt.AppendField(sb, "Connection", $"{provider.Alias} ({provider.ProviderName})");
            fmt.AppendField(sb, "Table", tableName);
            if (result.Rows.Count == 0)
            {
                fmt.AppendEmpty(sb, $"Table '{tableName}' not found or has no columns.");
                return sb.ToString();
            }
            fmt.AppendTable(sb, "Columns", result.Columns, result.Rows);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return SqlError(ex, $"DescribeTable {tableName}");
        }
    }

    private static string NoConnection(DbConnectionRegistry db, string alias)
    {
        var available = db.All.Count == 0
            ? "(none configured)"
            : string.Join(", ", db.All.Select(p => p.Alias));
        return $"Error: No connection with alias '{alias}'. Available: {available}.";
    }

    private static bool TryParseParameters(
        string? json, out Dictionary<string, object?>? parameters, out string error)
    {
        error = "";
        parameters = null;
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Error: parameters must be a JSON object, e.g. {\"@id\": 42}.";
                return false;
            }
            parameters = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                parameters[prop.Name] = JsonElementToValue(prop.Value);
            return true;
        }
        catch (JsonException jex)
        {
            error = $"Error: invalid parameters JSON: {jex.Message}";
            return false;
        }
    }

    private static object? JsonElementToValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText(),
    };

    private static string SqlError(Exception ex, string context)
    {
        var ctx = context.Length > 200 ? context[..200] + "..." : context;
        return $"SQL Error: {ex.Message}\nCommand: {ctx}";
    }
}
