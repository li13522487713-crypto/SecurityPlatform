using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services.AiPlatform;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class AiDatabaseAccessPolicyTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public void For_MultiUserOpen_ShouldYieldOpenPolicy()
    {
        var db = new AiDatabase(Tenant, "demo", null, null, "[]", 1L);
        var policy = AiDatabaseAccessPolicy.For(db, userId: 100, channelId: "web");
        Assert.Null(policy.OwnerUserId);
        Assert.Null(policy.ChannelId);
    }

    [Fact]
    public void For_SingleUser_WithoutUserId_ShouldThrow()
    {
        var db = new AiDatabase(Tenant, "demo", null, null, "[]", 2L, queryMode: AiDatabaseQueryMode.SingleUser);
        Assert.Throws<BusinessException>(() => AiDatabaseAccessPolicy.For(db, userId: null, channelId: null));
    }

    [Fact]
    public void For_ChannelScope_WithoutChannelId_ShouldThrow()
    {
        var db = new AiDatabase(
            Tenant,
            "demo",
            null,
            null,
            "[]",
            2L,
            queryMode: AiDatabaseQueryMode.MultiUser,
            channelScope: AiDatabaseChannelScope.Channel);
        Assert.Throws<BusinessException>(() => AiDatabaseAccessPolicy.For(db, userId: 1, channelId: null));
        Assert.Throws<BusinessException>(() => AiDatabaseAccessPolicy.For(db, userId: 1, channelId: "   "));
    }

    [Fact]
    public void For_SingleUserChannel_ShouldRestrictBoth()
    {
        var db = new AiDatabase(
            Tenant, "demo", null, null, "[]", 2L,
            queryMode: AiDatabaseQueryMode.SingleUser,
            channelScope: AiDatabaseChannelScope.Channel);
        var policy = AiDatabaseAccessPolicy.For(db, userId: 7, channelId: "bot-001");
        Assert.Equal(7, policy.OwnerUserId);
        Assert.Equal("bot-001", policy.ChannelId);
    }

    [Fact]
    public void IsRecordVisible_ShouldHonorOwnerAndChannel()
    {
        var policy = new AiDatabaseAccessPolicy(OwnerUserId: 7, ChannelId: "bot-001");
        Assert.True(policy.IsRecordVisible(recordOwnerUserId: 7, recordChannelId: "bot-001"));
        Assert.True(policy.IsRecordVisible(recordOwnerUserId: null, recordChannelId: null), "NULL 旧记录默认对所有人可见");
        Assert.False(policy.IsRecordVisible(recordOwnerUserId: 8, recordChannelId: "bot-001"));
        Assert.False(policy.IsRecordVisible(recordOwnerUserId: 7, recordChannelId: "bot-other"));
    }
}
