using System.Text.Json;

namespace Atlas.Application.LowCode.Models;

/// <summary>
/// AI 生成工作流（M19 S19-1）。两种模式：
///  - auto：完全自动 → LLM 直接生成完整 DAG canvas JSON
///  - assisted：半自动 → LLM 生成节点列表 → 人工确认 → 再生成绑定
/// </summary>
public sealed record WorkflowGenerationRequest(
    string Mode,           // auto / assisted
    string Prompt,         // 自然语言描述
    /// <summary>可选：参考的现有工作流 ID（基于其修改）。</summary>
    string? BaseWorkflowId);

public sealed record WorkflowGenerationResult(
    string Mode,
    string Status,         // success / partial / failed
    /// <summary>auto 模式产出的 canvas JSON（完整 DAG）；assisted 模式可空。</summary>
    string? CanvasJson,
    /// <summary>assisted 模式产出的节点骨架列表（用户可修改后再调用 finalize）。</summary>
    IReadOnlyList<GeneratedNodeSkeleton>? Nodes,
    string? ErrorMessage);

public sealed record GeneratedNodeSkeleton(string NodeKey, string Type, string Label, Dictionary<string, JsonElement>? ConfigHint);

/// <summary>批量执行 3 种输入源（M19 S19-2）：CSV / JSON / 数据库查询。</summary>
public sealed record BatchExecuteRequest(
    string WorkflowId,
    string SourceKind,           // csv / json / database
    string? CsvText,             // sourceKind=csv 时使用（首行为 header）
    JsonElement? JsonRows,       // sourceKind=json 时使用（数组）
    string? DatabaseQueryId,     // sourceKind=database 时使用（关联现有 AI 数据库节点）
    string? OnFailure);          // continue / abort

public sealed record BatchExecuteResult(string JobId, int Total, int Succeeded, int Failed);

/// <summary>封装/解散子工作流（M19 S19-4）。</summary>
public sealed record WorkflowComposeRequest(
    string WorkflowId,
    /// <summary>选中的节点 key 子集；服务端按 IO 推断子流程接口。</summary>
    IReadOnlyList<string> SelectedNodeKeys,
    string? SubflowName);

public sealed record WorkflowComposeResult(
    string SubWorkflowId,
    /// <summary>从 selected 节点子集推断出的入参字段。</summary>
    IReadOnlyList<string> InferredInputs,
    /// <summary>推断出的输出字段。</summary>
    IReadOnlyList<string> InferredOutputs);

public sealed record WorkflowDecomposeRequest(string WorkflowId, string SubWorkflowNodeKey);

/// <summary>配额信息（M19 S19-5）。</summary>
public sealed record WorkflowQuotaDto(
    int MaxWorkflows,
    int CurrentWorkflows,
    int MaxNodesPerWorkflow,
    int MaxQpsPerTenant,
    long MaxMonthlyExecutions,
    long CurrentMonthlyExecutions);
