using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Coze.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat;
using Atlas.Infrastructure.Services.Coze;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

public sealed class WechatChannelCredentialServicesTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000007"));
    private const string WorkspaceId = "ws-wechat-credentials";

    [Fact]
    public async Task WechatMiniappCredentialService_ShouldUpsertAndReturnMaskedDto()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            await SeedChannelAsync(db, 810001, "wechat-miniapp");
            var protector = BuildProtector();
            var service = new WechatMiniappChannelCredentialService(
                new WechatMiniappChannelCredentialRepository(db),
                new WorkspacePublishChannelRepository(db),
                protector,
                new FixedIdGeneratorAccessor(920001));

            var dto = await service.UpsertAsync(
                Tenant,
                WorkspaceId,
                "810001",
                new WechatMiniappChannelCredentialUpsertRequest(
                    AppId: "wx-mini-001",
                    AppSecret: "mini-secret",
                    OriginalId: "gh_mini_001",
                    MessageToken: "mini-token",
                    EncodingAesKey: "mini-aes"),
                CancellationToken.None);

            Assert.Equal("wx-mini-001", dto.AppId);
            Assert.NotEqual(dto.AppId, dto.AppIdMasked);
            Assert.Equal("gh_mini_001", dto.OriginalId);
            Assert.Equal("mini-token", dto.MessageToken);
            Assert.True(dto.HasEncodingAesKey);

            var entity = await new WechatMiniappChannelCredentialRepository(db).FindByChannelAsync(Tenant, 810001, CancellationToken.None);
            Assert.NotNull(entity);
            Assert.StartsWith("lcp:", entity!.AppSecretEnc, StringComparison.Ordinal);
            Assert.Equal("mini-secret", protector.Decrypt(entity.AppSecretEnc));
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task WechatCsCredentialService_ShouldDeleteStoredCredential()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            await SeedChannelAsync(db, 810002, "wechat-cs");
            var protector = BuildProtector();
            var service = new WechatCsChannelCredentialService(
                new WechatCsChannelCredentialRepository(db),
                new WorkspacePublishChannelRepository(db),
                protector,
                new FixedIdGeneratorAccessor(920002));

            await service.UpsertAsync(
                Tenant,
                WorkspaceId,
                "810002",
                new WechatCsChannelCredentialUpsertRequest(
                    CorpId: "ww-kf-001",
                    Secret: "kf-secret",
                    OpenKfId: "wkf_open_001",
                    Token: "kf-token",
                    EncodingAesKey: null),
                CancellationToken.None);

            await service.DeleteAsync(Tenant, WorkspaceId, "810002", CancellationToken.None);

            var entity = await new WechatCsChannelCredentialRepository(db).FindByChannelAsync(Tenant, 810002, CancellationToken.None);
            Assert.Null(entity);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    [Theory]
    [InlineData("wechat-miniapp")]
    [InlineData("wechat-cs")]
    public async Task WorkspacePublishChannelService_ShouldAllowNewWechatTypes(string channelType)
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchemaAsync(db);
            var service = new WorkspacePublishChannelService(
                new WorkspacePublishChannelRepository(db),
                new FixedIdGeneratorAccessor(930000));

            var channelId = await service.CreateAsync(
                Tenant,
                WorkspaceId,
                new WorkspacePublishChannelCreateRequest(
                    Name: $"test-{channelType}",
                    Type: channelType,
                    Description: null,
                    SupportedTargets: new[] { "agent", "workflow" }),
                CancellationToken.None);

            Assert.False(string.IsNullOrWhiteSpace(channelId));
            var entity = await new WorkspacePublishChannelRepository(db).FindAsync(Tenant, WorkspaceId, long.Parse(channelId), CancellationToken.None);
            Assert.NotNull(entity);
            Assert.Equal(channelType, entity!.ChannelType);
        }
        finally
        {
            DeleteDb(dbPath);
        }
    }

    private static async Task CreateSchemaAsync(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<WorkspacePublishChannel>();
        db.CodeFirst.InitTables<WechatMiniappChannelCredential>();
        db.CodeFirst.InitTables<WechatCsChannelCredential>();
        await Task.CompletedTask;
    }

    private static async Task SeedChannelAsync(SqlSugarClient db, long channelId, string channelType)
    {
        var entity = new WorkspacePublishChannel(
            Tenant,
            WorkspaceId,
            $"channel-{channelId}",
            channelType,
            string.Empty,
            "[]",
            channelId);
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

    private static LowCodeCredentialProtector BuildProtector()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:LowCode:CredentialProtectorKey"] = "atlas-test-credential-key-2026"
            })
            .Build();
        return new LowCodeCredentialProtector(config);
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"wechat-credential-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private sealed class FixedIdGeneratorAccessor : IIdGeneratorAccessor
    {
        private long _current;

        public FixedIdGeneratorAccessor(long seed)
        {
            _current = seed;
        }

        public long NextId()
        {
            _current += 1;
            return _current;
        }
    }
}
