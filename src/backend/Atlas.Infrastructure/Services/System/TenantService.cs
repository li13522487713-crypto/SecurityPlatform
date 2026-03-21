using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class TenantService : ITenantService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public TenantService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(long userId, TenantCreateRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Queryable<Tenant>()
            .AnyAsync(x => x.Code == request.Code, cancellationToken);
            
        if (exists)
        {
            throw new BusinessException("VALIDATION_ERROR", $"租户编码 '{request.Code}' 已存在");
        }

        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;
        
        var tenant = new Tenant(id, request.Name, request.Code, userId, now);
        
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            tenant.Update(request.Name, request.Code, request.Description, userId, now);
        }

        if (request.AdminUserId.HasValue)
        {
            tenant.SetAdminUser(request.AdminUserId.Value);
        }

        await _db.Insertable(tenant).ExecuteCommandAsync(cancellationToken);
        return id;
    }

    public async Task UpdateAsync(long userId, TenantUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Queryable<Tenant>().InSingleAsync(request.Id)
            ?? throw new BusinessException("NOT_FOUND", "租户不存在");

        var exists = await _db.Queryable<Tenant>()
            .AnyAsync(x => x.Code == request.Code && x.Id != request.Id, cancellationToken);
            
        if (exists)
        {
            throw new BusinessException("VALIDATION_ERROR", $"租户编码 '{request.Code}' 已存在");
        }

        var now = DateTimeOffset.UtcNow;
        tenant.Update(request.Name, request.Code, request.Description, userId, now);
        
        if (request.AdminUserId.HasValue)
        {
            tenant.SetAdminUser(request.AdminUserId.Value);
        }

        await _db.Updateable(tenant).ExecuteCommandAsync(cancellationToken);
    }

    public async Task ToggleStatusAsync(long userId, long id, bool isActive, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Queryable<Tenant>().InSingleAsync(id)
            ?? throw new BusinessException("NOT_FOUND", "租户不存在");

        var now = DateTimeOffset.UtcNow;
        tenant.ToggleStatus(isActive, userId, now);

        await _db.Updateable(tenant).ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(long userId, long id, CancellationToken cancellationToken = default)
    {
        // 核心租户不允许删除
        if (id == 1 || id == 10000) 
        {
            throw new BusinessException("FORBIDDEN", "内置核心租户不允许删除");
        }

        var tenant = await _db.Queryable<Tenant>().InSingleAsync(id)
            ?? throw new BusinessException("NOT_FOUND", "租户不存在");

        await _db.Deleteable(tenant).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<TenantDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Queryable<Tenant>().InSingleAsync(id);
        if (tenant is null)
        {
            return null;
        }

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Code,
            tenant.Description,
            tenant.AdminUserId,
            tenant.IsActive,
            tenant.Status,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            tenant.ExpiredAt,
            tenant.TrialEndsAt);
    }

    public async Task<PagedResult<TenantDto>> GetPagedAsync(TenantQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<Tenant>()
            .WhereIF(!string.IsNullOrWhiteSpace(request.Keyword), 
                x => x.Name.Contains(request.Keyword!) || x.Code.Contains(request.Keyword!))
            .WhereIF(request.IsActive.HasValue, x => x.IsActive == request.IsActive!.Value);

        RefAsync<int> totalCount = 0;
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToPageListAsync(request.PageIndex, request.PageSize, totalCount, cancellationToken);

        var dtos = items.Select(x => new TenantDto(
            x.Id,
            x.Name,
            x.Code,
            x.Description,
            x.AdminUserId,
            x.IsActive,
            x.Status,
            x.CreatedAt,
            x.UpdatedAt,
            x.ExpiredAt,
            x.TrialEndsAt)).ToList();

        return new PagedResult<TenantDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }

    public async Task RenewAsync(long userId, long tenantId, DateTimeOffset newExpiredAt, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Queryable<Tenant>().FirstAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"租户 {tenantId} 不存在");

        tenant.Renew(newExpiredAt, userId, DateTimeOffset.UtcNow);
        await _db.Updateable(tenant).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> CheckAndSuspendExpiredTenantsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredTenants = await _db.Queryable<Tenant>()
            .Where(x => x.IsActive && x.ExpiredAt != null && x.ExpiredAt < now)
            .ToListAsync(cancellationToken);

        if (expiredTenants.Count == 0)
        {
            return 0;
        }

        foreach (var tenant in expiredTenants)
        {
            // 系统 userId=0 表示定时任务自动执行
            tenant.Suspend(0, now);
        }

        await _db.Updateable(expiredTenants)
            .UpdateColumns(x => new { x.IsActive, x.Status, x.UpdatedBy, x.UpdatedAt })
            .ExecuteCommandAsync(cancellationToken);

        return expiredTenants.Count;
    }
}
