using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Feishu;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C7：FeishuChannelConnector 行为。
/// </summary>
public sealed class FeishuChannelConnectorTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000004"));
    private const string WorkspaceId = "ws-feishu-conn";

    [Fact]
    public async Task PublishAsync_ShouldSucceed_WhenCredentialPresentAndTokenWorks()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "feishu", id: 500001);
            await SeedCredential(db, channelId);
            var fakeApi = new FakeFeishuApiClient();
            fakeApi.NextToken = "tk-success";
            var (connector, _) = BuildConnector(db, fakeApi);

            var ctx = new ChannelPublishContext(
                Tenant, 0, channelId, "feishu",
                AgentId: 9001, AgentPublicationId: null, Version: 1, ReleasedByUserId: 9527,
                ConfigSnapshotJson: "{}");
            var result = await connector.PublishAsync(ctx, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("active", result.Status);
            using var meta = JsonDocument.Parse(result.PublicMetadataJson!);
            Assert.Equal($"/api/v1/runtime/channels/feishu/{channelId}/webhook",
                meta.RootElement.GetProperty("webhookUrl").GetString());
            Assert.Equal(1, fakeApi.GetTokenCalls);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task PublishAsync_ShouldFail_WhenCredentialMissing()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "feishu", id: 500002);
            var fakeApi = new FakeFeishuApiClient();
            var (connector, _) = BuildConnector(db, fakeApi);

            var result = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, 0, channelId, "feishu", 9001, null, 1, 9527, "{}"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("FeishuCredentialMissing", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldReturnChallenge_OnUrlVerification()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "feishu", id: 500003);
            var fakeApi = new FakeFeishuApiClient();
            var (connector, _) = BuildConnector(db, fakeApi);

            var body = "{\"type\":\"url_verification\",\"challenge\":\"abc123\"}";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "feishu", "webhook", null, null, body, headers), CancellationToken.None);

            Assert.True(result.Handled);
            using var doc = JsonDocument.Parse(result.AgentResponseJson!);
            Assert.Equal("abc123", doc.RootElement.GetProperty("challenge").GetString());
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldDispatch_OnImMessageReceiveV1()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "feishu", id: 500004);
            await SeedCredential(db, channelId);
            // bind agent 8001
            var credRepo = new FeishuChannelCredentialRepository(db);
            var entity = await credRepo.FindByChannelAsync(Tenant, channelId, default);
            entity!.Update(entity.AppId, entity.AppSecretEnc, entity.VerificationToken, entity.EncryptKeyEnc,
                JsonSerializer.Serialize(new[] { new { agentId = 8001L } }));
            await credRepo.UpdateAsync(entity, default);

            var fakeApi = new FakeFeishuApiClient();
            fakeApi.NextToken = "tk-1";
            var (connector, fakeChat) = BuildConnector(db, fakeApi);

            var body = "{\"header\":{\"event_type\":\"im.message.receive_v1\",\"token\":\"vtoken\"}," +
                       "\"event\":{\"sender\":{\"sender_type\":\"user\",\"sender_id\":{\"open_id\":\"ou_caller\"}}," +
                       "\"message\":{\"message_id\":\"om_unique_msg_1\",\"content\":\"{\\\"text\\\":\\\"你好\\\"}\"}}}";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "feishu", "webhook", null, null, body, headers), CancellationToken.None);

            Assert.True(result.Handled);
            Assert.Single(fakeChat.Calls);
            Assert.Equal(8001L, fakeChat.Calls[0].AgentId);
            Assert.Equal("你好", fakeChat.Calls[0].Request.Message);
            // 异步回包：应触发一次 SendImMessageAsync
            Assert.Equal(1, fakeApi.SendImCalls);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldRejectBadVerificationToken()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "feishu", id: 500005);
            await SeedCredential(db, channelId);
            var (connector, _) = BuildConnector(db, new FakeFeishuApiClient());

            var body = "{\"header\":{\"event_type\":\"im.message.receive_v1\",\"token\":\"WRONG\"}," +
                       "\"event\":{\"sender\":{\"sender_id\":{\"open_id\":\"ou_x\"}}," +
                       "\"message\":{\"message_id\":\"om_x\",\"content\":\"{}\"}}}";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "feishu", "webhook", null, null, body, headers), CancellationToken.None);

            Assert.False(result.Handled);
            Assert.Equal("FeishuVerificationTokenMismatch", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldDeduplicate_OnRepeatedMessageId()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "feishu", id: 500006);
            await SeedCredential(db, channelId);
            var credRepo = new FeishuChannelCredentialRepository(db);
            var entity = await credRepo.FindByChannelAsync(Tenant, channelId, default);
            entity!.Update(entity.AppId, entity.AppSecretEnc, entity.VerificationToken, entity.EncryptKeyEnc,
                JsonSerializer.Serialize(new[] { new { agentId = 8002L } }));
            await credRepo.UpdateAsync(entity, default);

            var fakeApi = new FakeFeishuApiClient { NextToken = "tk-dup" };
            var (connector, fakeChat) = BuildConnector(db, fakeApi);

            var dedupId = "om_dedup_" + Guid.NewGuid().ToString("N")[..6];
            var body = "{\"header\":{\"event_type\":\"im.message.receive_v1\",\"token\":\"vtoken\"}," +
                       "\"event\":{\"sender\":{\"sender_id\":{\"open_id\":\"ou_dedup\"}}," +
                       "\"message\":{\"message_id\":\"" + dedupId + "\",\"content\":\"{\\\"text\\\":\\\"hi\\\"}\"}}}";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var first = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "feishu", "webhook", null, null, body, headers), CancellationToken.None);
            var second = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "feishu", "webhook", null, null, body, headers), CancellationToken.None);

            Assert.True(first.Handled);
            Assert.True(second.Handled);
            Assert.Single(fakeChat.Calls); // 第二次应被去重，不再调用 chat
            using var doc = JsonDocument.Parse(second.AgentResponseJson!);
            Assert.True(doc.RootElement.GetProperty("deduped").GetBoolean());
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static (FeishuChannelConnector Connector, FakeAgentChatService Chat) BuildConnector(SqlSugarClient db, IFeishuApiClient feishuApi)
    {
        var channelRepo = new WorkspacePublishChannelRepository(db);
        var releaseRepo = new WorkspaceChannelReleaseRepository(db);
        var credRepo = new FeishuChannelCredentialRepository(db);
        var protector = new LowCodeCredentialProtector(BuildConfig());
        var chat = new FakeAgentChatService();
        var connector = new FeishuChannelConnector(channelRepo, releaseRepo, credRepo, feishuApi, protector, chat,
            NullLogger<FeishuChannelConnector>.Instance);
        return (connector, chat);
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
            "VALUES (@Id, @Tenant, @Ws, 'feishu-test', @Type, 'pending', 'unauthorized', 'test', '[\"agent\"]', '1970-01-01 00:00:00', @CreatedAt, NULL)",
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

    private static async Task SeedCredential(SqlSugarClient db, long channelId)
    {
        var protector = new LowCodeCredentialProtector(BuildConfig());
        var entity = new FeishuChannelCredential(
            Tenant, channelId, WorkspaceId,
            appId: $"app-{channelId}",
            appSecretEnc: protector.Encrypt("secret-1"),
            verificationToken: "vtoken",
            encryptKeyEnc: string.Empty,
            agentBindingsJson: "[]",
            id: 700000 + channelId);
        await db.Insertable(entity).ExecuteCommandAsync();
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
        db.CodeFirst.InitTables<FeishuChannelCredential>();
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"feishu-conn-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private sealed class FakeFeishuApiClient : IFeishuApiClient
    {
        public string NextToken { get; set; } = "tk-default";
        public int GetTokenCalls { get; private set; }
        public int SendImCalls { get; private set; }

        public Task<string> GetTenantAccessTokenAsync(long channelId, string appId, string appSecret, CancellationToken cancellationToken)
        {
            GetTokenCalls++;
            return Task.FromResult(NextToken);
        }

        public Task SendImMessageAsync(string tenantAccessToken, string receiveIdType, string receiveId, string msgType, string content, CancellationToken cancellationToken)
        {
            SendImCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAgentChatService : IAgentChatService
    {
        public List<(long AgentId, AgentChatRequest Request)> Calls { get; } = new();

        public Task<AgentChatResponse> ChatAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
        {
            Calls.Add((agentId, request));
            return Task.FromResult(new AgentChatResponse(11, 22, $"echo:{request.Message}", null));
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
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new Enum();
        private sealed class Enum : IAsyncEnumerator<T>
        {
            public T Current => default!;
            public ValueTask DisposeAsync() => default;
            public ValueTask<bool> MoveNextAsync() => new(false);
        }
    }
}
