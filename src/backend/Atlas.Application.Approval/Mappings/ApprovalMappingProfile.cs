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
                var idGeneratorAccessor = (IIdGeneratorAccessor)ctx.Items["IdGeneratorAccessor"];
                return new ApprovalFlowDefinition(
                    tenantId,
                    src.Name,
                    src.DefinitionJson,
                    idGeneratorAccessor.NextId());
            })
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantIdValue, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.PublishedAt, opt => opt.Ignore())
            .ForMember(dest => dest.PublishedByUserId, opt => opt.Ignore())
            .ForMember(dest => dest.FlowKind, opt => opt.Ignore())
            .ForMember(dest => dest.VisibilityScopeJson, opt => opt.Ignore())
            .ForMember(dest => dest.Category, opt => opt.Ignore())
            .ForMember(dest => dest.Description, opt => opt.Ignore())
            .ForMember(dest => dest.IsQuickEntry, opt => opt.Ignore())
            .ForMember(dest => dest.DeprecatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.DeprecatedByUserId, opt => opt.Ignore());

        CreateMap<ApprovalFlowDefinition, ApprovalFlowDefinitionResponse>();

        CreateMap<ApprovalFlowDefinition, ApprovalFlowDefinitionListItem>();

        // ApprovalProcessInstance 映射
        CreateMap<ApprovalStartRequest, ApprovalProcessInstance>()
            .ConstructUsing((src, ctx) =>
            {
                var tenantId = (Atlas.Core.Tenancy.TenantId)ctx.Items["TenantId"];
                var idGeneratorAccessor = (IIdGeneratorAccessor)ctx.Items["IdGeneratorAccessor"];
                var initiatorUserId = (long)ctx.Items["UserId"];
                return new ApprovalProcessInstance(
                    tenantId,
                    src.DefinitionId,
                    src.BusinessKey,
                    initiatorUserId,
                    idGeneratorAccessor.NextId(),
                    src.DataJson);
            })
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantIdValue, opt => opt.Ignore())
            .ForMember(dest => dest.InitiatorUserId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.StartedAt, opt => opt.Ignore())
            .ForMember(dest => dest.EndedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CurrentNodeId, opt => opt.Ignore())
            .ForMember(dest => dest.ParentInstanceId, opt => opt.Ignore())
            .ForMember(dest => dest.Priority, opt => opt.Ignore())
            .ForMember(dest => dest.InstanceNo, opt => opt.Ignore())
            .ForMember(dest => dest.CurrentNodeName, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<ApprovalProcessInstance, ApprovalInstanceResponse>()
            .ForMember(dest => dest.FlowName, opt => opt.Ignore())
            .ForMember(dest => dest.SlaRemainingMinutes, opt => opt.Ignore())
            .ForMember(dest => dest.ExpectedCompleteTime, opt => opt.Ignore());

        // ApprovalTask 映射
        CreateMap<ApprovalTask, ApprovalTaskResponse>()
            .ForMember(dest => dest.FlowName, opt => opt.Ignore())
            .ForMember(dest => dest.CurrentNodeName, opt => opt.Ignore())
            .ForMember(dest => dest.SlaRemainingMinutes, opt => opt.Ignore())
            .ForMember(dest => dest.ExpectedCompleteTime, opt => opt.Ignore());

        // ApprovalDepartmentLeader 映射
        CreateMap<ApprovalDepartmentLeaderRequest, ApprovalDepartmentLeader>()
            .ConstructUsing((src, ctx) =>
            {
                var tenantId = (Atlas.Core.Tenancy.TenantId)ctx.Items["TenantId"];
                var idGeneratorAccessor = (IIdGeneratorAccessor)ctx.Items["IdGeneratorAccessor"];
                return new ApprovalDepartmentLeader(
                    tenantId,
                    src.DepartmentId,
                    src.LeaderUserId,
                    idGeneratorAccessor.NextId());
            })
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantIdValue, opt => opt.Ignore());
    }
}
