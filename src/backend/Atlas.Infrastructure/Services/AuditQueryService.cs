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
    private const string ScopeAll = "all";
    private const string ScopeMine = "mine";

    private readonly ISqlSugarClient _db;
    private readonly ITenantDataScopeFilter _dataScopeFilter;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IResourceVisibilityResolver _visibilityResolver;
    private readonly IMapper _mapper;

    public AuditQueryService(
        ISqlSugarClient db,
        ITenantDataScopeFilter dataScopeFilter,
        ICurrentUserAccessor currentUserAccessor,
        IUserDepartmentRepository userDepartmentRepository,
        IUserAccountRepository userAccountRepository,
        IResourceVisibilityResolver visibilityResolver,
        IMapper mapper)
    {
        _db = db;
        _dataScopeFilter = dataScopeFilter;
        _currentUserAccessor = currentUserAccessor;
        _userDepartmentRepository = userDepartmentRepository;
        _userAccountRepository = userAccountRepository;
        _visibilityResolver = visibilityResolver;
        _mapper = mapper;
    }

    public async Task<PagedResult<AuditListItem>> QueryAuditsAsync(
        PagedRequest request,
        TenantId tenantId,
        string? action,
        string? result,
        CancellationToken cancellationToken,
        string? scope = null)
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

        var visible = await ApplyVisibilityFilterAsync(items, tenantId, scope, cancellationToken);
        var resultItems = visible.Select(x => _mapper.Map<AuditListItem>(x)).ToArray();
        // 治理 R1-B2：page total 扣减不可见行数，避免分页 UI 显示 "看不到的总数"
        var adjustedTotal = total - (items.Count - visible.Count);
        return new PagedResult<AuditListItem>(resultItems, adjustedTotal < 0 ? 0 : adjustedTotal, pageIndex, pageSize);
    }

    public async Task<PagedResult<AuditListItem>> QueryAuditsByResourceAsync(
        PagedRequest request,
        TenantId tenantId,
        string? actorId,
        string? action,
        string? resourceId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken,
        string? scope = null)
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

        var visible = await ApplyVisibilityFilterAsync(items, tenantId, scope, cancellationToken);
        var resultItems = visible.Select(x => _mapper.Map<AuditListItem>(x)).ToArray();
        var adjustedTotal = total - (items.Count - visible.Count);
        return new PagedResult<AuditListItem>(resultItems, adjustedTotal < 0 ? 0 : adjustedTotal, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<AuditListItem>> ExportAuditsCsvAsync(
        TenantId tenantId,
        string? action,
        string? result,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int maxRows,
        CancellationToken cancellationToken,
        string? scope = null)
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

        var visible = await ApplyVisibilityFilterAsync(items, tenantId, scope, cancellationToken);
        return visible.Select(x => _mapper.Map<AuditListItem>(x)).ToArray();
    }

    /// <summary>
    /// 治理 R1-B2：按 (ResourceType, ResourceId) 收口可见性。
    /// scope=all 且当前用户允许 bypass（platform admin 或 system admin 自动允许）→ 不过滤；
    /// scope=mine（默认）或 user 不允许 → 调 IResourceVisibilityResolver 过滤。
    /// 记录的 (ResourceType, ResourceId) 缺失（旧数据 / 行为级审计）→ 保留行为兼容。
    /// </summary>
    private async Task<List<AuditRecord>> ApplyVisibilityFilterAsync(
        IReadOnlyList<AuditRecord> items,
        TenantId tenantId,
        string? scope,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return new List<AuditRecord>(0);
        }

        var currentUser = _currentUserAccessor.GetCurrentUser();
        var isAdmin = currentUser?.IsPlatformAdmin == true
            || (currentUser?.Roles?.Any(r => string.Equals(r, "system-admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "platform-admin", StringComparison.OrdinalIgnoreCase)) ?? false);

        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? ScopeMine : scope.Trim().ToLowerInvariant();
        if (normalizedScope == ScopeAll && isAdmin)
        {
            return items.ToList();
        }

        // 提取所有 (ResourceType, ResourceId) 候选；为空则该行不参与过滤（保留）
        var candidates = items
            .Where(x => !string.IsNullOrWhiteSpace(x.ResourceType) && !string.IsNullOrWhiteSpace(x.ResourceId))
            .Select(x => (ResourceType: x.ResourceType!, ResourceId: x.ResourceId!))
            .Distinct()
            .ToArray();

        if (candidates.Length == 0)
        {
            // 全部行都没有资源 tag → 沿用 owner/dept scope 的过滤结果，不再做资源级收口
            return items.ToList();
        }

        var visibleSet = await _visibilityResolver.FilterVisibleAsync(
            tenantId,
            currentUser?.UserId ?? 0,
            isAdmin,
            candidates,
            cancellationToken);

        var visibleHashSet = visibleSet
            .Select(t => $"{t.ResourceType}|{t.ResourceId}")
            .ToHashSet(StringComparer.Ordinal);

        return items
            .Where(x =>
                string.IsNullOrWhiteSpace(x.ResourceType)
                || string.IsNullOrWhiteSpace(x.ResourceId)
                || visibleHashSet.Contains($"{x.ResourceType}|{x.ResourceId}"))
            .ToList();
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
