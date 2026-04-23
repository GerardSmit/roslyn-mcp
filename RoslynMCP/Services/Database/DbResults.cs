namespace RoslynMCP.Services.Database;

public sealed record DbQueryResult(
    string[] Columns,
    List<string[]> Rows,
    bool Truncated,
    TimeSpan Elapsed);

public sealed record DbSchemaResult(
    string[] Columns,
    List<string[]> Rows);
