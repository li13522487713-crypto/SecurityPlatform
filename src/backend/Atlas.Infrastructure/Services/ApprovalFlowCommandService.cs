using AutoMapper;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批流定义命令服务实现
/// </summary>
public sealed class ApprovalFlowCommandService : IApprovalFlowCommandService
{
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IMapper _mapper;

    public ApprovalFlowCommandService(
        IApprovalFlowRepository flowRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IMapper mapper)
    {
        _flowRepository = flowRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _mapper = mapper;
    }

    public async Task<ApprovalFlowDefinitionResponse> CreateAsync(
        TenantId tenantId,
        ApprovalFlowDefinitionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = new ApprovalFlowDefinition(
            tenantId,
            request.Name,
            request.DefinitionJson,
            _idGeneratorAccessor.NextId());
        entity.SetMetadata(request.Description, request.Category, request.VisibilityScopeJson, request.IsQuickEntry);
        await _flowRepository.AddAsync(entity, cancellationToken);

        return _mapper.Map<ApprovalFlowDefinitionResponse>(entity);
    }

    public async Task<ApprovalFlowDefinitionResponse> UpdateAsync(
        TenantId tenantId,
        ApprovalFlowDefinitionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _flowRepository.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "审批流定义不存在");
        }

        if (entity.Status != ApprovalFlowStatus.Draft)
        {
            throw new BusinessException("FLOW_NOT_DRAFT", "仅可编辑草稿状态的流程定义");
        }

        entity.Update(request.Name, request.DefinitionJson, request.Description, request.Category, request.VisibilityScopeJson);
        entity.SetQuickEntry(request.IsQuickEntry);
        await _flowRepository.UpdateAsync(entity, cancellationToken);

        return _mapper.Map<ApprovalFlowDefinitionResponse>(entity);
    }

    public async Task PublishAsync(
        TenantId tenantId,
        long flowId,
        long publishedByUserId,
        CancellationToken cancellationToken)
    {
        var entity = await _flowRepository.GetByIdAsync(tenantId, flowId, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "审批流定义不存在");
        }

        if (entity.Status == ApprovalFlowStatus.Published)
        {
            throw new BusinessException("FLOW_ALREADY_PUBLISHED", "该流程定义已发布");
        }

        entity.Publish(publishedByUserId, DateTimeOffset.UtcNow);
        await _flowRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _flowRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "审批流定义不存在");
        }

        if (entity.Status != ApprovalFlowStatus.Draft)
        {
            throw new BusinessException("FLOW_NOT_DRAFT", "仅可删除草稿状态的流程定义");
        }

        await _flowRepository.DeleteAsync(tenantId, id, cancellationToken);
    }

    public async Task DisableAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _flowRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "审批流定义不存在");
        }

        entity.Disable();
        await _flowRepository.UpdateAsync(entity, cancellationToken);
    }
}




