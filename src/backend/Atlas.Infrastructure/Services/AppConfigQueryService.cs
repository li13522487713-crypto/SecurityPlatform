using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Infrastructure.Services;

public sealed class AppConfigQueryService : IAppConfigQueryService
{
    private readonly IAppConfigRepository _repository;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan AppConfigCacheTtl = TimeSpan.FromMinutes(2);

    public AppConfigQueryService(IAppConfigRepository repository, IMapper mapper, IMemoryCache cache)
    {
        _repository = repository;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<PagedResult<AppConfigListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _repository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<AppConfigListItem>(x)).ToArray();
        return new PagedResult<AppConfigListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<AppConfigDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var appConfig = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        return appConfig is null ? null : _mapper.Map<AppConfigDetail>(appConfig);
    }

    public async Task<AppConfigDetail?> GetByAppIdAsync(string appId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var cacheKey = $"app_config_{tenantId.Value:D}_{appId}";
        if (_cache.TryGetValue(cacheKey, out AppConfigDetail? cached))
        {
            return cached;
        }

        var appConfig = await _repository.FindByAppIdAsync(tenantId, appId, cancellationToken);
        var result = appConfig is null ? null : _mapper.Map<AppConfigDetail>(appConfig);

        _cache.Set(cacheKey, result, AppConfigCacheTtl);
        return result;
    }
}
