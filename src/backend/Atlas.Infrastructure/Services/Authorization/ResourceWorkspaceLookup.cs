using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Authorization;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Authorization;

/// <summary>
/// 治理 R1-B4 默认实现：按 resourceType 选表，单字段 SELECT 取 WorkspaceId。
/// 复杂度：O(1) 查询；命中索引 (Id + TenantIdValue) 即可。
/// </summary>
public sealed class ResourceWorkspaceLookup : IResourceWorkspaceLookup
{
    private readonly ISqlSugarClient _db;

    public ResourceWorkspaceLookup(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long?> ResolveWorkspaceIdAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || resourceId <= 0)
        {
            return null;
        }

        var tenantValue = tenantId.Value;
        var normalizedType = resourceType.Trim().ToLowerInvariant();

        try
        {
            return normalizedType switch
            {
                "agent" => await _db.Queryable<Agent>()
                    .Where(x => x.TenantIdValue == tenantValue && x.Id == resourceId)
                    .Select(x => x.WorkspaceId)
                    .FirstAsync(cancellationToken),
                "workflow" => await _db.Queryable<WorkflowMeta>()
                    .Where(x => x.TenantIdValue == tenantValue && x.Id == resourceId)
                    .Select(x => x.WorkspaceId)
                    .FirstAsync(cancellationToken),
                "app" => await ResolveAppWorkspaceIdAsync(tenantValue, resourceId, cancellationToken),
                "knowledge" or "knowledge-base" or "kb" => await _db.Queryable<KnowledgeBase>()
                    .Where(x => x.TenantIdValue == tenantValue && x.Id == resourceId)
                    .Select(x => x.WorkspaceId)
                    .FirstAsync(cancellationToken),
                "database" or "ai-database" => await _db.Queryable<AiDatabase>()
                    .Where(x => x.TenantIdValue == tenantValue && x.Id == resourceId)
                    .Select(x => x.WorkspaceId)
                    .FirstAsync(cancellationToken),
                "plugin" or "ai-plugin" => await _db.Queryable<AiPlugin>()
                    .Where(x => x.TenantIdValue == tenantValue && x.Id == resourceId)
                    .Select(x => x.WorkspaceId)
                    .FirstAsync(cancellationToken),
                _ => null
            };
        }
        catch (Exception)
        {
            // 资源不存在或 schema 不一致时返回 null 让 controller 走 fallback skip
            return null;
        }
    }

    private async Task<long?> ResolveAppWorkspaceIdAsync(
        Guid tenantValue,
        long resourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var lowcodeWorkspaceId = await _db.Queryable<AppDefinition>()
                .Where(x => x.TenantIdValue == tenantValue && x.Id == resourceId)
                .Select(x => x.WorkspaceId)
                .FirstAsync(cancellationToken);

            if (long.TryParse(lowcodeWorkspaceId, out var parsedLowcodeWorkspaceId) && parsedLowcodeWorkspaceId > 0)
            {
                return parsedLowcodeWorkspaceId;
            }
        }
        catch (Exception)
        {
            // Ignore and fall through to legacy AiApp lookup.
        }

        return await _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantValue && x.Id == resourceId)
            .Select(x => x.WorkspaceId)
            .FirstAsync(cancellationToken);
    }
}
