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
///
/// P3-7 增强：除了 Plan 的元数据生成外，增加 ExecuteAsync 真实执行入口 ——
/// agentic 模式接 LLM tool calling 协议，运行时根据模型决策动态调用 Tool 池。
/// 此前 Plan 仅生成元数据，DagWorkflowExecutionService.SyncRunAsync 无 agentic 分支
/// → agentic 模式实际不会执行任何 LLM tool calling（FINAL 报告未发现）。
/// </summary>
public interface IDualOrchestrationEngine
{
    /// <summary>把当前 canvas 与 mode 转为执行配置（agentic 模式注入 tools 池等元数据）。</summary>
    OrchestrationPlan Plan(string canvasJson, string mode, IReadOnlyList<OrchestrationTool>? tools);

    /// <summary>
    /// agentic 模式真实执行（P3-7）：基于 IChatClient + tool calling 循环，运行时根据模型决策调用 Tool。
    /// explicit 模式不调用此方法（继续走 DagExecutor）。
    /// 参数 prompt 为本轮用户输入；结果为 LLM 最终回复 + 调用过的工具调用记录。
    /// 若租户未配置 LLM（IChatClientFactory 不可用）→ 返回 ExecuteResult.Error("MODEL_PROVIDER_NOT_CONFIGURED")。
    /// </summary>
    Task<OrchestrationExecuteResult> ExecuteAsync(
        TenantId tenantId,
        OrchestrationPlan plan,
        string prompt,
        OrchestrationExecutionOptions? options,
        CancellationToken cancellationToken);
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

public sealed record OrchestrationExecutionOptions(
    /// <summary>最大 LLM 轮次（防止无限 tool calling 循环）。默认 8。</summary>
    int MaxRounds = 8,
    /// <summary>整体超时（默认 60s）。</summary>
    int TimeoutSeconds = 60,
    /// <summary>使用的模型 id（来自 ModelConfigPool）。为空时用租户默认模型。</summary>
    string? ModelId = null);

public sealed record OrchestrationToolInvocation(
    string ToolName,
    string ArgumentsJson,
    string? ResultJson,
    string? Error);

public sealed record OrchestrationExecuteResult(
    bool Success,
    string FinalText,
    IReadOnlyList<OrchestrationToolInvocation> Invocations,
    string? ErrorCode,
    string? ErrorMessage);
