using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

/// <summary>
/// LowCodeApp 实体移除后，用 AppManifest / AppDataRoutePolicy 判断应用实例是否存在。
/// </summary>
public static class TenantAppInstanceGuard
{
    public static async Task EnsureExistsAsync(
        ISqlSugarClient db,
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        if (appId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "无效的应用实例。");
        }

        var hasManifest = await db.Queryable<AppManifest>()
            .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.Id == appId, cancellationToken);
        var hasPolicy = await db.Queryable<AppDataRoutePolicy>()
            .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appId, cancellationToken);
        if (!hasManifest && !hasPolicy)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用实例不存在。");
        }
    }

    public static async Task<bool> ExistsAsync(
        ISqlSugarClient db,
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        if (appId <= 0)
        {
            return false;
        }

        var hasManifest = await db.Queryable<AppManifest>()
            .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.Id == appId, cancellationToken);
        var hasPolicy = await db.Queryable<AppDataRoutePolicy>()
            .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appId, cancellationToken);
        return hasManifest || hasPolicy;
    }
}
