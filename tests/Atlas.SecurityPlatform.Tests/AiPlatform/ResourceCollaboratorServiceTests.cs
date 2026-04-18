using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform;

/// <summary>
/// 覆盖 M-G03-C7（S7）：ResourceCollaboratorService。
/// </summary>
public sealed class ResourceCollaboratorServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000030"));
    private const long WorkspaceId = 700100;
    private const long EditorRoleId = 800001;
    private const long ViewerRoleId = 800002;

    [Fact]
    public async Task ListAsync_ShouldReturnRealWorkspaceMembers_AndExplicitAclFlags()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            // 仅 seed workspace 成员，不创建 UserAccount（SqlSugar 在 SQLite 下的 DateTimeOffset 计算列绑定有限制；
            // 服务的 List 会按 userId 关联，找不到 UserAccount 时降级为 displayName/username 空字符串，符合 DTO 契约）。
            await SeedMember(db, 9001, EditorRoleId);
            await SeedMember(db, 9002, ViewerRoleId);
            // 给 viewer role 一个资源 ACL（仅 view），编辑角色无显式 ACL
            await SeedResourceAcl(db, ViewerRoleId, "agent", 5001, "[\"view\"]");

            var service = BuildService(db);
            var list = await service.ListAsync(Tenant, WorkspaceId, "agent", 5001, CancellationToken.None);

            Assert.Equal(2, list.Count);
            var alice = list.First(c => c.UserId == "9001");
            Assert.Equal("editor", alice.RoleCode);
            Assert.False(alice.HasExplicitResourceAcl);
            var bob = list.First(c => c.UserId == "9002");
            Assert.Equal("viewer", bob.RoleCode);
            Assert.True(bob.HasExplicitResourceAcl);
            Assert.Equal("[\"view\"]", bob.ExplicitActionsJson);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task AddAsync_ShouldUpsertWorkspaceMember_AndInvalidatePdp()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            var service = BuildService(db, out var pdp);

            await service.AddAsync(Tenant, WorkspaceId, "agent", 5001, actorUserId: 1,
                new ResourceCollaboratorAddRequest("9001", "viewer"), CancellationToken.None);

            var tenantValue = Tenant.Value;
            const long workspaceIdLocal = WorkspaceId;
            var members = await db.Queryable<WorkspaceMember>()
                .Where(m => m.TenantIdValue == tenantValue && m.WorkspaceId == workspaceIdLocal && m.UserId == 9001L)
                .ToListAsync();
            Assert.Single(members);
            Assert.Equal(ViewerRoleId, members[0].WorkspaceRoleId);
            Assert.Single(pdp.ResourceInvalidations);
            Assert.Equal("agent", pdp.ResourceInvalidations[0].ResourceType);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task AddAsync_ShouldThrowValidationError_OnInvalidResourceType()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            var service = BuildService(db);

            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AddAsync(
                Tenant, WorkspaceId, "unknown-type", 1, 1, new ResourceCollaboratorAddRequest("1", "viewer"), CancellationToken.None));
            Assert.Equal("VALIDATION_ERROR", ex.Code);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteMember_AndInvalidatePdp()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedWorkspace(db);
            await SeedMember(db, 9001, ViewerRoleId);
            var service = BuildService(db, out var pdp);

            await service.RemoveAsync(Tenant, WorkspaceId, "agent", 5001, targetUserId: 9001, actorUserId: 1, CancellationToken.None);

            var tenantValue = Tenant.Value;
            const long workspaceIdLocal = WorkspaceId;
            var remaining = await db.Queryable<WorkspaceMember>()
                .Where(m => m.TenantIdValue == tenantValue && m.WorkspaceId == workspaceIdLocal && m.UserId == 9001L)
                .CountAsync();
            Assert.Equal(0, remaining);
            Assert.Single(pdp.ResourceInvalidations);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static ResourceCollaboratorService BuildService(SqlSugarClient db) => BuildService(db, out _);

    private static ResourceCollaboratorService BuildService(SqlSugarClient db, out RecordingPdp pdp)
    {
        var workspaceRepo = new WorkspaceRepository(db);
        var memberRepo = new WorkspaceMemberRepository(db);
        var roleRepo = new WorkspaceRoleRepository(db);
        var permRepo = new WorkspaceResourcePermissionRepository(db);
        pdp = new RecordingPdp();
        var idGen = new SeqIdGen();
        return new ResourceCollaboratorService(db, workspaceRepo, memberRepo, roleRepo, permRepo, pdp, idGen);
    }

    private static async Task SeedWorkspace(SqlSugarClient db)
    {
        await db.Ado.ExecuteCommandAsync(
            "INSERT INTO Workspace (Id, TenantIdValue, Name, Description, Icon, AppInstanceId, AppKey, IsArchived, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) " +
            "VALUES (@Id, @Tenant, 'ws', '', '', 0, 'ak', 0, @Now, 0, @Now, 0)",
            new SugarParameter[]
            {
                new("@Id", WorkspaceId),
                new("@Tenant", Tenant.Value.ToString()),
                new("@Now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            });
        var editor = new WorkspaceRole(Tenant, WorkspaceId, "editor", "编辑者", "[\"view\",\"edit\"]", true, EditorRoleId);
        var viewer = new WorkspaceRole(Tenant, WorkspaceId, "viewer", "查看者", "[\"view\"]", true, ViewerRoleId);
        await db.Insertable(editor).ExecuteCommandAsync();
        await db.Insertable(viewer).ExecuteCommandAsync();
    }

    private static async Task SeedMember(SqlSugarClient db, long userId, long roleId)
    {
        var member = new WorkspaceMember(Tenant, WorkspaceId, userId, roleId, addedBy: 1, id: 900100 + userId + roleId);
        await db.Insertable(member).ExecuteCommandAsync();
    }

    private static async Task SeedResourceAcl(SqlSugarClient db, long roleId, string resourceType, long resourceId, string actionsJson)
    {
        var p = new WorkspaceResourcePermission(Tenant, WorkspaceId, roleId, resourceType, resourceId, actionsJson, 1, id: 1000100 + roleId + resourceId);
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
        // UserAccount 表也需要存在以便 Db.Queryable<UserAccount>() 不抛错（即便表为空）
        db.CodeFirst.InitTables<UserAccount>();
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"resource-collab-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class RecordingPdp : IPermissionDecisionService
    {
        public List<(TenantId T, string ResourceType, long ResourceId)> ResourceInvalidations { get; } = new();
        public Task<bool> HasPermissionAsync(TenantId tenantId, long userId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> IsSystemAdminAsync(TenantId tenantId, long userId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task InvalidateUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateRoleAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateResourceAsync(TenantId tenantId, string resourceType, long resourceId, CancellationToken cancellationToken = default)
        {
            ResourceInvalidations.Add((tenantId, resourceType, resourceId));
            return Task.CompletedTask;
        }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 1_500_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }
}
