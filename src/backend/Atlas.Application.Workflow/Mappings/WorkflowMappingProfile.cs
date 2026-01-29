using Atlas.Application.Workflow.Models;
using Atlas.WorkflowCore.Models;
using AutoMapper;

namespace Atlas.Application.Workflow.Mappings;

/// <summary>
/// 工作流 AutoMapper 映射配置
/// </summary>
public sealed class WorkflowMappingProfile : Profile
{
    public WorkflowMappingProfile()
    {
        // WorkflowDefinition 映射
        CreateMap<WorkflowDefinition, WorkflowDefinitionResponse>()
            .ForMember(dest => dest.StepsCount, opt => opt.MapFrom(src => src.Steps.Count))
            .ForMember(dest => dest.DataType, opt => opt.MapFrom(src => src.DataType != null ? src.DataType.FullName : null));

        // WorkflowInstance 映射
        CreateMap<WorkflowInstance, WorkflowInstanceResponse>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.ExecutionPointersCount, opt => opt.MapFrom(src => src.ExecutionPointers.Count));

        CreateMap<WorkflowInstance, WorkflowInstanceListItem>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
    }
}
