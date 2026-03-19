using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Domain.AiPlatform.ValueObjects;

/// <summary>
/// V2 画布 Schema —— 由前端画布编辑器产出的 JSON 反序列化目标。
/// </summary>
public sealed record CanvasSchema(
    IReadOnlyList<NodeSchema> Nodes,
    IReadOnlyList<ConnectionSchema> Connections);

/// <summary>
/// 单个节点的完整配置。
/// </summary>
public sealed record NodeSchema(
    string Key,
    WorkflowNodeType Type,
    string Label,
    Dictionary<string, JsonElement> Config,
    NodeLayout Layout);

/// <summary>
/// 两个节点之间的连线。
/// </summary>
public sealed record ConnectionSchema(
    string SourceNodeKey,
    string SourcePort,
    string TargetNodeKey,
    string TargetPort,
    string? Condition);

/// <summary>
/// 节点在画布上的布局位置。
/// </summary>
public sealed record NodeLayout(
    double X,
    double Y,
    double Width,
    double Height);
