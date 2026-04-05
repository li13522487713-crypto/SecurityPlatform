using Atlas.Application.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
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
    private readonly ITenantDataScopeFilter _dataScopeFilter;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IMapper _mapper;

    public AuditQueryService(
        ISqlSugarClient db,
        ITenantDataScopeFilter dataScopeFilter,
        ICurrentUserAccessor currentUserAccessor,
        IUserDepartmentRepository userDepartmentRepository,
        IUserAccountRepository userAccountRepository,
        IMapper mapper)
    {
        _db = db;
        _dataScopeFilter = dataScopeFilter;
        _currentUserAccessor = currentUserAccessor;
        _userDepartmentRepository = userDepartmentRepository;
        _userAccountRepository = userAccountRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<AuditListItem>> QueryAuditsAsync(
        PagedRequest request,
        TenantId tenantId,
        string? action,
        string? result,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var query = _db.Queryable<AuditRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        query = await ApplyOwnerAndDeptScopeToAuditQueryAsync(query, tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Action.Contains(keyword) || x.Actor.Contains(keyword) || x.Target.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            query = query.Where(x => x.Result == result);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.OccurredAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<AuditListItem>(x)).ToArray();
        return new PagedResult<AuditListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<AuditListItem>> QueryAuditsByResourceAsync(
        PagedRequest request,
        TenantId tenantId,
        string? actorId,
        string? action,
        string? resourceId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var query = _db.Queryable<AuditRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        query = await ApplyOwnerAndDeptScopeToAuditQueryAsync(query, tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(actorId))
        {
            query = query.Where(x => x.Actor == actorId);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            query = query.Where(x => x.Target.Contains(resourceId));
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value;
            query = query.Where(x => x.OccurredAt >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value;
            query = query.Where(x => x.OccurredAt <= to);
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

    public async Task<IReadOnlyList<AuditListItem>> ExportAuditsCsvAsync(
        TenantId tenantId,
        string? action,
        string? result,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(maxRows, 1, 50000);
        var query = _db.Queryable<AuditRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        query = await ApplyOwnerAndDeptScopeToAuditQueryAsync(query, tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }
        if (!string.IsNullOrWhiteSpace(result))
        {
            query = query.Where(x => x.Result == result);
        }
        if (fromDate.HasValue)
        {
            var from = fromDate.Value;
            query = query.Where(x => x.OccurredAt >= from);
        }
        if (toDate.HasValue)
        {
            var to = toDate.Value;
            query = query.Where(x => x.OccurredAt <= to);
        }

        var items = await query
            .OrderBy(x => x.OccurredAt, OrderByType.Desc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(x => _mapper.Map<AuditListItem>(x)).ToArray();
    }

    private async Task<ISugarQueryable<AuditRecord>> ApplyOwnerAndDeptScopeToAuditQueryAsync(
        ISugarQueryable<AuditRecord> query,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var ownerFilterId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        if (ownerFilterId.HasValue)
        {
            var currentUser = _currentUserAccessor.GetCurrentUser();
            if (currentUser is null)
            {
                return query.Where(x => false);
            }

            var actorId = currentUser.UserId.ToString();
            if (!string.IsNullOrWhiteSpace(currentUser.Username)
                && !long.TryParse(currentUser.Username, out _))
            {
                var actorName = currentUser.Username;
                query = query.Where(x => x.Actor == actorId || x.Actor == actorName);
            }
            else
            {
                query = query.Where(x => x.Actor == actorId);
            }
        }

        var deptFilterIds = await _dataScopeFilter.GetDeptFilterIdsAsync(cancellationToken);
        if (deptFilterIds is null)
        {
            return query;
        }

        if (deptFilterIds.Count == 0)
        {
            return query.Where(x => false);
        }

        var userIds = await _userDepartmentRepository.QueryUserIdsByDepartmentIdsAsync(
            tenantId,
            deptFilterIds,
            cancellationToken);
        if (userIds.Count == 0)
        {
            return query.Where(x => false);
        }

        var accounts = await _userAccountRepository.QueryByIdsAsync(tenantId, userIds, cancellationToken);
        var actorTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in accounts)
        {
            actorTokens.Add(a.Id.ToString());
            if (!string.IsNullOrWhiteSpace(a.Username) && !long.TryParse(a.Username, out _))
            {
                actorTokens.Add(a.Username);
            }
        }

        var actorArray = actorTokens.ToArray();
        if (actorArray.Length == 0)
        {
            return query.Where(x => false);
        }

        return query.Where(x => SqlFunc.ContainsArray(actorArray, x.Actor));
    }
}
