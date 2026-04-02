using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Domain.LogicFlow.Expressions;
using AutoMapper;

namespace Atlas.Application.LogicFlow.Expressions.Mappings;

public sealed class ExpressionMappingProfile : Profile
{
    public ExpressionMappingProfile()
    {
        CreateMap<FunctionDefinition, FunctionDefinitionResponse>();
        CreateMap<FunctionDefinition, FunctionDefinitionListItem>();

        CreateMap<DecisionTableDefinition, DecisionTableResponse>();
        CreateMap<DecisionTableDefinition, DecisionTableListItem>();

        CreateMap<RuleChainDefinition, RuleChainResponse>();
        CreateMap<RuleChainDefinition, RuleChainListItem>();
    }
}
