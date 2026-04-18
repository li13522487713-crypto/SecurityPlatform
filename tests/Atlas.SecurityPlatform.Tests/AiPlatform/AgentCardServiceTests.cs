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
/// 治理 R1-F4：AgentCardService 正反例。
/// </summary>
public sealed class AgentCardServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000401"));

    [Fact]
    public async Task CreateAndUpdateAndDelete_HappyPath()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<AgentCard>();
            var svc = new AgentCardService(db, new SeqIdGen());
            var dto = await svc.CreateAsync(Tenant, agentId: 1L, createdBy: 1L,
                new AgentCardUpsertRequest("price-card", "structured", "{\"items\":[]}", true), CancellationToken.None);
            await svc.UpdateAsync(Tenant, 1L, long.Parse(dto.Id),
                new AgentCardUpsertRequest("price-card-v2", "structured", "{\"items\":[1]}", false), CancellationToken.None);
            var listAfterUpdate = await svc.ListAsync(Tenant, 1L, CancellationToken.None);
            Assert.False(listAfterUpdate[0].IsEnabled);

            await svc.DeleteAsync(Tenant, 1L, long.Parse(dto.Id), CancellationToken.None);
            var listAfterDelete = await svc.ListAsync(Tenant, 1L, CancellationToken.None);
            Assert.Empty(listAfterDelete);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenCardNotFound()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            db.CodeFirst.InitTables<AgentCard>();
            var svc = new AgentCardService(db, new SeqIdGen());
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.UpdateAsync(
                Tenant, agentId: 1L, cardId: 99999L,
                new AgentCardUpsertRequest("x", "structured", "{}", false), CancellationToken.None));
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

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"agent-card-{Guid.NewGuid():N}.db");
    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 7_400_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }
}
