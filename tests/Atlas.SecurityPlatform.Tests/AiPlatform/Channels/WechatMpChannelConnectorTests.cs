using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
using Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C11：WechatMpChannelConnector 行为。
/// </summary>
public sealed class WechatMpChannelConnectorTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000006"));
    private const string WorkspaceId = "ws-wx-conn";
    private const string Token = "wxtoken";

    [Fact]
    public async Task PublishAsync_ShouldSucceed_WhenCredentialPresentAndTokenWorks()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "wechat-mp", id: 600001);
            await SeedCredential(db, channelId);
            var fakeApi = new FakeWechatMpApiClient { NextToken = "wx-tk" };
            var (connector, _) = BuildConnector(db, fakeApi);

            var result = await connector.PublishAsync(new ChannelPublishContext(
                Tenant, 0, channelId, "wechat-mp", 9100, null, 1, 9527, "{}"), CancellationToken.None);

            Assert.True(result.Success);
            using var meta = JsonDocument.Parse(result.PublicMetadataJson!);
            Assert.Equal($"/api/v1/runtime/channels/wechat-mp/{channelId}/webhook",
                meta.RootElement.GetProperty("webhookUrl").GetString());
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInbound_ShouldReturnEchostr_OnGetVerification()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "wechat-mp", id: 600002);
            await SeedCredential(db, channelId);
            var (connector, _) = BuildConnector(db, new FakeWechatMpApiClient());

            var ts = "1700000000";
            var nonce = "n-1";
            var signature = ComputeSignature(Token, ts, nonce);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["signature"] = signature,
                ["timestamp"] = ts,
                ["nonce"] = nonce,
                ["echostr"] = "echo-12345"
            };

            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "wechat-mp", "webhook", null, null, PayloadJson: string.Empty, headers), CancellationToken.None);

            Assert.True(result.Handled);
            using var doc = JsonDocument.Parse(result.AgentResponseJson!);
            Assert.Equal("echo-12345", doc.RootElement.GetProperty("echostr").GetString());
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInbound_ShouldReject_WhenSignatureMismatches()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "wechat-mp", id: 600003);
            await SeedCredential(db, channelId);
            var (connector, _) = BuildConnector(db, new FakeWechatMpApiClient());

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["signature"] = "definitely-wrong",
                ["timestamp"] = "1",
                ["nonce"] = "x"
            };
            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "wechat-mp", "webhook", null, null, PayloadJson: "<xml/>", headers), CancellationToken.None);

            Assert.False(result.Handled);
            Assert.Equal("WechatMpSignatureMismatch", result.FailureReason);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInbound_ShouldDispatchTextMessage_AndCallCustomerSend()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "wechat-mp", id: 600004);
            await SeedCredential(db, channelId);
            var credRepo = new WechatMpChannelCredentialRepository(db);
            var entity = await credRepo.FindByChannelAsync(Tenant, channelId, default);
            entity!.Update(entity.AppId, entity.AppSecretEnc, entity.Token, entity.EncodingAesKeyEnc,
                JsonSerializer.Serialize(new[] { new { agentId = 7700L } }));
            await credRepo.UpdateAsync(entity, default);

            var fakeApi = new FakeWechatMpApiClient { NextToken = "tk-x" };
            var (connector, fakeChat) = BuildConnector(db, fakeApi);

            var ts = "1700000001";
            var nonce = "n-2";
            var signature = ComputeSignature(Token, ts, nonce);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["signature"] = signature,
                ["timestamp"] = ts,
                ["nonce"] = nonce
            };
            var xml = "<xml>" +
                     "<ToUserName><![CDATA[gh_xxxx]]></ToUserName>" +
                     "<FromUserName><![CDATA[oUser1234]]></FromUserName>" +
                     "<CreateTime>1700000000</CreateTime>" +
                     "<MsgType><![CDATA[text]]></MsgType>" +
                     "<Content><![CDATA[查询订单]]></Content>" +
                     "<MsgId>1234567890123456</MsgId>" +
                     "</xml>";

            var result = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "wechat-mp", "webhook", null, null, xml, headers), CancellationToken.None);

            Assert.True(result.Handled);
            Assert.Single(fakeChat.Calls);
            Assert.Equal(7700L, fakeChat.Calls[0].AgentId);
            Assert.Equal("查询订单", fakeChat.Calls[0].Request.Message);
            Assert.Equal(1, fakeApi.SendCalls);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task HandleInbound_ShouldDeduplicate_OnRepeatedMsgId()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var channelId = await SeedChannel(db, "wechat-mp", id: 600005);
            await SeedCredential(db, channelId);
            var credRepo = new WechatMpChannelCredentialRepository(db);
            var entity = await credRepo.FindByChannelAsync(Tenant, channelId, default);
            entity!.Update(entity.AppId, entity.AppSecretEnc, entity.Token, entity.EncodingAesKeyEnc,
                JsonSerializer.Serialize(new[] { new { agentId = 7701L } }));
            await credRepo.UpdateAsync(entity, default);

            var fakeApi = new FakeWechatMpApiClient { NextToken = "tk-y" };
            var (connector, fakeChat) = BuildConnector(db, fakeApi);

            var ts = "1700000002";
            var nonce = "n-3";
            var signature = ComputeSignature(Token, ts, nonce);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["signature"] = signature,
                ["timestamp"] = ts,
                ["nonce"] = nonce
            };
            var msgId = "wx-dedup-" + Guid.NewGuid().ToString("N")[..6];
            var xml = "<xml><ToUserName>gh</ToUserName><FromUserName>ou</FromUserName><CreateTime>0</CreateTime>" +
                     "<MsgType>text</MsgType><Content>hi</Content><MsgId>" + msgId + "</MsgId></xml>";

            var first = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "wechat-mp", "webhook", null, null, xml, headers), CancellationToken.None);
            var second = await connector.HandleInboundAsync(new ChannelInboundContext(
                Tenant, channelId, "wechat-mp", "webhook", null, null, xml, headers), CancellationToken.None);

            Assert.True(first.Handled);
            Assert.True(second.Handled);
            Assert.Single(fakeChat.Calls);
            using var doc = JsonDocument.Parse(second.AgentResponseJson!);
            Assert.True(doc.RootElement.GetProperty("deduped").GetBoolean());
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static string ComputeSignature(string token, string timestamp, string nonce)
    {
        var arr = new[] { token, timestamp, nonce };
        Array.Sort(arr, StringComparer.Ordinal);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(string.Concat(arr)));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static (WechatMpChannelConnector Connector, FakeAgentChatService Chat) BuildConnector(SqlSugarClient db, IWechatMpApiClient api)
    {
        var channelRepo = new WorkspacePublishChannelRepository(db);
        var releaseRepo = new WorkspaceChannelReleaseRepository(db);
        var credRepo = new WechatMpChannelCredentialRepository(db);
        var protector = new LowCodeCredentialProtector(BuildConfig());
        var chat = new FakeAgentChatService();
        var connector = new WechatMpChannelConnector(
            channelRepo, releaseRepo, credRepo, api, protector, chat,
            NullLogger<WechatMpChannelConnector>.Instance);
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
            "VALUES (@Id, @Tenant, @Ws, 'wx-test', @Type, 'pending', 'unauthorized', 'test', '[\"agent\"]', '1970-01-01 00:00:00', @CreatedAt, NULL)",
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
        var entity = new WechatMpChannelCredential(
            Tenant, channelId, WorkspaceId,
            appId: $"wx-app-{channelId}",
            appSecretEnc: protector.Encrypt("wx-secret"),
            token: Token,
            encodingAesKeyEnc: string.Empty,
            agentBindingsJson: "[]",
            id: 900000 + channelId);
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
        db.CodeFirst.InitTables<WechatMpChannelCredential>();
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"wxmp-conn-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class FakeWechatMpApiClient : IWechatMpApiClient
    {
        public string NextToken { get; set; } = "tk-default";
        public int SendCalls { get; private set; }
        public Task<string> GetAccessTokenAsync(long channelId, string appId, string appSecret, CancellationToken cancellationToken)
            => Task.FromResult(NextToken);
        public Task SendCustomerMessageAsync(string accessToken, string toUser, string msgType, string content, CancellationToken cancellationToken)
        {
            SendCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAgentChatService : IAgentChatService
    {
        public List<(long AgentId, AgentChatRequest Request)> Calls { get; } = new();
        public Task<AgentChatResponse> ChatAsync(TenantId tenantId, long userId, long agentId, AgentChatRequest request, CancellationToken cancellationToken)
        {
            Calls.Add((agentId, request));
            return Task.FromResult(new AgentChatResponse(31, 32, $"echo:{request.Message}", null));
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
