using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class FormDefinitionCommandService : IFormDefinitionCommandService
{
    private readonly IFormDefinitionRepository _repository;
    private readonly IFormDefinitionVersionRepository _versionRepository;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IAuditWriter _auditWriter;

    public FormDefinitionCommandService(
        IFormDefinitionRepository repository,
        IFormDefinitionVersionRepository versionRepository,
        IIdGeneratorAccessor idGenerator,
        IAuditWriter auditWriter)
    {
        _repository = repository;
        _versionRepository = versionRepository;
        _idGenerator = idGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long userId, FormDefinitionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.ExistsByNameAsync(tenantId, request.Name, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException($"表单名称 '{request.Name}' 已存在");
        }

        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;

        var entity = new FormDefinition(
            tenantId,
            request.Name,
            request.Description,
            request.Category,
            request.SchemaJson,
            userId,
            id,
            now);

        if (!string.IsNullOrWhiteSpace(request.DataTableKey))
        {
            entity.BindDataTable(request.DataTableKey, userId, now);
        }

        if (!string.IsNullOrWhiteSpace(request.Icon))
        {
            entity.SetIcon(request.Icon, userId, now);
        }

        await _repository.InsertAsync(entity, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId, long userId, long id, FormDefinitionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        if (await _repository.ExistsByNameAsync(tenantId, request.Name, id, cancellationToken))
        {
            throw new InvalidOperationException($"表单名称 '{request.Name}' 已存在");
        }

        var now = DateTimeOffset.UtcNow;
        entity.Update(request.Name, request.Description, request.Category, request.SchemaJson, userId, now);

        if (request.DataTableKey != entity.DataTableKey)
        {
            entity.BindDataTable(request.DataTableKey ?? string.Empty, userId, now);
        }

        entity.SetIcon(request.Icon, userId, now);

        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UpdateSchemaAsync(
        TenantId tenantId, long userId, long id, FormDefinitionSchemaUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.UpdateSchema(request.SchemaJson, userId, now);

        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Publish(userId, now);

        await _repository.UpdateAsync(entity, cancellationToken);

        // 发布时创建版本快照
        var versionId = _idGenerator.NextId();
        var version = new FormDefinitionVersion(
            tenantId,
            entity.Id,
            entity.Version,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.SchemaJson,
            entity.DataTableKey,
            entity.Icon,
            userId,
            versionId,
            now);

        await _versionRepository.InsertAsync(version, cancellationToken);

        // 设计态审计埋点
        var auditRecord = new AuditRecord(
            tenantId,
            userId.ToString(),
            "LowCode.FormDefinition.Published",
            "Success",
            $"FormDefinition:{id}",
            null,
            null);
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);
    }

    public async Task DisableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Disable(userId, now);

        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task EnableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Enable(userId, now);

        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        _ = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        await _versionRepository.DeleteByFormDefinitionIdAsync(tenantId, id, cancellationToken);
        await _repository.DeleteAsync(id, cancellationToken);
    }

    public async Task RollbackToVersionAsync(
        TenantId tenantId, long userId, long id, long versionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        var versionSnapshot = await _versionRepository.GetByIdAsync(tenantId, versionId, cancellationToken)
            ?? throw new InvalidOperationException($"版本 ID={versionId} 不存在");

        if (versionSnapshot.FormDefinitionId != id)
        {
            throw new InvalidOperationException("版本不属于该表单定义");
        }

        var now = DateTimeOffset.UtcNow;
        // 用快照 Schema 覆盖当前 Schema 并重新发布
        entity.UpdateSchema(versionSnapshot.SchemaJson, userId, now);
        entity.Publish(userId, now);

        await _repository.UpdateAsync(entity, cancellationToken);

        // 回滚发布也创建一次版本快照（记录回滚操作）
        var newVersionId = _idGenerator.NextId();
        var newVersion = new FormDefinitionVersion(
            tenantId,
            entity.Id,
            entity.Version,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.SchemaJson,
            entity.DataTableKey,
            entity.Icon,
            userId,
            newVersionId,
            now);

        await _versionRepository.InsertAsync(newVersion, cancellationToken);

        // 设计态审计埋点
        var auditRecord = new AuditRecord(
            tenantId,
            userId.ToString(),
            "LowCode.FormDefinition.RolledBack",
            "Success",
            $"FormDefinition:{id}:Version:{versionId}",
            null,
            null);
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);
    }

    public async Task DeprecateAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"表单定义 {id} 不存在");

        if (entity.IsDeprecated)
            return;

        entity.Deprecate(userId, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, cancellationToken);

        var auditRecord = new AuditRecord(
            tenantId,
            userId.ToString(),
            "LowCode.FormDefinition.Deprecated",
            "Success",
            $"FormDefinition:{id}",
            null,
            null);
        await _auditWriter.WriteAsync(auditRecord, cancellationToken);
    }
}
