using Atlas.Application.LogicFlow.Flows.Models;

namespace Atlas.WebApi.Models;

/// <summary>
/// 创建逻辑流：流程元数据 + 节点绑定 + 边。
/// </summary>
public sealed class LogicFlowWriteRequest
{
    public LogicFlowCreateRequest Flow { get; set; } = new();
    public List<FlowNodeBindingRequest> Nodes { get; set; } = [];
    public List<FlowEdgeRequest> Edges { get; set; } = [];
}

/// <summary>
/// 更新逻辑流：流程元数据 + 节点绑定 + 边。
/// </summary>
public sealed class LogicFlowUpdateWriteRequest
{
    public LogicFlowUpdateRequest Flow { get; set; } = new() { IsEnabled = true };
    public List<FlowNodeBindingRequest> Nodes { get; set; } = [];
    public List<FlowEdgeRequest> Edges { get; set; } = [];
}
