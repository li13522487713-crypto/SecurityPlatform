using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using AutoMapper;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly ISqlSugarClient _db;
    private readonly IDataScopeFilter _dataScopeFilter;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IMapper _mapper;

    public AuditQueryService(
        ISqlSugarClient db,
        IDataScopeFilter dataScopeFilter,
        ICurrentUserAccessor currentUserAccessor,
        IMapper mapper)
    {
        _db = db;
        _dataScopeFilter = dataScopeFilter;
        _currentUserAccessor = currentUserAccessor;
        _mapper = mapper;
    }

    public async Task<PagedResult<AuditListItem>> QueryAuditsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var query = _db.Queryable<AuditRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        var ownerFilterId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        if (ownerFilterId.HasValue)
        {
            var currentUser = _currentUserAccessor.GetCurrentUser();
            if (currentUser is null)
            {
                query = query.Where(x => false);
            }
            else
            {
                var actorId = currentUser.UserId.ToString();
                var actorName = currentUser.Username;
                query = query.Where(x => x.Actor == actorId || x.Actor == actorName);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Action.Contains(keyword) || x.Actor.Contains(keyword) || x.Target.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.OccurredAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<AuditListItem>(x)).ToArray();
        return new PagedResult<AuditListItem>(resultItems, total, pageIndex, pageSize);
    }
}
