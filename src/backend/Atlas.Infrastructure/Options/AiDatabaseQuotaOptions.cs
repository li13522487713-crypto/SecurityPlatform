namespace Atlas.Infrastructure.Options;

/// <summary>D4：AI 数据库配额（软上限 + 节点上限）。</summary>
public sealed class AiDatabaseQuotaOptions
{
    /// <summary>单租户数据库数量上限。</summary>
    public int MaxPerTenant { get; set; } = 200;

    /// <summary>单数据库字段数量上限。</summary>
    public int MaxFieldsPerTable { get; set; } = 20;

    /// <summary>单数据库行数上限（软上限，超过仅告警）。</summary>
    public int MaxRowsPerTable { get; set; } = 100_000;

    /// <summary>单次批量写入上限。</summary>
    public int MaxBulkInsertRows { get; set; } = 1_000;

    /// <summary>单工作流单数据库节点数量上限（设计态校验）。</summary>
    public int MaxTableNodesPerWorkflow { get; set; } = 1;
}
