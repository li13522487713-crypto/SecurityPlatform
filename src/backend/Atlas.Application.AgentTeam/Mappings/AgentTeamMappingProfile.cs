using AutoMapper;
using Atlas.Application.AgentTeam.Models;
using Atlas.Domain.AgentTeam.Entities;

namespace Atlas.Application.AgentTeam.Mappings;

public sealed class AgentTeamMappingProfile : Profile
{
    public AgentTeamMappingProfile()
    {
        CreateMap<AgentTeamDefinition, AgentTeamListItem>()
            .ForCtorParam(nameof(AgentTeamListItem.Id), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(AgentTeamListItem.TeamName), opt => opt.MapFrom(src => src.TeamName))
            .ForCtorParam(nameof(AgentTeamListItem.Description), opt => opt.MapFrom(src => src.Description))
            .ForCtorParam(nameof(AgentTeamListItem.Owner), opt => opt.MapFrom(src => src.Owner))
            .ForCtorParam(nameof(AgentTeamListItem.Status), opt => opt.MapFrom(src => src.Status))
            .ForCtorParam(nameof(AgentTeamListItem.PublishStatus), opt => opt.MapFrom(src => src.PublishStatus))
            .ForCtorParam(nameof(AgentTeamListItem.PublishedVersionId), opt => opt.MapFrom(src => src.PublishedVersionId))
            .ForCtorParam(nameof(AgentTeamListItem.RiskLevel), opt => opt.MapFrom(src => src.RiskLevel))
            .ForCtorParam(nameof(AgentTeamListItem.Version), opt => opt.MapFrom(src => src.Version))
            .ForCtorParam(nameof(AgentTeamListItem.UpdatedAt), opt => opt.MapFrom(src => src.UpdatedAt));
    }
}
