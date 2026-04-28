using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Identity;

/// <summary>
/// 覆盖 M-G05-C1（S9）：OrganizationService。
/// </summary>
public sealed class OrganizationServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000050"));

    [Fact]
    public async Task GetOrCreateDefaultAsync_ShouldBeIdempotent()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var service = BuildService(db);
            var first = await service.GetOrCreateDefaultAsync(Tenant, createdBy: 1, CancellationToken.None);
            var second = await service.GetOrCreateDefaultAsync(Tenant, createdBy: 1, CancellationToken.None);
            Assert.Equal(first.Id, second.Id);
            Assert.True(first.IsDefault);
            Assert.Equal("default", first.Code);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectDuplicateCode()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var service = BuildService(db);
            await service.CreateAsync(Tenant, 1, new OrganizationCreateRequest("eng", "Engineering", null), CancellationToken.None);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(
                Tenant, 1, new OrganizationCreateRequest("eng", "Other", null), CancellationToken.None));
            Assert.Equal("CONFLICT", ex.Code);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectDefaultOrganization()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var service = BuildService(db);
            var def = await service.GetOrCreateDefaultAsync(Tenant, 1, CancellationToken.None);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(Tenant, long.Parse(def.Id), CancellationToken.None));
            Assert.Equal("VALIDATION_ERROR", ex.Code);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task ListAsync_ShouldReturnDefaultFirst()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var service = BuildService(db);
            await service.CreateAsync(Tenant, 1, new OrganizationCreateRequest("biz", "Business", null), CancellationToken.None);
            await service.GetOrCreateDefaultAsync(Tenant, 1, CancellationToken.None);
            var list = await service.ListAsync(Tenant, CancellationToken.None);
            Assert.Equal(2, list.Count);
            Assert.True(list[0].IsDefault);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static OrganizationService BuildService(SqlSugarClient db)
    {
        var repo = new OrganizationRepository(db);
        var idGen = new SeqIdGen();
        return new OrganizationService(repo, idGen);
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
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"organization-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 2_000_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }
}
