using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AppConfigQueryService : IAppConfigQueryService
{
    private readonly IAppConfigRepository _repository;
    private readonly IMapper _mapper;

    public AppConfigQueryService(IAppConfigRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
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
        var appConfig = await _repository.FindByAppIdAsync(tenantId, appId, cancellationToken);
        return appConfig is null ? null : _mapper.Map<AppConfigDetail>(appConfig);
    }
}
