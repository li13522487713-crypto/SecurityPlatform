using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Feishu;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C6：FeishuApiClient（token 缓存 + im messages）。
/// 通过自定义 HttpMessageHandler 注入响应。
/// </summary>
public sealed class FeishuApiClientTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private const long ChannelId = 444444;

    [Fact]
    public async Task GetTenantAccessToken_ShouldFetchAndCache()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedCredential(db);

            var fakeHandler = new FakeHandler((req, ct) =>
            {
                Assert.Contains("/open-apis/auth/v3/tenant_access_token/internal", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
                return JsonResponse(200, "{\"code\":0,\"msg\":\"ok\",\"tenant_access_token\":\"t-123\",\"expire\":7200}");
            });
            var client = BuildClient(db, fakeHandler);

            var t1 = await client.GetTenantAccessTokenAsync(ChannelId, "appid-1", "secret-1", CancellationToken.None);
            var t2 = await client.GetTenantAccessTokenAsync(ChannelId, "appid-1", "secret-1", CancellationToken.None);

            Assert.Equal("t-123", t1);
            Assert.Equal("t-123", t2);
            // 第二次命中缓存：HTTP 调用应仅 1 次
            Assert.Equal(1, fakeHandler.CallCount);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task GetTenantAccessToken_ShouldThrow_WhenFeishuReturnsNonZeroCode()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedCredential(db, channelId: ChannelId + 1);

            var fakeHandler = new FakeHandler((req, ct) =>
                JsonResponse(200, "{\"code\":99991663,\"msg\":\"app secret invalid\"}"));
            var client = BuildClient(db, fakeHandler);

            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                client.GetTenantAccessTokenAsync(ChannelId + 1, "appid-x", "secret-bad", CancellationToken.None));
            Assert.Equal("FEISHU_TOKEN_FAILED", ex.Code);
            Assert.Contains("99991663", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task SendImMessage_ShouldPostBody_AndThrowOnFailure()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedCredential(db, channelId: ChannelId + 2);

            var fakeHandler = new FakeHandler((req, ct) =>
            {
                Assert.Equal("/open-apis/im/v1/messages", req.RequestUri!.AbsolutePath);
                Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
                return JsonResponse(200, "{\"code\":230002,\"msg\":\"bot has not been added to the chat\"}");
            });
            var client = BuildClient(db, fakeHandler);

            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                client.SendImMessageAsync("token-x", "open_id", "ou_xxx", "text", "{\"text\":\"hi\"}", CancellationToken.None));
            Assert.Equal("FEISHU_API_FAILED", ex.Code);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static FeishuApiClient BuildClient(SqlSugarClient db, FakeHandler handler)
    {
        var factory = new SingleClientHttpFactory(handler);
        var repo = new FeishuChannelCredentialRepository(db);
        var tenantProvider = new FixedTenantProvider(Tenant);
        return new FeishuApiClient(factory, repo, tenantProvider, NullLogger<FeishuApiClient>.Instance);
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
        db.CodeFirst.InitTables<FeishuChannelCredential>();
        await Task.CompletedTask;
    }

    private static async Task SeedCredential(SqlSugarClient db, long channelId = ChannelId)
    {
        var entity = new FeishuChannelCredential(
            Tenant, channelId, "ws-feishu",
            appId: $"app-{channelId}",
            appSecretEnc: "lcp:dummy",
            verificationToken: "vtoken",
            encryptKeyEnc: string.Empty,
            agentBindingsJson: "[]",
            id: 700000 + channelId);
        await db.Insertable(entity).ExecuteCommandAsync();
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"feishu-api-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static HttpResponseMessage JsonResponse(int statusCode, string body)
    {
        return new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

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
