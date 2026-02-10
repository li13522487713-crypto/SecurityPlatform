using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.SecurityPlatform.Tests.Domain;

public sealed class TenantEntityTests
{
    private static readonly TenantId TenantA = new(Guid.Parse("aaaa0000-0000-0000-0000-000000000001"));
    private static readonly TenantId TenantB = new(Guid.Parse("bbbb0000-0000-0000-0000-000000000002"));

    [Fact]
    public void Entity_StoresTenantId()
    {
        var user = new UserAccount(TenantA, "user1", "User 1", "hash", 1L);
        Assert.Equal(TenantA.Value, user.TenantIdValue);
        Assert.Equal(TenantA, user.TenantId);
    }

    [Fact]
    public void DifferentTenants_HaveDifferentTenantIds()
    {
        var userA = new UserAccount(TenantA, "user1", "User 1", "hash", 1L);
        var userB = new UserAccount(TenantB, "user1", "User 1", "hash", 2L);
        Assert.NotEqual(userA.TenantIdValue, userB.TenantIdValue);
    }

    [Fact]
    public void TenantId_Empty_IsRecognized()
    {
        Assert.True(TenantId.Empty.IsEmpty);
        Assert.Equal(Guid.Empty, TenantId.Empty.Value);
    }

    [Fact]
    public void TenantId_NonEmpty_IsNotEmpty()
    {
        Assert.False(TenantA.IsEmpty);
        Assert.False(TenantB.IsEmpty);
    }

    [Fact]
    public void TenantId_SameValue_AreEqual()
    {
        var id1 = new TenantId(Guid.Parse("aaaa0000-0000-0000-0000-000000000001"));
        var id2 = new TenantId(Guid.Parse("aaaa0000-0000-0000-0000-000000000001"));
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AuthSession_StoresTenantId()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new AuthSession(TenantA, 1L, "Web", "Windows", "Browser", "Chrome", "127.0.0.1", "UA", now, now.AddHours(1), 100L);
        Assert.Equal(TenantA.Value, session.TenantIdValue);
    }

    [Fact]
    public void RefreshToken_StoresTenantId()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(TenantA, 1L, 100L, "tokenhash", now, now.AddHours(12), 200L);
        Assert.Equal(TenantA.Value, token.TenantIdValue);
    }
}
