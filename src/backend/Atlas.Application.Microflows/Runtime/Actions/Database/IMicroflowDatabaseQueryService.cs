namespace Atlas.Application.Microflows.Runtime.Actions.Database;

/// <summary>
/// 微流运行时对外部数据库的查询/执行抽象，由 Infrastructure 层实现，通过 DI 注入。
/// 设计态与 Atlas.Application.AiPlatform 解耦，不在此层直接依赖 DatabaseManagementService。
/// </summary>
public interface IMicroflowDatabaseQueryService
{
    Task<MicroflowDatabaseQueryResult> ExecuteAsync(
        MicroflowDatabaseQueryRequest request,
        CancellationToken cancellationToken);
}

public sealed record MicroflowDatabaseQueryRequest
{
    /// <summary>DatabaseCenter sourceId，格式为 "ai:{instanceId}"</summary>
    public string SourceId { get; init; } = string.Empty;

    public string Sql { get; init; } = string.Empty;

    /// <summary>已完成变量替换后的参数列表（有序），名称如 @p0/@p1... 或 ?</summary>
    public IReadOnlyList<MicroflowDatabaseSqlParameter> Parameters { get; init; } = Array.Empty<MicroflowDatabaseSqlParameter>();

    public string? TenantId { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    public int MaxRows { get; init; } = 1000;

    /// <summary>Auto=根据 SQL 首词判断; SelectOnly=只允许 SELECT; DmlOnly=只允许写操作</summary>
    public string Mode { get; init; } = MicroflowDatabaseQueryMode.Auto;
}

public static class MicroflowDatabaseQueryMode
{
    public const string Auto = "auto";
    public const string SelectOnly = "selectOnly";
    public const string DmlOnly = "dmlOnly";
}

public sealed record MicroflowDatabaseSqlParameter(string Name, object? Value);

public sealed record MicroflowDatabaseQueryResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = Array.Empty<IReadOnlyDictionary<string, object?>>();
    public int? AffectedRows { get; init; }
    public long ElapsedMs { get; init; }
    public bool Truncated { get; init; }
}
