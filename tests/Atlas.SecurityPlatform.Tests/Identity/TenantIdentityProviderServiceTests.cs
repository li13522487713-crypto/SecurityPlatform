using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Identity;

/// <summary>
/// 治理 R1-F4：TenantIdentityProviderService 正反例。
/// </summary>
public sealed class TenantIdentityProviderServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000301"));

    [Fact]
    public async Task CreateAsync_ShouldEncryptSecret_AndReturnHasSecretTrue()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<TenantIdentityProvider>();
            var svc = BuildService(db);
            var dto = await svc.CreateAsync(Tenant, 1L,
                new TenantIdentityProviderCreateRequest("oidc-corp", "Corp OIDC", TenantIdentityProvider.TypeOidc, true,
                    "{\"issuer\":\"https://idp\"}", "{\"client_secret\":\"abc\"}"), CancellationToken.None);

            Assert.True(dto.HasSecret);
            var entity = await db.Queryable<TenantIdentityProvider>().FirstAsync();
            Assert.StartsWith(LowCodeCredentialProtector.ProtectedPrefix, entity.SecretJson);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenIdpTypeUnsupported()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<TenantIdentityProvider>();
            var svc = BuildService(db);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.CreateAsync(Tenant, 1L,
                new TenantIdentityProviderCreateRequest("ldap", "L", "ldap", true, "{}", null), CancellationToken.None));
            Assert.Equal(ErrorCodes.ValidationError, ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenCodeDuplicated()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<TenantIdentityProvider>();
            var svc = BuildService(db);
            await svc.CreateAsync(Tenant, 1L,
                new TenantIdentityProviderCreateRequest("dup", "first", TenantIdentityProvider.TypeOidc, true, "{}", null), CancellationToken.None);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.CreateAsync(Tenant, 1L,
                new TenantIdentityProviderCreateRequest("dup", "second", TenantIdentityProvider.TypeSaml, true, "{}", null), CancellationToken.None));
            Assert.Equal(ErrorCodes.Conflict, ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenNotFound()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<TenantIdentityProvider>();
            var svc = BuildService(db);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.UpdateAsync(Tenant, 999, 1L,
                new TenantIdentityProviderUpdateRequest("X", true, "{}", null), CancellationToken.None));
            Assert.Equal(ErrorCodes.NotFound, ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    private static TenantIdentityProviderService BuildService(ISqlSugarClient db)
    {
        var repo = new TenantIdentityProviderRepository(db);
        var protector = new LowCodeCredentialProtector(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:LowCode:CredentialProtectorKey"] = "tests-key-tenant-idp"
            }).Build());
        return new TenantIdentityProviderService(repo, protector, new SeqIdGen());
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

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"tenant-idp-{Guid.NewGuid():N}.db");
    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 7_100_000;
        public long NextId() => System.Threading.Interlocked.Increment(ref _next);
    }
}
