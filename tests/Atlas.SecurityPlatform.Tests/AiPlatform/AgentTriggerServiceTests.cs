using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services.AiPlatform;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform;

/// <summary>
/// 治理 R1-F4：AgentTriggerService 正反例。
/// </summary>
public sealed class AgentTriggerServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000400"));

    [Fact]
    public async Task CreateThenList_ShouldReturnPersistedEntity()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<AgentTrigger>();
            var svc = new AgentTriggerService(db, new SeqIdGen());
            var dto = await svc.CreateAsync(Tenant, agentId: 1L, createdBy: 1L,
                new AgentTriggerUpsertRequest("daily-cron", "cron", "{\"expr\":\"0 0 * * *\"}", true), CancellationToken.None);
            Assert.True(dto.IsEnabled);
            var list = await svc.ListAsync(Tenant, agentId: 1L, CancellationToken.None);
            Assert.Single(list);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenTriggerNotFound()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<AgentTrigger>();
            var svc = new AgentTriggerService(db, new SeqIdGen());
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.UpdateAsync(
                Tenant, agentId: 1L, triggerId: 99999L,
                new AgentTriggerUpsertRequest("x", "cron", "{}", false), CancellationToken.None));
            Assert.Equal(ErrorCodes.NotFound, ex.Code);
        }
        finally { DeleteDb(dbPath); }
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

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"agent-trigger-{Guid.NewGuid():N}.db");
    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 7_300_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }
}
