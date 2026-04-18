using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C10：WechatMpApiClient（access_token 缓存 + 客服消息发送）。
/// </summary>
public sealed class WechatMpApiClientTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000005"));
    private const long ChannelId = 555000;

    [Fact]
    public async Task GetAccessToken_ShouldFetchAndCache()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedCredential(db);
            var fakeHandler = new FakeHandler((req, ct) =>
                JsonResponse(200, "{\"access_token\":\"wx-tk-1\",\"expires_in\":7200}"));
            var client = BuildClient(db, fakeHandler);

            var t1 = await client.GetAccessTokenAsync(ChannelId, "wxapp-1", "secret-1", CancellationToken.None);
            var t2 = await client.GetAccessTokenAsync(ChannelId, "wxapp-1", "secret-1", CancellationToken.None);

            Assert.Equal("wx-tk-1", t1);
            Assert.Equal("wx-tk-1", t2);
            Assert.Equal(1, fakeHandler.CallCount);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task GetAccessToken_ShouldThrow_WhenWechatReturnsErrcode()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedCredential(db, channelId: ChannelId + 1);
            var fakeHandler = new FakeHandler((req, ct) =>
                JsonResponse(200, "{\"errcode\":40013,\"errmsg\":\"invalid appid\"}"));
            var client = BuildClient(db, fakeHandler);

            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                client.GetAccessTokenAsync(ChannelId + 1, "bad-appid", "x", CancellationToken.None));
            Assert.Equal("WECHAT_MP_TOKEN_FAILED", ex.Code);
            Assert.Contains("40013", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task SendCustomerMessage_ShouldThrowOnFailure()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedCredential(db, channelId: ChannelId + 2);
            var fakeHandler = new FakeHandler((req, ct) =>
            {
                Assert.Equal("/cgi-bin/message/custom/send", req.RequestUri!.AbsolutePath);
                return JsonResponse(200, "{\"errcode\":45047,\"errmsg\":\"too many keywords\"}");
            });
            var client = BuildClient(db, fakeHandler);

            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                client.SendCustomerMessageAsync("tk", "ouser", "text", "hi", CancellationToken.None));
            Assert.Equal("WECHAT_MP_API_FAILED", ex.Code);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static WechatMpApiClient BuildClient(SqlSugarClient db, FakeHandler handler)
    {
        var factory = new SingleClientHttpFactory(handler);
        var repo = new WechatMpChannelCredentialRepository(db);
        var tenantProvider = new FixedTenantProvider(Tenant);
        return new WechatMpApiClient(factory, repo, tenantProvider, NullLogger<WechatMpApiClient>.Instance);
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
        db.CodeFirst.InitTables<WechatMpChannelCredential>();
        await Task.CompletedTask;
    }

    private static async Task SeedCredential(SqlSugarClient db, long channelId = ChannelId)
    {
        var entity = new WechatMpChannelCredential(
            Tenant, channelId, "ws-wx",
            appId: $"wx-{channelId}",
            appSecretEnc: "lcp:dummy",
            token: "tok",
            encodingAesKeyEnc: string.Empty,
            agentBindingsJson: "[]",
            id: 800000 + channelId);
        await db.Insertable(entity).ExecuteCommandAsync();
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"wxmp-api-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private static HttpResponseMessage JsonResponse(int statusCode, string body)
        => new((HttpStatusCode)statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;
        public int CallCount { get; private set; }
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }

    private sealed class SingleClientHttpFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleClientHttpFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class FixedTenantProvider : ITenantProvider
    {
        private readonly TenantId _tenantId;
        public FixedTenantProvider(TenantId tenantId) => _tenantId = tenantId;
        public TenantId GetTenantId() => _tenantId;
    }
}
