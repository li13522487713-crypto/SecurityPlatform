namespace Atlas.Infrastructure.Options;

/// <summary>
/// 低代码工作流配额选项（M19 S19-5 / docs/lowcode-resilience-spec.md §4）。
///
/// 通过 appsettings.json 节 <c>"LowCode:WorkflowQuota"</c> 绑定；可在 PlatformHost / AppHost
/// 任意一侧配置。所有租户共享默认配额；如需租户级差异，可在 PerTenant 字典内按 tenantId 覆盖。
/// </summary>
public sealed class LowCodeWorkflowQuotaOptions
{
    public const string SectionName = "LowCode:WorkflowQuota";

    public int MaxWorkflows { get; set; } = 200;
    public int MaxNodesPerWorkflow { get; set; } = 100;
    public int MaxQpsPerTenant { get; set; } = 10;
    public long MaxMonthlyExecutions { get; set; } = 100_000;

    /// <summary>租户级覆盖（可选）。key = tenantId.Value (Guid 字符串)。</summary>
    public Dictionary<string, LowCodeWorkflowQuotaTenantOverride> PerTenant { get; set; } = new();
}

public sealed class LowCodeWorkflowQuotaTenantOverride
{
    public int? MaxWorkflows { get; set; }
    public int? MaxNodesPerWorkflow { get; set; }
    public int? MaxQpsPerTenant { get; set; }
    public long? MaxMonthlyExecutions { get; set; }
}
