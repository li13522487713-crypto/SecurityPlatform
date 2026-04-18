using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Identity;

/// <summary>
/// 治理 R1-F4：TenantPolicyService 正反例（network + residency）。
/// </summary>
public sealed class TenantPolicyServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000300"));

    [Fact]
    public async Task UpsertNetworkAsync_ShouldBeIdempotent_AndReflectLatestValues()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<TenantNetworkPolicy>();
            db.CodeFirst.InitTables<TenantDataResidencyPolicy>();
            var svc = BuildService(db);

            var first = await svc.UpsertNetworkAsync(Tenant, 1L,
                new TenantNetworkPolicyUpdateRequest("audit", new[] { "10.0.0.0/8" }, null), CancellationToken.None);
            Assert.Equal("audit", first.Mode);
            Assert.Single(first.Allowlist);

            var second = await svc.UpsertNetworkAsync(Tenant, 2L,
                new TenantNetworkPolicyUpdateRequest("enforce", new[] { "10.0.0.0/8", "172.16.0.0/12" }, new[] { "1.2.3.4" }), CancellationToken.None);
            Assert.Equal("enforce", second.Mode);
            Assert.Equal(2, second.Allowlist.Count);
            Assert.Single(second.Denylist);
            Assert.Equal(first.Id, second.Id); // 同一 tenant 行更新而非新建
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task GetNetworkAsync_ShouldReturnNull_WhenNoPolicyConfigured()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<TenantNetworkPolicy>();
            db.CodeFirst.InitTables<TenantDataResidencyPolicy>();
            var svc = BuildService(db);
            var dto = await svc.GetNetworkAsync(Tenant, CancellationToken.None);
            Assert.Null(dto);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task UpsertResidencyAsync_ShouldPersistRegions_AndOverwriteOnReUpsert()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<TenantNetworkPolicy>();
            db.CodeFirst.InitTables<TenantDataResidencyPolicy>();
            var svc = BuildService(db);

            var first = await svc.UpsertResidencyAsync(Tenant, 1L,
                new TenantDataResidencyPolicyUpdateRequest(new[] { "cn-east-2" }, "init"), CancellationToken.None);
            Assert.Single(first.AllowedRegions);

            var second = await svc.UpsertResidencyAsync(Tenant, 1L,
                new TenantDataResidencyPolicyUpdateRequest(new[] { "cn-east-2", "cn-north-1" }, null), CancellationToken.None);
            Assert.Equal(2, second.AllowedRegions.Count);
            Assert.Null(second.Notes);
        }
        finally { DeleteDb(dbPath); }
    }

    private static TenantPolicyService BuildService(ISqlSugarClient db)
    {
        var network = new TenantNetworkPolicyRepository(db);
        var residency = new TenantDataResidencyPolicyRepository(db);
        return new TenantPolicyService(network, residency, new SeqIdGen());
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

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"tenant-policy-{Guid.NewGuid():N}.db");
    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 7_000_000;
        public long NextId() => System.Threading.Interlocked.Increment(ref _next);
    }
}
