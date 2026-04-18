using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C4：OpenApiChannelConnector PublishAsync + HandleInboundAsync + 限流。
/// </summary>
public sealed class OpenApiChannelConnectorTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private const string WorkspaceId = "ws-3001";

    [Fact]
    public async Task PublishAsync_ShouldIssueTenantTokenAndCatalog()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "open-api", id: 300101);
            var (connector, _) = BuildConnector(db);

            var ctx = new ChannelPublishContext(
                Tenant, 0, channelId, "open-api",
                AgentId: 2002, AgentPublicationId: null, Version: 1, ReleasedByUserId: 9527,
                ConfigSnapshotJson: "{\"rateLimitPerMinute\":120,\"endpoints\":[\"/chat\",\"/task-invoke\"]}");
            var result = await connector.PublishAsync(ctx, CancellationToken.None);

            Assert.True(result.Success);
            using var doc = JsonDocument.Parse(result.PublicMetadataJson!);
            var endpoint = doc.RootElement.GetProperty("endpoint").GetString();
            Assert.Equal($"/api/v1/runtime/channels/open-api/{channelId}/chat", endpoint);
            Assert.Equal(120, doc.RootElement.GetProperty("rateLimitPerMinute").GetInt32());
            Assert.Equal(2, doc.RootElement.GetProperty("endpoints").GetArrayLength());

            var channel = await db.Queryable<WorkspacePublishChannel>().FirstAsync(c => c.Id == channelId);
            Assert.StartsWith("lcp:", channel.SecretJson);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldDispatch_WhenBearerMatches()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "open-api", id: 300102);
            var (connector, fakeChat) = BuildConnector(db);

            var publishResult = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, 0, channelId, "open-api", 2002, null, 1, 9527, "{}"), CancellationToken.None);
            using var meta = JsonDocument.Parse(publishResult.PublicMetadataJson!);
            var token = meta.RootElement.GetProperty("tenantToken").GetString()!;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["authorization"] = "Bearer " + token
            };
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "open-api", "message", null, null,
                "{\"message\":\"hello via open api\"}", headers), CancellationToken.None);

            Assert.True(result.Handled);
            Assert.Single(fakeChat.Calls);
            Assert.Equal(2002L, fakeChat.Calls[0].AgentId);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldReject_WhenBearerMismatches()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "open-api", id: 300103);
            var (connector, _) = BuildConnector(db);

            await connector.PublishAsync(new ChannelPublishContext(
                Tenant, 0, channelId, "open-api", 2002, null, 1, 9527, "{}"), CancellationToken.None);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["authorization"] = "Bearer wrong-token"
            };
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "open-api", "message", null, null,
                "{\"message\":\"hi\"}", headers), CancellationToken.None);

            Assert.False(result.Handled);
            Assert.Equal("OpenApiTokenMismatch", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldRateLimit_AfterExceedingQuota()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "open-api", id: 300104);
            var (connector, fakeChat) = BuildConnector(db);

            // rateLimitPerMinute = 2，第三次必拒
            var publishResult = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, 0, channelId, "open-api", 2002, null, 1, 9527,
                "{\"rateLimitPerMinute\":2}"), CancellationToken.None);
            using var meta = JsonDocument.Parse(publishResult.PublicMetadataJson!);
            var token = meta.RootElement.GetProperty("tenantToken").GetString()!;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["authorization"] = "Bearer " + token
            };
            var bodyJson = "{\"message\":\"x\"}";

            var r1 = await connector.HandleInboundAsync(BuildInbound(channelId, headers, bodyJson), CancellationToken.None);
            var r2 = await connector.HandleInboundAsync(BuildInbound(channelId, headers, bodyJson), CancellationToken.None);
            var r3 = await connector.HandleInboundAsync(BuildInbound(channelId, headers, bodyJson), CancellationToken.None);

            Assert.True(r1.Handled);
            Assert.True(r2.Handled);
            Assert.False(r3.Handled);
            Assert.Equal("OpenApiRateLimited", r3.FailureReason);
            Assert.Equal(2, fakeChat.Calls.Count);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task PublishAsync_ShouldFail_WhenAgentIdMissing()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "open-api", id: 300105);
            var (connector, _) = BuildConnector(db);

            var result = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, 0, channelId, "open-api", null, null, 1, 9527, "{}"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("OpenApiRequiresAgentId", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static ChannelInboundContext BuildInbound(long channelId, Dictionary<string, string> headers, string body)
    {
        return new ChannelInboundContext(Tenant, channelId, "open-api", "message", null, null, body, headers);
    }

    private static (OpenApiChannelConnector Connector, FakeAgentChatService FakeChat) BuildConnector(SqlSugarClient db)
    {
        var channelRepo = new WorkspacePublishChannelRepository(db);
        var releaseRepo = new WorkspaceChannelReleaseRepository(db);
        var protector = new LowCodeCredentialProtector(BuildConfig());
        var fakeChat = new FakeAgentChatService();
        var connector = new OpenApiChannelConnector(channelRepo, releaseRepo, protector, fakeChat, NullLogger<OpenApiChannelConnector>.Instance);
        return (connector, fakeChat);
    }

    private static IConfiguration BuildConfig()
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:LowCode:CredentialProtectorKey"] = "atlas-test-credential-key-2026"
        }).Build();
    }

    private static async Task<long> SeedChannel(SqlSugarClient db, string channelType, long id)
    {
        await db.Ado.ExecuteCommandAsync(
            "INSERT INTO WorkspacePublishChannel (Id, TenantIdValue, WorkspaceId, Name, ChannelType, Status, AuthStatus, Description, SupportedTargetsJson, LastSyncAt, CreatedAt, SecretJson) " +
            "VALUES (@Id, @Tenant, @Ws, 'open-api-channel', @Type, 'pending', 'unauthorized', 'test', '[\"agent\"]', '1970-01-01 00:00:00', @CreatedAt, NULL)",
            new SugarParameter[]
            {
                new("@Id", id),
                new("@Tenant", Tenant.Value.ToString()),
                new("@Ws", WorkspaceId),
                new("@Type", channelType),
                new("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            });
        return id;
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
        db.CodeFirst.InitTables<WorkspacePublishChannel>();
        db.CodeFirst.InitTables<WorkspaceChannelRelease>();
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"open-api-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private sealed class FakeAgentChatService : IAgentChatService
    {
        public List<(long AgentId, AgentChatRequest Request)> Calls { get; } = new();

        public Task<AgentChatResponse> ChatAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
        {
            Calls.Add((agentId, request));
            return Task.FromResult(new AgentChatResponse(7001, 8001, $"echo:{request.Message}", null));
        }

        public IAsyncEnumerable<string> ChatStreamAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
            => EmptyAsyncEnumerable<string>.Instance;

        public IAsyncEnumerable<AgentChatStreamEvent> ChatEventStreamAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
            => EmptyAsyncEnumerable<AgentChatStreamEvent>.Instance;

        public Task CancelAsync(TenantId tenantId, long userId, long agentId, long conversationId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class EmptyAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        public static readonly EmptyAsyncEnumerable<T> Instance = new();
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new Enumerator();
        private sealed class Enumerator : IAsyncEnumerator<T>
        {
            public T Current => default!;
            public ValueTask DisposeAsync() => default;
            public ValueTask<bool> MoveNextAsync() => new(false);
        }
    }
}
