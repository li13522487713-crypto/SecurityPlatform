using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Authorization;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.Authorization;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Authorization;

/// <summary>
/// 覆盖 M-G03-C2：ResourceAccessGuard 三级合并判定。
/// </summary>
public sealed class ResourceAccessGuardTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000010"));
    private const long WorkspaceId = 100100;
    private const long UserId = 9527;
    private const long EditorRoleId = 200001;
    private const long ViewerRoleId = 200002;

    [Fact]
    public async Task PlatformAdmin_ShouldAlwaysAllow()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            var guard = BuildGuard(db, isPlatformAdmin: true);

            var dec = await guard.CheckAsync(new ResourceAccessQuery(
                Tenant, UserId, IsPlatformAdmin: true, WorkspaceId,
                ResourceType: "agent", ResourceId: 9999, Action: "delete"), CancellationToken.None);

            Assert.True(dec.Allowed);
            Assert.Equal("platform", dec.GrantingTier);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task NonMember_ShouldBeDenied()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            var guard = BuildGuard(db);

            var dec = await guard.CheckAsync(new ResourceAccessQuery(
                Tenant, UserId: 8888 /* not member */, false, WorkspaceId,
                "agent", null, "view"), CancellationToken.None);

            Assert.False(dec.Allowed);
            Assert.Equal("NotWorkspaceMember", dec.DeniedReason);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task WorkspaceRole_ShouldAllow_WhenActionInDefaultActions()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            await SeedMember(db, EditorRoleId);
            var guard = BuildGuard(db);

            var dec = await guard.CheckAsync(new ResourceAccessQuery(
                Tenant, UserId, false, WorkspaceId,
                "agent", null, "edit"), CancellationToken.None);

            Assert.True(dec.Allowed);
            Assert.Equal("workspace", dec.GrantingTier);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task WorkspaceRole_ShouldDeny_WhenActionMissing()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            await SeedMember(db, ViewerRoleId);
            var guard = BuildGuard(db);

            var dec = await guard.CheckAsync(new ResourceAccessQuery(
                Tenant, UserId, false, WorkspaceId,
                "agent", null, "edit"), CancellationToken.None);

            Assert.False(dec.Allowed);
            Assert.Equal("WorkspaceRoleDenied", dec.DeniedReason);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task ResourceAcl_ShouldOverrideWorkspaceRole_WhenSpecified()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            await SeedMember(db, ViewerRoleId);
            // viewer 默认无 edit；但资源 ACL 给该 viewer 角色显式赋了 edit。
            await SeedResourceAcl(db, ViewerRoleId, "agent", 5001, actionsJson: "[\"view\",\"edit\"]");
            var guard = BuildGuard(db);

            var dec = await guard.CheckAsync(new ResourceAccessQuery(
                Tenant, UserId, false, WorkspaceId,
                "agent", 5001, "edit"), CancellationToken.None);

            Assert.True(dec.Allowed);
            Assert.Equal("resource", dec.GrantingTier);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task ResourceAcl_PresentButMissingAction_ShouldDeny_NotFallback()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            await SeedMember(db, EditorRoleId);
            // editor 默认有 edit；但资源 ACL 收紧为只 view，则 edit 应被拒。
            await SeedResourceAcl(db, EditorRoleId, "agent", 5002, actionsJson: "[\"view\"]");
            var guard = BuildGuard(db);

            var dec = await guard.CheckAsync(new ResourceAccessQuery(
                Tenant, UserId, false, WorkspaceId,
                "agent", 5002, "edit"), CancellationToken.None);

            Assert.False(dec.Allowed);
            Assert.Equal("ResourceAclDenied", dec.DeniedReason);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task ResourceAcl_NoEntryForRole_ShouldFallbackToWorkspaceRole()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            await SeedMember(db, EditorRoleId);
            // 资源 ACL 只给了 viewer role，editor 这一行没条目 → fallback 到 workspace 默认 actions（含 edit）
            await SeedResourceAcl(db, ViewerRoleId, "agent", 5003, actionsJson: "[\"view\"]");
            var guard = BuildGuard(db);

            var dec = await guard.CheckAsync(new ResourceAccessQuery(
                Tenant, UserId, false, WorkspaceId,
                "agent", 5003, "edit"), CancellationToken.None);

            Assert.True(dec.Allowed);
            Assert.Equal("workspace", dec.GrantingTier);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task RequireAsync_ShouldThrowForbidden_WhenDenied()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            await SeedMember(db, ViewerRoleId);
            var guard = BuildGuard(db);

            var ex = await Assert.ThrowsAsync<BusinessException>(() => guard.RequireAsync(new ResourceAccessQuery(
                Tenant, UserId, false, WorkspaceId,
                "agent", null, "delete"), CancellationToken.None));
            Assert.Equal("FORBIDDEN", ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task RequireAsync_ShouldThrowNotFound_WhenWorkspaceMissing()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            // No workspace seeded
            var guard = BuildGuard(db);

            var ex = await Assert.ThrowsAsync<BusinessException>(() => guard.RequireAsync(new ResourceAccessQuery(
                Tenant, UserId, false, 9999999, "agent", null, "view"), CancellationToken.None));
            Assert.Equal("NOT_FOUND", ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    private static ResourceAccessGuard BuildGuard(SqlSugarClient db, bool isPlatformAdmin = false)
    {
        var workspaceRepo = new WorkspaceRepository(db);
        var memberRepo = new WorkspaceMemberRepository(db);
        var roleRepo = new WorkspaceRoleRepository(db);
        var permRepo = new WorkspaceResourcePermissionRepository(db);
        var pdp = new FakePdp(isPlatformAdmin);
        return new ResourceAccessGuard(workspaceRepo, memberRepo, roleRepo, permRepo, pdp);
    }

    private static async Task SeedWorkspace(SqlSugarClient db)
    {
        // Workspace 实体含 UpdatedBy/UpdatedAt 等字段；用 raw INSERT 显式赋全列。
        await db.Ado.ExecuteCommandAsync(
            "INSERT INTO Workspace (Id, TenantIdValue, Name, Description, Icon, AppInstanceId, AppKey, IsArchived, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) " +
            "VALUES (@Id, @Tenant, 'ws', '', '', 0, 'ak', 0, @Now, 0, @Now, 0)",
            new SugarParameter[]
            {
                new("@Id", WorkspaceId),
                new("@Tenant", Tenant.Value.ToString()),
                new("@Now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            });

        // Roles
        var editor = new WorkspaceRole(Tenant, WorkspaceId, "editor", "编辑者", "[\"view\",\"edit\"]", isSystem: true, id: EditorRoleId);
        var viewer = new WorkspaceRole(Tenant, WorkspaceId, "viewer", "查看者", "[\"view\"]", isSystem: true, id: ViewerRoleId);
        await db.Insertable(editor).ExecuteCommandAsync();
        await db.Insertable(viewer).ExecuteCommandAsync();
    }

    private static async Task SeedMember(SqlSugarClient db, long roleId)
    {
        var member = new WorkspaceMember(Tenant, WorkspaceId, UserId, roleId, addedBy: 1, id: 300100 + roleId);
        await db.Insertable(member).ExecuteCommandAsync();
    }

    private static async Task SeedResourceAcl(SqlSugarClient db, long roleId, string resourceType, long resourceId, string actionsJson)
    {
        var p = new WorkspaceResourcePermission(
            Tenant, WorkspaceId, roleId, resourceType, resourceId, actionsJson, updatedBy: 1, id: 400100 + roleId + resourceId);
        await db.Insertable(p).ExecuteCommandAsync();
    }

    private static SqlSugarClient CreateDb(string path)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path}",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(TenantEntity.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static async Task CreateSchema(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<Workspace>();
        db.CodeFirst.InitTables<WorkspaceRole>();
        db.CodeFirst.InitTables<WorkspaceMember>();
        db.CodeFirst.InitTables<WorkspaceResourcePermission>();
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"resource-guard-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class FakePdp : IPermissionDecisionService
    {
        private readonly bool _systemAdmin;
        public FakePdp(bool systemAdmin) => _systemAdmin = systemAdmin;
        public Task<bool> HasPermissionAsync(TenantId tenantId, long userId, string permissionCode, CancellationToken cancellationToken)
            => Task.FromResult(_systemAdmin);
        public Task<bool> IsSystemAdminAsync(TenantId tenantId, long userId, CancellationToken cancellationToken) => Task.FromResult(_systemAdmin);
        public Task InvalidateUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateRoleAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateResourceAsync(TenantId tenantId, string resourceType, long resourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
