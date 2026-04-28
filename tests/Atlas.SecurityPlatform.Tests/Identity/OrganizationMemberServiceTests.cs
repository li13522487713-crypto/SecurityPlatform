using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Identity;

/// <summary>
/// 覆盖 M-G05-C4 + C5（S10）：OrganizationMemberService。
/// </summary>
public sealed class OrganizationMemberServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000060"));

    [Fact]
    public async Task AddAsync_ShouldUpsert_AndInvalidateUser()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var orgId = await SeedOrganization(db);
            var (svc, pdp) = BuildService(db);

            await svc.AddAsync(Tenant, orgId, actorUserId: 1, new OrganizationMemberAddRequest("9001", "admin"), CancellationToken.None);
            await svc.AddAsync(Tenant, orgId, actorUserId: 1, new OrganizationMemberAddRequest("9001", "owner"), CancellationToken.None);
            var list = await svc.ListAsync(Tenant, orgId, CancellationToken.None);
            Assert.Single(list);
            Assert.Equal("owner", list[0].RoleCode);
            Assert.Equal(2, pdp.UserInvalidations.Count);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteRow()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var orgId = await SeedOrganization(db);
            var (svc, _) = BuildService(db);
            await svc.AddAsync(Tenant, orgId, 1, new OrganizationMemberAddRequest("9001", "member"), CancellationToken.None);
            await svc.RemoveAsync(Tenant, orgId, 9001, CancellationToken.None);
            var list = await svc.ListAsync(Tenant, orgId, CancellationToken.None);
            Assert.Empty(list);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task MoveWorkspaceAsync_ShouldThrow_WhenWorkspaceMissing()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var orgId = await SeedOrganization(db);
            var (svc, _) = BuildService(db);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.MoveWorkspaceAsync(Tenant, 999999, orgId, 1, CancellationToken.None));
            Assert.Equal("NOT_FOUND", ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    private static (OrganizationMemberService Service, RecordingPdp Pdp) BuildService(SqlSugarClient db)
    {
        var orgRepo = new OrganizationRepository(db);
        var memberRepo = new OrganizationMemberRepository(db);
        var workspaceRepo = new WorkspaceRepository(db);
        var idGen = new SeqIdGen();
        var pdp = new RecordingPdp();
        return (new OrganizationMemberService(orgRepo, memberRepo, workspaceRepo, idGen, pdp), pdp);
    }

    private static async Task<long> SeedOrganization(SqlSugarClient db)
    {
        var org = new Organization(Tenant, code: "default", name: "Default Org", description: null, isDefault: true, createdBy: 1, id: 3000001);
        await db.Insertable(org).ExecuteCommandAsync();
        return org.Id;
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
        db.CodeFirst.InitTables<Organization>();
        db.CodeFirst.InitTables<OrganizationMember>();
        db.CodeFirst.InitTables<Workspace>();
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"organization-member-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 4_000_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }

    private sealed class RecordingPdp : IPermissionDecisionService
    {
        public List<long> UserInvalidations { get; } = new();
        public Task<bool> HasPermissionAsync(TenantId tenantId, long userId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> IsSystemAdminAsync(TenantId tenantId, long userId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task InvalidateUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken = default)
        {
            UserInvalidations.Add(userId);
            return Task.CompletedTask;
        }
        public Task InvalidateRoleAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateResourceAsync(TenantId tenantId, string resourceType, long resourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
