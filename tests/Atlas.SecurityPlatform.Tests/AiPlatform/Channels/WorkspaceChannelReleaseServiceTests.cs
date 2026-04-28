using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C2：渠道发布编排服务（治理 §3 S1）。
/// 走真实 SQLite 文件 + 真实 Repository，仅 mock connector / id-generator / audit / current-user。
/// </summary>
public sealed class WorkspaceChannelReleaseServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private const string WorkspaceId = "ws-1001";

    [Fact]
    public async Task PublishAsync_WithNoConnector_ShouldRecordFailedAndAudit()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "wechat-mp");
            var (service, audit) = BuildService(db, connectors: Array.Empty<IWorkspaceChannelConnector>());

            var dto = await service.PublishAsync(
                Tenant,
                WorkspaceId,
                fixture.ChannelId.ToString(),
                CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest(AgentId: "1001", AgentPublicationId: null, ReleaseNote: "first"),
                CancellationToken.None);

            Assert.Equal(WorkspaceChannelRelease.StatusFailed, dto.Status);
            Assert.Equal(1, dto.ReleaseNo);
            Assert.Contains("not registered", dto.ConnectorMessage, StringComparison.OrdinalIgnoreCase);

            Assert.Single(audit.Records);
            Assert.Equal("CHANNEL_RELEASE_PUBLISH", audit.Records[0].Action);
            Assert.Equal("failure", audit.Records[0].Result);

            // channel 不应该被 markAuthorized
            var channelStillPending = await db.Queryable<WorkspacePublishChannel>()
                .FirstAsync(x => x.Id == fixture.ChannelId);
            Assert.Equal("unauthorized", channelStillPending.AuthStatus);
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    [Fact]
    public async Task PublishAsync_WithSuccessfulConnector_ShouldMarkActiveAndAuthorizeChannel()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "web-sdk");
            var connector = new RecordingConnector("web-sdk", new ChannelPublishResult(true, "ok", "{\"snippet\":\"abc\"}", null));
            var (service, audit) = BuildService(db, new IWorkspaceChannelConnector[] { connector });

            var dto = await service.PublishAsync(
                Tenant,
                WorkspaceId,
                fixture.ChannelId.ToString(),
                CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest(AgentId: "1001", AgentPublicationId: null, ReleaseNote: "go-live"),
                CancellationToken.None);

            Assert.Equal(WorkspaceChannelRelease.StatusActive, dto.Status);
            Assert.Equal(1, dto.ReleaseNo);
            Assert.Equal("{\"snippet\":\"abc\"}", dto.PublicMetadataJson);
            Assert.Single(connector.Calls);
            Assert.Equal(fixture.ChannelId, connector.Calls[0].ChannelId);

            var channel = await db.Queryable<WorkspacePublishChannel>().FirstAsync(x => x.Id == fixture.ChannelId);
            Assert.Equal("authorized", channel.AuthStatus);
            Assert.Equal("active", channel.Status);

            Assert.Single(audit.Records);
            Assert.Equal("success", audit.Records[0].Result);
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    [Fact]
    public async Task PublishAsync_SecondSuccess_ShouldSupersedePreviousActive()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "web-sdk");
            var connector = new RecordingConnector("web-sdk", new ChannelPublishResult(true, "ok", "{}", null));
            var (service, _) = BuildService(db, new IWorkspaceChannelConnector[] { connector });

            await service.PublishAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest("1001", null, "v1"), CancellationToken.None);
            var second = await service.PublishAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest("1001", null, "v2"), CancellationToken.None);

            Assert.Equal(2, second.ReleaseNo);
            var entities = (await db.Queryable<WorkspaceChannelRelease>()
                .Where(x => x.ChannelId == fixture.ChannelId)
                .ToListAsync())
                .OrderBy(x => x.ReleaseNo)
                .ToList();
            Assert.Equal(2, entities.Count);
            Assert.Equal(WorkspaceChannelRelease.StatusSuperseded, entities[0].Status);
            Assert.NotNull(entities[0].SupersededAt);
            Assert.Equal(WorkspaceChannelRelease.StatusActive, entities[1].Status);
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    [Fact]
    public async Task RollbackAsync_ShouldCreateNewReleaseAndMarkPreviousAsRolledBack()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "web-sdk");
            var connector = new RecordingConnector("web-sdk", new ChannelPublishResult(true, "ok", "{\"v\":1}", null));
            var (service, audit) = BuildService(db, new IWorkspaceChannelConnector[] { connector });

            var first = await service.PublishAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest("1001", null, "v1"), CancellationToken.None);
            connector.NextResult = new ChannelPublishResult(true, "ok", "{\"v\":2}", null);
            await service.PublishAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest("1001", null, "v2"), CancellationToken.None);

            connector.NextResult = new ChannelPublishResult(true, "ok", "{\"v\":1-rollback\"}", null);
            var rolled = await service.RollbackAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseRollbackRequest(first.Id, "back-to-v1"),
                CancellationToken.None);

            Assert.Equal(3, rolled.ReleaseNo);
            Assert.Equal(WorkspaceChannelRelease.StatusActive, rolled.Status);
            Assert.Equal(first.Id, rolled.RolledBackFromReleaseId);

            var second = await db.Queryable<WorkspaceChannelRelease>()
                .Where(x => x.ChannelId == fixture.ChannelId && x.ReleaseNo == 2)
                .FirstAsync();
            Assert.Equal(WorkspaceChannelRelease.StatusRolledBack, second.Status);
            Assert.NotNull(second.SupersededAt);

            Assert.Contains(audit.Records, r => r.Action == "CHANNEL_RELEASE_ROLLBACK" && r.Result == "success");
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    [Fact]
    public async Task RollbackAsync_ToCurrentlyActiveRelease_ShouldThrowValidationError()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "web-sdk");
            var connector = new RecordingConnector("web-sdk", new ChannelPublishResult(true, "ok", "{}", null));
            var (service, _) = BuildService(db, new IWorkspaceChannelConnector[] { connector });

            var first = await service.PublishAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest("1001", null, "v1"), CancellationToken.None);

            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.RollbackAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseRollbackRequest(first.Id, null),
                CancellationToken.None));
            Assert.Equal("VALIDATION_ERROR", ex.Code);
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    [Fact]
    public async Task ListAsync_ShouldReturnReleasesOrderedByReleaseNoDesc()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "web-sdk");
            var connector = new RecordingConnector("web-sdk", new ChannelPublishResult(true, "ok", "{}", null));
            var (service, _) = BuildService(db, new IWorkspaceChannelConnector[] { connector });

            for (var i = 0; i < 3; i++)
            {
                await service.PublishAsync(
                    Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                    new WorkspaceChannelReleaseCreateRequest("1001", null, $"v{i}"),
                    CancellationToken.None);
            }

            var paged = await service.ListAsync(Tenant, WorkspaceId, fixture.ChannelId.ToString(),
                new PagedRequest { PageIndex = 1, PageSize = 10 }, CancellationToken.None);

            Assert.Equal(3, paged.Total);
            Assert.Equal(new[] { 3, 2, 1 }, paged.Items.Select(x => x.ReleaseNo).ToArray());
            Assert.Equal(WorkspaceChannelRelease.StatusActive, paged.Items[0].Status);
            Assert.Equal(WorkspaceChannelRelease.StatusSuperseded, paged.Items[1].Status);
            Assert.Equal(WorkspaceChannelRelease.StatusSuperseded, paged.Items[2].Status);
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    [Fact]
    public async Task GetAsync_ShouldThrowNotFound_WhenReleaseMissing()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "web-sdk");
            var (service, _) = BuildService(db, Array.Empty<IWorkspaceChannelConnector>());

            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), "9999", CancellationToken.None));
            Assert.Equal("NOT_FOUND", ex.Code);
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenConnectorThrows_ShouldRecordFailedAndRethrow()
    {
        var dbPath = NewDbPath();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var fixture = await SeedChannelAsync(db, channelType: "web-sdk");
            var connector = new ThrowingConnector("web-sdk", new BusinessException("VALIDATION_ERROR", "fake-down"));
            var (service, audit) = BuildService(db, new IWorkspaceChannelConnector[] { connector });

            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.PublishAsync(
                Tenant, WorkspaceId, fixture.ChannelId.ToString(), CurrentUser(),
                new WorkspaceChannelReleaseCreateRequest("1001", null, null), CancellationToken.None));
            Assert.Equal("VALIDATION_ERROR", ex.Code);

            var record = await db.Queryable<WorkspaceChannelRelease>()
                .Where(x => x.ChannelId == fixture.ChannelId)
                .FirstAsync();
            Assert.Equal(WorkspaceChannelRelease.StatusFailed, record.Status);
            Assert.Contains("fake-down", record.ConnectorMessage, StringComparison.Ordinal);
            Assert.Single(audit.Records);
            Assert.Equal("failure", audit.Records[0].Result);
        }
        finally
        {
            CleanupDb(dbPath);
        }
    }

    private static (WorkspaceChannelReleaseService Service, RecordingAuditWriter Audit) BuildService(
        SqlSugarClient db,
        IWorkspaceChannelConnector[] connectors)
    {
        var registry = new WorkspaceChannelConnectorRegistry(connectors);
        var releaseRepo = new WorkspaceChannelReleaseRepository(db);
        var channelRepo = new WorkspacePublishChannelRepository(db);
        var publicationRepo = new AgentPublicationRepository(db);
        var audit = new RecordingAuditWriter();
        var idGen = new SequentialIdGenerator();
        var service = new WorkspaceChannelReleaseService(
            releaseRepo, channelRepo, publicationRepo, registry, idGen, audit, NullLogger<WorkspaceChannelReleaseService>.Instance);
        return (service, audit);
    }

    private static async Task<ChannelFixture> SeedChannelAsync(SqlSugarClient db, string channelType)
    {
        // 直接 INSERT，绕过 SqlSugar 默认对 DateTime? 的 NOT NULL 推断（生产由 DatabaseInitializer 修正，
        // 测试这里走最小路径）。
        await db.Ado.ExecuteCommandAsync(
            "INSERT INTO WorkspacePublishChannel (Id, TenantIdValue, WorkspaceId, Name, ChannelType, Status, AuthStatus, Description, SupportedTargetsJson, LastSyncAt, CreatedAt) " +
            "VALUES (@Id, @Tenant, @Ws, 'test-channel', @Type, 'pending', 'unauthorized', 'test', '[\"agent\"]', '1970-01-01 00:00:00', @CreatedAt)",
            new SugarParameter[]
            {
                new("@Id", 200001L),
                new("@Tenant", Tenant.Value.ToString()),
                new("@Ws", WorkspaceId),
                new("@Type", channelType),
                new("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            });
        return new ChannelFixture(200001L);
    }

    private static CurrentUserInfo CurrentUser()
    {
        return new CurrentUserInfo(
            UserId: 999,
            Username: "admin",
            DisplayName: "admin",
            TenantId: Tenant,
            Roles: Array.Empty<string>(),
            IsPlatformAdmin: true,
            SessionId: 1);
    }

    private static SqlSugarClient CreateDb(string dbPath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
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

    private static async Task CreateSchemaAsync(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<WorkspacePublishChannel>();
        db.CodeFirst.InitTables<WorkspaceChannelRelease>();
        db.CodeFirst.InitTables<AgentPublication>();
        await Task.CompletedTask;
    }

    private static string NewDbPath()
        => Path.Combine(Path.GetTempPath(), $"channel-release-{Guid.NewGuid():N}.db");

    private static void CleanupDb(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return;
        }
        try
        {
            File.Delete(dbPath);
        }
        catch
        {
            // tolerate cleanup race
        }
    }

    private sealed record ChannelFixture(long ChannelId);

    private sealed class SequentialIdGenerator : IIdGeneratorAccessor
    {
        private long _next = 100000;
        public long NextId() => Interlocked.Increment(ref _next);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditRecord> Records { get; } = new();
        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingConnector : IWorkspaceChannelConnector
    {
        public RecordingConnector(string channelType, ChannelPublishResult initial)
        {
            ChannelType = channelType;
            NextResult = initial;
        }
        public string ChannelType { get; }
        public ChannelPublishResult NextResult { get; set; }
        public List<ChannelPublishContext> Calls { get; } = new();

        public Task<ChannelPublishResult> PublishAsync(ChannelPublishContext context, CancellationToken cancellationToken)
        {
            Calls.Add(context);
            return Task.FromResult(NextResult);
        }

        public Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken)
            => Task.FromResult(new ChannelDispatchResult(true, null, null));

        public Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ThrowingConnector : IWorkspaceChannelConnector
    {
        private readonly Exception _error;
        public ThrowingConnector(string channelType, Exception error)
        {
            ChannelType = channelType;
            _error = error;
        }
        public string ChannelType { get; }
        public Task<ChannelPublishResult> PublishAsync(ChannelPublishContext context, CancellationToken cancellationToken)
            => throw _error;
        public Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken)
            => Task.FromResult(new ChannelDispatchResult(false, null, null));
        public Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
