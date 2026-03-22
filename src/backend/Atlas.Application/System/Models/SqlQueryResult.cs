using System.Collections.Generic;

namespace Atlas.Application.System.Models;

public record SqlQueryResultColumn(string Field, string Title, string Type);

public class SqlQueryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SqlQueryResultColumn> Columns { get; set; } = new();
    public List<IDictionary<string, object?>> Data { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

public class DataSourceSchemaResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DataSourceTableInfo> Tables { get; set; } = new();
}

public class DataSourceTableInfo
{
    public string Name { get; set; } = string.Empty;
    public List<DataSourceColumnInfo> Columns { get; set; } = new();
}

public class DataSourceColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
}
