using Atlas.Application.BatchProcess.Models;
using Atlas.Domain.BatchProcess.Entities;
using AutoMapper;

namespace Atlas.Application.BatchProcess.Mappings;

public sealed class BatchProcessMappingProfile : Profile
{
    public BatchProcessMappingProfile()
    {
        CreateMap<BatchJobDefinition, BatchJobDefinitionResponse>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()));

        CreateMap<BatchJobDefinition, BatchJobDefinitionListItem>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()));

        CreateMap<BatchJobExecution, BatchJobExecutionResponse>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.JobDefinitionId, opt => opt.MapFrom(s => s.JobDefinitionId.ToString()));

        CreateMap<BatchJobExecution, BatchJobExecutionListItem>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.JobDefinitionId, opt => opt.MapFrom(s => s.JobDefinitionId.ToString()));

        CreateMap<ShardExecution, ShardExecutionResponse>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.JobExecutionId, opt => opt.MapFrom(s => s.JobExecutionId.ToString()))
            .ForMember(d => d.LastCheckpointId, opt => opt.MapFrom(s => s.LastCheckpointId.HasValue ? s.LastCheckpointId.Value.ToString() : null));

        CreateMap<BatchDeadLetter, BatchDeadLetterResponse>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.JobExecutionId, opt => opt.MapFrom(s => s.JobExecutionId.ToString()))
            .ForMember(d => d.ShardExecutionId, opt => opt.MapFrom(s => s.ShardExecutionId.ToString()))
            .ForMember(d => d.BatchExecutionId, opt => opt.MapFrom(s => s.BatchExecutionId.HasValue ? s.BatchExecutionId.Value.ToString() : null));

        CreateMap<BatchDeadLetter, BatchDeadLetterListItem>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.JobExecutionId, opt => opt.MapFrom(s => s.JobExecutionId.ToString()));

        CreateMap<BatchCheckpoint, CheckpointInfo>()
            .ForMember(d => d.CheckpointId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.CheckpointKey, opt => opt.MapFrom(s => s.CheckpointKey))
            .ForMember(d => d.ProcessedUpTo, opt => opt.MapFrom(s => s.ProcessedUpTo))
            .ForMember(d => d.ProcessedCount, opt => opt.MapFrom(s => s.ProcessedCount))
            .ForMember(d => d.CreatedAt, opt => opt.MapFrom(s => s.CreatedAt));
    }
}
