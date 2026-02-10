using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.SecurityPlatform.Tests.Domain;

public sealed class AuthSessionTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public void Constructor_InitializesAllFields()
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddHours(1);
        var session = new AuthSession(TestTenant, 42L, "Web", "Windows", "Browser", "Chrome",
            "192.168.1.1", "Mozilla/5.0", now, expiresAt, 1L);

        Assert.Equal(1L, session.Id);
        Assert.Equal(42L, session.UserId);
        Assert.Equal("Web", session.ClientType);
        Assert.Equal("Windows", session.ClientPlatform);
        Assert.Equal("Browser", session.ClientChannel);
        Assert.Equal("Chrome", session.ClientAgent);
        Assert.Equal("192.168.1.1", session.IpAddress);
        Assert.Equal(now, session.CreatedAt);
        Assert.Equal(expiresAt, session.ExpiresAt);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public void MarkSeen_UpdatesLastSeenAt()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new AuthSession(TestTenant, 1L, "Web", "Win", "B", "C", null, null, now, now.AddHours(1), 1L);
        var later = now.AddMinutes(5);
        session.MarkSeen(later);
        Assert.Equal(later, session.LastSeenAt);
    }

    [Fact]
    public void Revoke_SetsRevokedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new AuthSession(TestTenant, 1L, "Web", "Win", "B", "C", null, null, now, now.AddHours(1), 1L);
        Assert.Null(session.RevokedAt);

        var revokeTime = now.AddMinutes(10);
        session.Revoke(revokeTime);
        Assert.NotNull(session.RevokedAt);
        Assert.Equal(revokeTime, session.RevokedAt);
    }
}
