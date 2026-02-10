using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.SecurityPlatform.Tests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(12);
        var token = new RefreshToken(TestTenant, 42L, 100L, "hash", now, expires, 1L);

        Assert.Equal(1L, token.Id);
        Assert.Equal(42L, token.UserId);
        Assert.Equal(100L, token.SessionId);
        Assert.Equal("hash", token.TokenHash);
        Assert.Equal(now, token.IssuedAt);
        Assert.Equal(expires, token.ExpiresAt);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.ReplacedById);
    }

    [Fact]
    public void Revoke_SetsRevokedAtAndReplacedById()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(TestTenant, 1L, 100L, "hash", now, now.AddHours(12), 1L);

        var revokeTime = now.AddMinutes(30);
        token.Revoke(revokeTime, 2L);

        Assert.Equal(revokeTime, token.RevokedAt);
        Assert.Equal(2L, token.ReplacedById);
    }

    [Fact]
    public void Revoke_WithoutReplacement_SetsNullReplacedById()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(TestTenant, 1L, 100L, "hash", now, now.AddHours(12), 1L);

        token.Revoke(now, null);

        Assert.NotNull(token.RevokedAt);
        Assert.Null(token.ReplacedById);
    }
}
