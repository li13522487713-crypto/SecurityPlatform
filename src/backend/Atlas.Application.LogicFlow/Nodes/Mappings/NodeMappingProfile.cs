using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Domain.LogicFlow.Nodes;
using AutoMapper;

namespace Atlas.Application.LogicFlow.Nodes.Mappings;

public sealed class NodeMappingProfile : Profile
{
    public NodeMappingProfile()
    {
        CreateMap<NodeTypeDefinition, NodeTypeListItem>()
            .ForCtorParam(nameof(NodeTypeListItem.Id), opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam(nameof(NodeTypeListItem.TypeKey), opt => opt.MapFrom(src => src.TypeKey))
            .ForCtorParam(nameof(NodeTypeListItem.Category), opt => opt.MapFrom(src => src.Category))
            .ForCtorParam(nameof(NodeTypeListItem.DisplayName), opt => opt.MapFrom(src => src.DisplayName))
            .ForCtorParam(nameof(NodeTypeListItem.Description), opt => opt.MapFrom(src => src.Description))
            .ForCtorParam(nameof(NodeTypeListItem.Version), opt => opt.MapFrom(src => src.Version))
            .ForCtorParam(nameof(NodeTypeListItem.IsBuiltIn), opt => opt.MapFrom(src => src.IsBuiltIn))
            .ForCtorParam(nameof(NodeTypeListItem.IsActive), opt => opt.MapFrom(src => src.IsActive))
            .ForCtorParam(nameof(NodeTypeListItem.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt));

        CreateMap<NodeTypeDefinition, NodeTypeDetailResponse>()
            .ForCtorParam(nameof(NodeTypeDetailResponse.Id), opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam(nameof(NodeTypeDetailResponse.TypeKey), opt => opt.MapFrom(src => src.TypeKey))
            .ForCtorParam(nameof(NodeTypeDetailResponse.Category), opt => opt.MapFrom(src => src.Category))
            .ForCtorParam(nameof(NodeTypeDetailResponse.DisplayName), opt => opt.MapFrom(src => src.DisplayName))
            .ForCtorParam(nameof(NodeTypeDetailResponse.Description), opt => opt.MapFrom(src => src.Description))
            .ForCtorParam(nameof(NodeTypeDetailResponse.Version), opt => opt.MapFrom(src => src.Version))
            .ForCtorParam(nameof(NodeTypeDetailResponse.IsBuiltIn), opt => opt.MapFrom(src => src.IsBuiltIn))
            .ForCtorParam(nameof(NodeTypeDetailResponse.IsActive), opt => opt.MapFrom(src => src.IsActive))
            .ForCtorParam(nameof(NodeTypeDetailResponse.Ports), opt => opt.MapFrom(src => src.Ports))
            .ForCtorParam(nameof(NodeTypeDetailResponse.ConfigSchema), opt => opt.MapFrom(src => src.ConfigSchema))
            .ForCtorParam(nameof(NodeTypeDetailResponse.Capabilities), opt => opt.MapFrom(src => src.Capabilities))
            .ForCtorParam(nameof(NodeTypeDetailResponse.UiMetadata), opt => opt.MapFrom(src => src.UiMetadata))
            .ForCtorParam(nameof(NodeTypeDetailResponse.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam(nameof(NodeTypeDetailResponse.UpdatedAt), opt => opt.MapFrom(src => src.UpdatedAt));

        CreateMap<NodeTemplate, NodeTemplateListItem>()
            .ForCtorParam(nameof(NodeTemplateListItem.Id), opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam(nameof(NodeTemplateListItem.Name), opt => opt.MapFrom(src => src.Name))
            .ForCtorParam(nameof(NodeTemplateListItem.Description), opt => opt.MapFrom(src => src.Description))
            .ForCtorParam(nameof(NodeTemplateListItem.NodeTypeKey), opt => opt.MapFrom(src => src.NodeTypeKey))
            .ForCtorParam(nameof(NodeTemplateListItem.Category), opt => opt.MapFrom(src => src.Category))
            .ForCtorParam(nameof(NodeTemplateListItem.Tags), opt => opt.MapFrom(src => src.Tags))
            .ForCtorParam(nameof(NodeTemplateListItem.IsPublic), opt => opt.MapFrom(src => src.IsPublic))
            .ForCtorParam(nameof(NodeTemplateListItem.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt));

        CreateMap<NodeTemplate, NodeTemplateDetailResponse>()
            .ForCtorParam(nameof(NodeTemplateDetailResponse.Id), opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.Name), opt => opt.MapFrom(src => src.Name))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.Description), opt => opt.MapFrom(src => src.Description))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.NodeTypeKey), opt => opt.MapFrom(src => src.NodeTypeKey))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.Category), opt => opt.MapFrom(src => src.Category))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.PresetConfig), opt => opt.MapFrom(src => src.PresetConfig))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.Tags), opt => opt.MapFrom(src => src.Tags))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.IsPublic), opt => opt.MapFrom(src => src.IsPublic))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam(nameof(NodeTemplateDetailResponse.UpdatedAt), opt => opt.MapFrom(src => src.UpdatedAt));
    }
}
