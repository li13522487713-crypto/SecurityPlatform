using AutoMapper;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.Audit.Entities;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批流定义命令服务实现
/// </summary>
public sealed class ApprovalFlowCommandService : IApprovalFlowCommandService
{
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalFlowDefinitionVersionRepository _versionRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IAuditWriter _auditWriter;
    private readonly IMapper _mapper;

    public ApprovalFlowCommandService(
        IApprovalFlowRepository flowRepository,
        IApprovalFlowDefinitionVersionRepository versionRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAuditWriter auditWriter,
        IMapper mapper)
    {
        _flowRepository = flowRepository;
        _versionRepository = versionRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _auditWriter = auditWriter;
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

        // 发布时创建版本快照
        var versionId = _idGeneratorAccessor.NextId();
        var version = new ApprovalFlowDefinitionVersion(
            tenantId,
            entity.Id,
            entity.Version,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.DefinitionJson,
            entity.VisibilityScopeJson,
            publishedByUserId,
            versionId,
            DateTimeOffset.UtcNow);
        await _versionRepository.InsertAsync(version, cancellationToken);

        // 设计态审计埋点
        var auditRecord = new AuditRecord(
            tenantId,
            publishedByUserId.ToString(),
            "Approval.FlowDefinition.Published",
            "Success",
            $"ApprovalFlowDefinition:{flowId}",
            null,
            null);
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);
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

    public async Task<ApprovalFlowDefinitionResponse> CopyAsync(
        TenantId tenantId,
        long id,
        ApprovalFlowCopyRequest request,
        CancellationToken cancellationToken)
    {
        var source = await _flowRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (source == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "审批流定义不存在");
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{source.Name}-副本"
            : request.Name.Trim();

        var entity = new ApprovalFlowDefinition(
            tenantId,
            name,
            source.DefinitionJson,
            _idGeneratorAccessor.NextId());
        entity.SetMetadata(source.Description, source.Category, source.VisibilityScopeJson, source.IsQuickEntry);
        await _flowRepository.AddAsync(entity, cancellationToken);
        return _mapper.Map<ApprovalFlowDefinitionResponse>(entity);
    }

    public async Task<ApprovalFlowDefinitionResponse> ImportAsync(
        TenantId tenantId,
        ApprovalFlowImportRequest request,
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

    public async Task RollbackToVersionAsync(
        TenantId tenantId,
        long flowId,
        long versionId,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        var entity = await _flowRepository.GetByIdAsync(tenantId, flowId, cancellationToken)
            ?? throw new BusinessException("FLOW_NOT_FOUND", "审批流定义不存在");

        var versionSnapshot = await _versionRepository.GetByIdAsync(tenantId, versionId, cancellationToken)
            ?? throw new BusinessException("FLOW_VERSION_NOT_FOUND", "版本快照不存在");

        if (versionSnapshot.DefinitionId != flowId)
        {
            throw new BusinessException("FLOW_VERSION_MISMATCH", "版本不属于该审批流定义");
        }

        // 用快照恢复定义 JSON 并重新发布
        entity.Update(versionSnapshot.Name, versionSnapshot.DefinitionJson, versionSnapshot.Description, versionSnapshot.Category, versionSnapshot.VisibilityScopeJson);
        entity.Publish(operatorUserId, DateTimeOffset.UtcNow);
        await _flowRepository.UpdateAsync(entity, cancellationToken);

        // 回滚发布也创建版本快照
        var newVersionId = _idGeneratorAccessor.NextId();
        var newVersion = new ApprovalFlowDefinitionVersion(
            tenantId,
            entity.Id,
            entity.Version,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.DefinitionJson,
            entity.VisibilityScopeJson,
            operatorUserId,
            newVersionId,
            DateTimeOffset.UtcNow);
        await _versionRepository.InsertAsync(newVersion, cancellationToken);

        // 设计态审计埋点
        var rollbackAudit = new AuditRecord(
            tenantId,
            operatorUserId.ToString(),
            "Approval.FlowDefinition.RolledBack",
            "Success",
            $"ApprovalFlowDefinition:{flowId}:Version:{versionId}",
            null,
            null);
        await _auditWriter.WriteAsync(rollbackAudit, cancellationToken);
    }

    public async Task DeprecateAsync(
        TenantId tenantId,
        long id,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        var definition = await _flowRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"审批流定义 {id} 不存在");

        if (definition.IsDeprecated)
            return; // 幂等：已弃用则忽略

        definition.Deprecate(operatorUserId, DateTimeOffset.UtcNow);
        await _flowRepository.UpdateAsync(definition, cancellationToken);

        var auditRecord = new AuditRecord(
            tenantId,
            operatorUserId.ToString(),
            "Approval.FlowDefinition.Deprecated",
            "Success",
            $"ApprovalFlowDefinition:{id}",
            null,
            null);
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);
    }
}
