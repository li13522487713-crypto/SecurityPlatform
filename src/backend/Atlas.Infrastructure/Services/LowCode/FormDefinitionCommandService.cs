using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class FormDefinitionCommandService : IFormDefinitionCommandService
{
    private readonly IFormDefinitionRepository _repository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public FormDefinitionCommandService(
        IFormDefinitionRepository repository,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
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
        var entity = await _repository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"表单定义 ID={id} 不存在");

        await _repository.DeleteAsync(id, cancellationToken);
    }
}
