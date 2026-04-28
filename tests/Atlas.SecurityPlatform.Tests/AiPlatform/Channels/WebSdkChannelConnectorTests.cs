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
using Atlas.Infrastructure.Services.AiPlatform.Channels.Signatures;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C3：WebSdkChannelConnector PublishAsync + HandleInboundAsync。
/// </summary>
public sealed class WebSdkChannelConnectorTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private const string WorkspaceId = "ws-2001";

    [Fact]
    public async Task PublishAsync_ShouldRotateSecret_AndReturnSnippetEndpoint()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "web-sdk");
            var (connector, fakeChat) = BuildConnector(db);

            var ctx = new ChannelPublishContext(
                Tenant, ResolveLong(WorkspaceId), channelId, "web-sdk",
                AgentId: 1001, AgentPublicationId: null, Version: 1, ReleasedByUserId: 9527,
                ConfigSnapshotJson: "{\"originAllowlist\":[\"https://example.com\"]}");
            var result = await connector.PublishAsync(ctx, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("active", result.Status);
            Assert.False(string.IsNullOrEmpty(result.PublicMetadataJson));

            using var doc = JsonDocument.Parse(result.PublicMetadataJson!);
            var meta = doc.RootElement;
            var endpoint = meta.GetProperty("endpoint").GetString();
            Assert.Equal($"/api/v1/runtime/channels/web-sdk/{channelId}/messages", endpoint);
            var snippet = meta.GetProperty("snippet").GetString();
            Assert.Contains("AtlasWebSdk", snippet, StringComparison.Ordinal);
            Assert.Contains("https://example.com", snippet, StringComparison.Ordinal);
            var secret = meta.GetProperty("secret").GetString();
            Assert.False(string.IsNullOrWhiteSpace(secret));

            // channel 被写入加密 SecretJson
            var channel = await db.Queryable<WorkspacePublishChannel>().FirstAsync(c => c.Id == channelId);
            Assert.False(string.IsNullOrEmpty(channel.SecretJson));
            Assert.StartsWith("lcp:", channel.SecretJson);
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
            var channelId = await SeedChannel(db, "web-sdk");
            var (connector, _) = BuildConnector(db);

            var ctx = new ChannelPublishContext(
                Tenant, ResolveLong(WorkspaceId), channelId, "web-sdk",
                AgentId: null, AgentPublicationId: null, Version: 1, ReleasedByUserId: 9527,
                ConfigSnapshotJson: "{}");
            var result = await connector.PublishAsync(ctx, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("failed", result.Status);
            Assert.Equal("WebSdkRequiresAgentId", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldDispatchToAgent_WhenSignatureAndOriginValid()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "web-sdk");
            var (connector, fakeChat) = BuildConnector(db);

            // 先发布以写入 secret
            var publishResult = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, ResolveLong(WorkspaceId), channelId, "web-sdk",
                AgentId: 1001, AgentPublicationId: null, Version: 1, ReleasedByUserId: 9527,
                ConfigSnapshotJson: "{\"originAllowlist\":[\"https://example.com\"]}"), CancellationToken.None);
            using var meta = JsonDocument.Parse(publishResult.PublicMetadataJson!);
            var secret = meta.RootElement.GetProperty("secret").GetString()!;

            var body = "{\"message\":\"hi\"}";
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nonce = "nonce-1";
            var sig = HmacChannelSigner.Compute(secret, ts, nonce, body);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-channel-signature"] = sig,
                ["x-channel-timestamp"] = ts.ToString(),
                ["x-channel-nonce"] = nonce,
                ["origin"] = "https://example.com"
            };
            var inbound = new ChannelInboundContext(
                Tenant, channelId, "web-sdk", "message", null, null, body, headers);

            var result = await connector.HandleInboundAsync(inbound, CancellationToken.None);

            Assert.True(result.Handled);
            Assert.Single(fakeChat.Calls);
            Assert.Equal(1001L, fakeChat.Calls[0].AgentId);
            Assert.Equal("hi", fakeChat.Calls[0].Request.Message);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldReject_WhenOriginNotAllowed()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "web-sdk");
            var (connector, _) = BuildConnector(db);

            var publishResult = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, ResolveLong(WorkspaceId), channelId, "web-sdk",
                AgentId: 1001, AgentPublicationId: null, Version: 1, ReleasedByUserId: 9527,
                ConfigSnapshotJson: "{\"originAllowlist\":[\"https://allowed.com\"]}"), CancellationToken.None);
            using var meta = JsonDocument.Parse(publishResult.PublicMetadataJson!);
            var secret = meta.RootElement.GetProperty("secret").GetString()!;

            var body = "{\"message\":\"hi\"}";
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sig = HmacChannelSigner.Compute(secret, ts, "n", body);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-channel-signature"] = sig,
                ["x-channel-timestamp"] = ts.ToString(),
                ["x-channel-nonce"] = "n",
                ["origin"] = "https://evil.com"
            };
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "web-sdk", "message", null, null, body, headers), CancellationToken.None);

            Assert.False(result.Handled);
            Assert.Equal("WebSdkOriginRejected", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldReject_WhenSignatureMismatches()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "web-sdk");
            var (connector, _) = BuildConnector(db);

            var publishResult = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, ResolveLong(WorkspaceId), channelId, "web-sdk",
                AgentId: 1001, AgentPublicationId: null, Version: 1, ReleasedByUserId: 9527,
                ConfigSnapshotJson: "{\"originAllowlist\":[\"https://example.com\"]}"), CancellationToken.None);
            // 用错误 secret 签名
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sig = HmacChannelSigner.Compute("wrong-secret", ts, "n", "{}");

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-channel-signature"] = sig,
                ["x-channel-timestamp"] = ts.ToString(),
                ["x-channel-nonce"] = "n",
                ["origin"] = "https://example.com"
            };
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "web-sdk", "message", null, null, "{}", headers), CancellationToken.None);

            Assert.False(result.Handled);
            Assert.Equal("WebSdkSignatureMismatch", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldReject_WhenChannelNeverPublished()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "web-sdk");
            var (connector, _) = BuildConnector(db);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "web-sdk", "message", null, null, "{}", headers), CancellationToken.None);

            Assert.False(result.Handled);
            Assert.Equal("WebSdkChannelNotPublished", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static (WebSdkChannelConnector Connector, FakeAgentChatService FakeChat) BuildConnector(SqlSugarClient db)
    {
        var channelRepo = new WorkspacePublishChannelRepository(db);
        var releaseRepo = new WorkspaceChannelReleaseRepository(db);
        var protector = new LowCodeCredentialProtector(BuildConfig());
        var fakeChat = new FakeAgentChatService();
        var connector = new WebSdkChannelConnector(channelRepo, releaseRepo, protector, fakeChat, NullLogger<WebSdkChannelConnector>.Instance);
        return (connector, fakeChat);
    }

    private static IConfiguration BuildConfig()
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:LowCode:CredentialProtectorKey"] = "atlas-test-credential-key-2026"
        }).Build();
    }

    private static long ResolveLong(string id) => long.TryParse(id, out var v) ? v : 0L;

    private static async Task<long> SeedChannel(SqlSugarClient db, string channelType)
    {
        const long ChannelId = 200002L;
        await db.Ado.ExecuteCommandAsync(
            "INSERT INTO WorkspacePublishChannel (Id, TenantIdValue, WorkspaceId, Name, ChannelType, Status, AuthStatus, Description, SupportedTargetsJson, LastSyncAt, CreatedAt, SecretJson) " +
            "VALUES (@Id, @Tenant, @Ws, 'web-channel', @Type, 'pending', 'unauthorized', 'test', '[\"agent\"]', '1970-01-01 00:00:00', @CreatedAt, NULL)",
            new SugarParameter[]
            {
                new("@Id", ChannelId),
                new("@Tenant", Tenant.Value.ToString()),
                new("@Ws", WorkspaceId),
                new("@Type", channelType),
                new("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            });
        return ChannelId;
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

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"web-sdk-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private sealed class FakeAgentChatService : IAgentChatService
    {
        public List<(long AgentId, long UserId, AgentChatRequest Request)> Calls { get; } = new();

        public Task<AgentChatResponse> ChatAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
        {
            Calls.Add((agentId, userId, request));
            return Task.FromResult(new AgentChatResponse(
                ConversationId: 5001,
                MessageId: 6001,
                Content: $"echo:{request.Message}",
                Sources: null));
        }

        public IAsyncEnumerable<string> ChatStreamAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
            => AsyncEnumerable.Empty<string>();

        public IAsyncEnumerable<AgentChatStreamEvent> ChatEventStreamAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
            => AsyncEnumerable.Empty<AgentChatStreamEvent>();

        public Task CancelAsync(TenantId tenantId, long userId, long agentId, long conversationId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private static class AsyncEnumerable
    {
        public static IAsyncEnumerable<T> Empty<T>()
        {
            return new EmptyAsyncEnumerable<T>();
        }
        private sealed class EmptyAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                => new EmptyEnumerator<T>();
        }
        private sealed class EmptyEnumerator<T> : IAsyncEnumerator<T>
        {
            public T Current => default!;
            public ValueTask DisposeAsync() => default;
            public ValueTask<bool> MoveNextAsync() => new(false);
        }
    }
}
