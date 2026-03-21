using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class SystemConfigQueryService : ISystemConfigQueryService
{
    private readonly SystemConfigRepository _repository;

    public SystemConfigQueryService(SystemConfigRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<SystemConfigDto>> GetSystemConfigsPagedAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        var dtos = items.Select(ToDto).ToList();
        return new PagedResult<SystemConfigDto>(dtos, total, pageIndex, pageSize);
    }

    public async Task<SystemConfigDto?> GetByKeyAsync(
        TenantId tenantId, string configKey, CancellationToken cancellationToken)
    {
        var item = await _repository.FindByKeyAsync(tenantId, configKey, cancellationToken);
        return item is null ? null : ToDto(item);
    }

    public async Task<IReadOnlyList<SystemConfigDto>> GetFeatureFlagsAsync(
        TenantId tenantId, CancellationToken cancellationToken)
    {
        var all = await _repository.FindByTypeAsync(tenantId, "FeatureFlag", cancellationToken);
        return all.Select(ToDto).ToList();
    }

    private static SystemConfigDto ToDto(Atlas.Domain.System.Entities.SystemConfig x)
        => new(x.Id, x.ConfigKey, x.ConfigValue, x.ConfigName, x.IsBuiltIn, x.ConfigType, x.TargetJson, x.Remark);
}
