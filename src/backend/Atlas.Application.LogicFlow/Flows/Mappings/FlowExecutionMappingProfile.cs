using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Domain.LogicFlow.Flows;
using AutoMapper;

namespace Atlas.Application.LogicFlow.Flows.Mappings;

public sealed class FlowExecutionMappingProfile : Profile
{
    private const string RoundtripFormat = "O";

    public FlowExecutionMappingProfile()
    {
        CreateMap<FlowExecution, FlowExecutionResponse>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.SnapshotId, o => o.MapFrom(s => s.SnapshotId.HasValue ? s.SnapshotId.Value.ToString() : null))
            .ForMember(d => d.ParentExecutionId, o => o.MapFrom(s => s.ParentExecutionId.HasValue ? s.ParentExecutionId.Value.ToString() : null))
            .ForMember(d => d.StartedAt, o => o.MapFrom(s => s.StartedAt.HasValue ? s.StartedAt.Value.ToString(RoundtripFormat) : null))
            .ForMember(d => d.CompletedAt, o => o.MapFrom(s => s.CompletedAt.HasValue ? s.CompletedAt.Value.ToString(RoundtripFormat) : null))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt.ToString(RoundtripFormat)));

        CreateMap<FlowExecution, FlowExecutionListItem>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()));

        CreateMap<NodeRun, NodeRunResponse>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.StartedAt, o => o.MapFrom(s => s.StartedAt.HasValue ? s.StartedAt.Value.ToString(RoundtripFormat) : null))
            .ForMember(d => d.CompletedAt, o => o.MapFrom(s => s.CompletedAt.HasValue ? s.CompletedAt.Value.ToString(RoundtripFormat) : null));
    }
}
