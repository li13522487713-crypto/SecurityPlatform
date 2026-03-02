using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class LowCodeAppCommandService : ILowCodeAppCommandService
{
    private readonly ILowCodeAppRepository _appRepository;
    private readonly ILowCodePageRepository _pageRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public LowCodeAppCommandService(
        ILowCodeAppRepository appRepository,
        ILowCodePageRepository pageRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _appRepository = appRepository;
        _pageRepository = pageRepository;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long userId, LowCodeAppCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _appRepository.ExistsByKeyAsync(tenantId, request.AppKey, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException($"应用标识 '{request.AppKey}' 已存在");
        }

        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;

        var entity = new LowCodeApp(
            tenantId, request.AppKey, request.Name,
            request.Description, request.Category, request.Icon,
            userId, id, now);

        await _appRepository.InsertAsync(entity, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId, long userId, long id, LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Update(request.Name, request.Description, request.Category, request.Icon, userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Publish(userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DisableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Disable(userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task EnableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Enable(userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        await _pageRepository.DeleteByAppIdAsync(id, cancellationToken);
        await _appRepository.DeleteAsync(id, cancellationToken);
    }
}
