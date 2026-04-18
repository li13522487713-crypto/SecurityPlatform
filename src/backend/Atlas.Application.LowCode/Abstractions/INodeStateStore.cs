using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 节点级状态存储（M20 S20-7）。
///
/// 4 类作用域（与 docs/lowcode-orchestration-spec.md §3 对齐）：
///  - session：会话级
///  - conversation：对话级
///  - trigger：触发器级
///  - app：应用级
/// 节点 executor 通过 NodeExecutionContext.State 访问；M20 阶段单 SQL 读写，避免循环。
/// </summary>
public interface INodeStateStore
{
    Task<string?> ReadAsync(TenantId tenantId, string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken);
    Task WriteAsync(TenantId tenantId, string scope, string scopeKey, string nodeKey, string stateJson, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken);
}

/// <summary>
/// 双哲学编排：模型自决（agentic）vs 显式节点（explicit）。
/// M20 阶段提供切换器与 metadata 输出；真实模型自决执行由 IDagWorkflowExecutionService.SyncRunAsync 在配置带有 orchestration=agentic 时改写为 LLM tool calling 协议。
/// </summary>
public interface IDualOrchestrationEngine
{
    /// <summary>把当前 canvas 与 mode 转为执行配置（agentic 模式注入 tools 池等元数据）。</summary>
    OrchestrationPlan Plan(string canvasJson, string mode, IReadOnlyList<OrchestrationTool>? tools);
}

public sealed record OrchestrationTool(
    string Name,
    string Description,
    /// <summary>工具类型（与 DagWorkflow 节点 Type 对齐：Llm / Plugin / KnowledgeRetriever / DatabaseQuery / HttpRequester / TextProcessor 等）。</summary>
    string Type = "Plugin");

public sealed record OrchestrationPlan(
    string Mode,
    string CanvasJson,
    /// <summary>agentic 模式：tools 描述。</summary>
    IReadOnlyList<OrchestrationTool>? Tools,
    /// <summary>额外注入的 metadata（前端 / 引擎共享）。</summary>
    string MetadataJson);
