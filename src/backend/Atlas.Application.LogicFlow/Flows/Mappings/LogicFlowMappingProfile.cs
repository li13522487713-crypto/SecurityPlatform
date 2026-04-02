using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Domain.LogicFlow.Flows;
using AutoMapper;

namespace Atlas.Application.LogicFlow.Flows.Mappings;

public sealed class LogicFlowMappingProfile : Profile
{
    public LogicFlowMappingProfile()
    {
        CreateMap<LogicFlowDefinition, LogicFlowResponse>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.TriggerType, o => o.MapFrom(s => (int)s.TriggerType))
            .ForMember(d => d.SnapshotId, o => o.MapFrom(s => s.SnapshotId.HasValue ? s.SnapshotId.Value.ToString() : null));

        CreateMap<LogicFlowDefinition, LogicFlowListItem>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.TriggerType, o => o.MapFrom(s => (int)s.TriggerType));

        CreateMap<FlowNodeBinding, FlowNodeBindingResponse>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.FlowDefinitionId, o => o.MapFrom(s => s.FlowDefinitionId.ToString()));

        CreateMap<FlowEdgeDefinition, FlowEdgeResponse>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.FlowDefinitionId, o => o.MapFrom(s => s.FlowDefinitionId.ToString()));

        CreateMap<LogicFlowDefinition, LogicFlowDetailResponse>()
            .IncludeBase<LogicFlowDefinition, LogicFlowResponse>()
            .ForMember(d => d.Nodes, o => o.Ignore())
            .ForMember(d => d.Edges, o => o.Ignore());
    }
}
