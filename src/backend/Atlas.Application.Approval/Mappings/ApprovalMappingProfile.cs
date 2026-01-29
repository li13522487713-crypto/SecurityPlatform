using AutoMapper;
using Atlas.Application.Approval.Models;
using Atlas.Core.Abstractions;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Mappings;

/// <summary>
/// 审批流 AutoMapper 映射配置
/// </summary>
public sealed class ApprovalMappingProfile : Profile
{
    public ApprovalMappingProfile()
    {
        // ApprovalFlowDefinition 映射
        CreateMap<ApprovalFlowDefinitionCreateRequest, ApprovalFlowDefinition>()
            .ConstructUsing((src, ctx) =>
            {
                var tenantId = (Atlas.Core.Tenancy.TenantId)ctx.Items["TenantId"];
                var idGenerator = (IIdGenerator)ctx.Items["IdGenerator"];
                return new ApprovalFlowDefinition(tenantId, src.Name, src.DefinitionJson, idGenerator.NextId());
            })
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantIdValue, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.PublishedAt, opt => opt.Ignore())
            .ForMember(dest => dest.PublishedByUserId, opt => opt.Ignore())
            .ForMember(dest => dest.VisibilityScopeJson, opt => opt.Ignore())
            .ForMember(dest => dest.Category, opt => opt.Ignore())
            .ForMember(dest => dest.Description, opt => opt.Ignore())
            .ForMember(dest => dest.IsQuickEntry, opt => opt.Ignore());

        CreateMap<ApprovalFlowDefinition, ApprovalFlowDefinitionResponse>();

        CreateMap<ApprovalFlowDefinition, ApprovalFlowDefinitionListItem>();

        // ApprovalProcessInstance 映射
        CreateMap<ApprovalStartRequest, ApprovalProcessInstance>()
            .ConstructUsing((src, ctx) =>
            {
                var tenantId = (Atlas.Core.Tenancy.TenantId)ctx.Items["TenantId"];
                var idGenerator = (IIdGenerator)ctx.Items["IdGenerator"];
                var initiatorUserId = (long)ctx.Items["UserId"];
                return new ApprovalProcessInstance(
                    tenantId,
                    src.DefinitionId,
                    src.BusinessKey,
                    initiatorUserId,
                    idGenerator.NextId(),
                    src.DataJson);
            })
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantIdValue, opt => opt.Ignore())
            .ForMember(dest => dest.InitiatorUserId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.StartedAt, opt => opt.Ignore())
            .ForMember(dest => dest.EndedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CurrentNodeId, opt => opt.Ignore());

        CreateMap<ApprovalProcessInstance, ApprovalInstanceResponse>();

        // ApprovalTask 映射
        CreateMap<ApprovalTask, ApprovalTaskResponse>();

        // ApprovalDepartmentLeader 映射
        CreateMap<ApprovalDepartmentLeaderRequest, ApprovalDepartmentLeader>()
            .ConstructUsing((src, ctx) =>
            {
                var tenantId = (Atlas.Core.Tenancy.TenantId)ctx.Items["TenantId"];
                var idGenerator = (IIdGenerator)ctx.Items["IdGenerator"];
                return new ApprovalDepartmentLeader(tenantId, src.DepartmentId, src.LeaderUserId, idGenerator.NextId());
            })
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantIdValue, opt => opt.Ignore());
    }
}
