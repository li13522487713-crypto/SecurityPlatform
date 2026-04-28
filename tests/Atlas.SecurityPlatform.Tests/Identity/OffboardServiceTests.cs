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
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Identity;

/// <summary>
/// 治理 R1-F4：OffboardService 正反例。
/// </summary>
public sealed class OffboardServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000302"));

    [Fact]
    public async Task ExecuteOffboardAsync_ShouldRecordTransfers_AndMarkUserOffboarded()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedUserAsync(db, 100L, "alice");
            var pdp = new RecordingPdp();
            var svc = new OffboardService(
                db,
                new ResourceOwnershipTransferRepository(db),
                new OrganizationRepository(db),
                new OrganizationMemberRepository(db),
                pdp,
                new SeqIdGen());

            var resp = await svc.ExecuteOffboardAsync(Tenant, actorUserId: 1L,
                new OffboardRequest(100L, 200L, new[]
                {
                    new OffboardTransferItem("agent", "1001"),
                    new OffboardTransferItem("workflow", "2002")
                }, "leaving"), CancellationToken.None);

            Assert.Equal(2, resp.Count);
            Assert.All(resp, t => Assert.Equal("executed", t.Status));
            Assert.Equal(2, pdp.ResourceCalls.Count);

            var user = await db.Queryable<UserAccount>().Where(x => x.Id == 100L).FirstAsync();
            Assert.Equal(UserAccount.StatusOffboarded, user.Status);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task ExecuteOffboardAsync_ShouldThrow_WhenFromAndToSame()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var svc = BuildService(db);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.ExecuteOffboardAsync(Tenant, 1L,
                new OffboardRequest(50L, 50L, new[] { new OffboardTransferItem("agent", "1") }, null),
                CancellationToken.None));
            Assert.Equal(ErrorCodes.ValidationError, ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task MoveMemberAcrossOrganizationsAsync_ShouldThrow_WhenTargetOrganizationNotFound()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedOrganizationAsync(db, 1L, "src");
            await SeedMemberAsync(db, 1L, 9L);
            var svc = BuildService(db);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.MoveMemberAcrossOrganizationsAsync(
                Tenant, sourceOrganizationId: 1L, targetUserId: 9L, actorUserId: 1L,
                new OrganizationMemberMoveRequest("9999", null), CancellationToken.None));
            Assert.Equal(ErrorCodes.NotFound, ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    private static OffboardService BuildService(SqlSugarClient db)
    {
        return new OffboardService(
            db,
            new ResourceOwnershipTransferRepository(db),
            new OrganizationRepository(db),
            new OrganizationMemberRepository(db),
            new RecordingPdp(),
            new SeqIdGen());
    }

    private static async Task SeedUserAsync(SqlSugarClient db, long userId, string username)
    {
        var user = new UserAccount(Tenant, username, username, "$placeholder$", userId);
        await db.Insertable(user).ExecuteCommandAsync();
    }

    private static async Task SeedOrganizationAsync(SqlSugarClient db, long id, string code)
    {
        var org = new Organization(Tenant, code, code, null, isDefault: code == "default", createdBy: 1, id: id);
        await db.Insertable(org).ExecuteCommandAsync();
    }

    private static async Task SeedMemberAsync(SqlSugarClient db, long orgId, long userId)
    {
        var m = new OrganizationMember(Tenant, orgId, userId, "member", 1L, 99_000L + userId);
        await db.Insertable(m).ExecuteCommandAsync();
    }

    private static async Task CreateSchema(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<UserAccount>();
        db.CodeFirst.InitTables<Organization>();
        db.CodeFirst.InitTables<OrganizationMember>();
        db.CodeFirst.InitTables<ResourceOwnershipTransfer>();
        await Task.CompletedTask;
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
                    if (property.PropertyType == typeof(DateTimeOffset))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"offboard-{Guid.NewGuid():N}.db");
    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 7_200_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }

    private sealed class RecordingPdp : IPermissionDecisionService
    {
        public List<(string Type, long Id)> ResourceCalls { get; } = new();
        public Task<bool> HasPermissionAsync(TenantId tenantId, long userId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> IsSystemAdminAsync(TenantId tenantId, long userId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task InvalidateUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateRoleAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateResourceAsync(TenantId tenantId, string resourceType, long resourceId, CancellationToken cancellationToken = default)
        {
            ResourceCalls.Add((resourceType, resourceId));
            return Task.CompletedTask;
        }
    }
}
