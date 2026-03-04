using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class LowCodeEnvironmentService : ILowCodeEnvironmentService
{
    private readonly ILowCodeAppRepository _appRepository;
    private readonly ILowCodeEnvironmentRepository _environmentRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public LowCodeEnvironmentService(
        ILowCodeAppRepository appRepository,
        ILowCodeEnvironmentRepository environmentRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _appRepository = appRepository;
        _environmentRepository = environmentRepository;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<LowCodeEnvironmentListItem>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var items = await _environmentRepository.GetByAppIdAsync(tenantId, appId, cancellationToken);
        return items.Select(x => new LowCodeEnvironmentListItem(
            x.Id.ToString(),
            x.AppId.ToString(),
            x.Name,
            x.Code,
            x.Description,
            x.IsDefault,
            x.IsActive,
            x.UpdatedAt)).ToArray();
    }

    public async Task<LowCodeEnvironmentDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var item = await _environmentRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return new LowCodeEnvironmentDetail(
            item.Id.ToString(),
            item.AppId.ToString(),
            item.Name,
            item.Code,
            item.Description,
            item.IsDefault,
            item.IsActive,
            item.VariablesJson,
            item.CreatedAt,
            item.UpdatedAt,
            item.CreatedBy,
            item.UpdatedBy);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        long appId,
        LowCodeEnvironmentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await _appRepository.GetByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={appId} 不存在");
        _ = app;

        if (await _environmentRepository.ExistsByCodeAsync(tenantId, appId, request.Code, null, cancellationToken))
        {
            throw new InvalidOperationException($"环境编码 {request.Code} 已存在");
        }

        var now = DateTimeOffset.UtcNow;
        if (request.IsDefault)
        {
            await _environmentRepository.ClearDefaultByAppIdAsync(tenantId, appId, cancellationToken);
        }

        var id = _idGenerator.NextId();
        var entity = new LowCodeEnvironment(
            tenantId,
            appId,
            request.Name,
            request.Code,
            request.Description,
            request.IsDefault,
            request.VariablesJson,
            userId,
            id,
            now);
        await _environmentRepository.InsertAsync(entity, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeEnvironmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _environmentRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"环境 ID={id} 不存在");
        if (request.IsDefault)
        {
            await _environmentRepository.ClearDefaultByAppIdAsync(tenantId, entity.AppId, cancellationToken);
        }

        entity.Update(
            request.Name,
            request.Description,
            request.IsDefault,
            request.VariablesJson,
            request.IsActive,
            userId,
            DateTimeOffset.UtcNow);
        await _environmentRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _environmentRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"环境 ID={id} 不存在");
        _ = userId;

        await _environmentRepository.DeleteAsync(tenantId, entity.Id, cancellationToken);
    }
}
