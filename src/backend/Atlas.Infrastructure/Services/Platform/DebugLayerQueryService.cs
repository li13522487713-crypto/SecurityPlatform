using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Identity;
using Atlas.Application.Options;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Text.Json;
using TenantAppDataSourceBindingDto = Atlas.Application.Platform.Models.TenantAppDataSourceBinding;
using TenantAppDataSourceBindingEntity = Atlas.Domain.System.Entities.TenantAppDataSourceBinding;

namespace Atlas.Infrastructure.Services.Platform;


public sealed class DebugLayerQueryService : IDebugLayerQueryService
{
    private readonly ISqlSugarClient _db;

    public DebugLayerQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<DebugLayerEmbedMetadata> GetEmbedMetadataAsync(
        TenantId tenantId,
        long userId,
        string appId,
        long? projectId,
        bool projectScopeEnabled,
        CancellationToken cancellationToken = default)
    {
        var roleIds = await _db.Queryable<UserRole>()
            .Where(item => item.TenantIdValue == tenantId.Value && item.UserId == userId)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);
        var grantedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleIds.Count > 0)
        {
            var roleIdArray = roleIds.Distinct().ToArray();
            var permissionIds = await _db.Queryable<RolePermission>()
                .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(roleIdArray, item.RoleId))
                .Select(item => item.PermissionId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (permissionIds.Count > 0)
            {
                var permissionIdArray = permissionIds.ToArray();
                var permissionCodes = await _db.Queryable<Permission>()
                    .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(permissionIdArray, item.Id))
                    .Select(item => item.Code)
                    .ToListAsync(cancellationToken);
                grantedPermissions = permissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        var resourceDefinitions = new[]
        {
            new DebugLayerResourceItem("workflow-executions", "运行执行观测", PermissionCodes.DebugView, "查看执行状态、输入输出与错误消息"),
            new DebugLayerResourceItem("runtime-audit-trails", "运行审计追溯", PermissionCodes.DebugRun, "查看与执行ID关联的审计轨迹"),
            new DebugLayerResourceItem("rollback-ops", "回滚操作入口", PermissionCodes.DebugManage, "触发发布版本回滚操作")
        };
        var resources = resourceDefinitions
            .Where(item => grantedPermissions.Contains(item.RequiredPermission))
            .ToArray();

        return new DebugLayerEmbedMetadata(
            tenantId.ToString(),
            appId,
            projectId?.ToString(),
            projectScopeEnabled,
            resources);
    }
}
