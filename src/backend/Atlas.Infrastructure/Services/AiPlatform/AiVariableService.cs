using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiVariableService : IAiVariableService
{
    private static readonly IReadOnlyList<AiSystemVariableDefinition> SystemVariables =
    [
        new("tenant.id", "租户ID", "当前请求租户标识。", null),
        new("user.id", "用户ID", "当前登录用户ID。", null),
        new("user.name", "用户名", "当前登录用户名。", null),
        new("project.id", "项目ID", "当前上下文项目ID。", null),
        new("now", "当前时间", "系统当前 UTC 时间。", null)
    ];

    private readonly AiVariableRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AiVariableService(AiVariableRepository repository, IIdGeneratorAccessor idGeneratorAccessor)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<AiVariableListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        AiVariableScope? scope,
        long? scopeId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            tenantId,
            keyword,
            scope,
            scopeId,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<AiVariableListItem>(
            items.Select(MapListItem).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<AiVariableDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<long> CreateAsync(TenantId tenantId, AiVariableCreateRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueKeyAsync(tenantId, request.Key, request.Scope, request.ScopeId, excludeId: null, cancellationToken);
        var entity = new AiVariable(
            tenantId,
            request.Key.Trim(),
            request.Value,
            request.Scope,
            request.ScopeId,
            _idGeneratorAccessor.NextId());
        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, AiVariableUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("变量不存在。", ErrorCodes.NotFound);
        await EnsureUniqueKeyAsync(tenantId, request.Key, request.Scope, request.ScopeId, id, cancellationToken);
        entity.Update(request.Key.Trim(), request.Value, request.Scope, request.ScopeId);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("变量不存在。", ErrorCodes.NotFound);
        await _repository.DeleteAsync(tenantId, entity.Id, cancellationToken);
    }

    public Task<IReadOnlyList<AiSystemVariableDefinition>> GetSystemVariableDefinitionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SystemVariables);
    }

    private async Task EnsureUniqueKeyAsync(
        TenantId tenantId,
        string key,
        AiVariableScope scope,
        long? scopeId,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var normalizedKey = key.Trim();
        var exists = await _repository.ExistsByKeyAsync(
            tenantId,
            normalizedKey,
            scope,
            scopeId,
            excludeId,
            cancellationToken);
        if (exists)
        {
            throw new BusinessException("同作用域下变量 Key 已存在。", ErrorCodes.ValidationError);
        }
    }

    private static AiVariableListItem MapListItem(AiVariable entity)
        => new(
            entity.Id,
            entity.Key,
            entity.Value,
            entity.Scope,
            entity.ScopeId,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static AiVariableDetail MapDetail(AiVariable entity)
        => new(
            entity.Id,
            entity.Key,
            entity.Value,
            entity.Scope,
            entity.ScopeId,
            entity.CreatedAt,
            entity.UpdatedAt);
}
