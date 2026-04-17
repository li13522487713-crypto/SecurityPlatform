using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// SetupConsoleToken 过期与续期纯领域测试（M10/D3）。
///
/// 真实持久化测试已被 OrmMigrationIntegrationTests 间接覆盖（共享 SetupRecoveryKeyService 实例化路径）；
/// 此处专注边界场景：过期判定、撤销后判定、续期前后判定。
/// </summary>
public sealed class SetupRecoveryKeyServiceTokenExpiryTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public void Token_ExpiresAtBoundaryReturnsFalse()
    {
        var issuedAt = new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero);
        var expiresAt = issuedAt.AddMinutes(30);
        var token = new SetupConsoleToken(TestTenant, 1, "hash-x", "system", issuedAt, expiresAt);

        Assert.True(token.IsActive(issuedAt));
        Assert.True(token.IsActive(issuedAt.AddMinutes(29)));
        Assert.False(token.IsActive(expiresAt)); // 开区间
        Assert.False(token.IsActive(expiresAt.AddMinutes(1)));
    }

    [Fact]
    public void Token_RevokedTakesPrecedenceOverExpiry()
    {
        var issuedAt = new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero);
        var token = new SetupConsoleToken(TestTenant, 1, "hash-x", "system", issuedAt, issuedAt.AddMinutes(30));

        token.Revoke(issuedAt.AddMinutes(5));
        Assert.False(token.IsActive(issuedAt.AddMinutes(10)));
        Assert.False(token.IsActive(issuedAt.AddMinutes(40))); // 即使过期也无意义，已 revoke
    }

    [Fact]
    public void Token_RenewExtendsActiveWindow()
    {
        var issuedAt = new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero);
        var token = new SetupConsoleToken(TestTenant, 1, "hash-x", "system", issuedAt, issuedAt.AddMinutes(30));

        // 滑动续期到 +60 分钟
        var renewedAt = issuedAt.AddMinutes(20);
        token.Renew(renewedAt, renewedAt.AddMinutes(30));

        Assert.True(token.IsActive(renewedAt.AddMinutes(29)));
        Assert.False(token.IsActive(renewedAt.AddMinutes(31)));
    }

    [Fact]
    public void Token_RenewAfterExpiryStillValid()
    {
        var issuedAt = new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero);
        var token = new SetupConsoleToken(TestTenant, 1, "hash-x", "system", issuedAt, issuedAt.AddMinutes(30));

        // 过期后续期（业务上不允许，但实体本身不阻断；调用方在 RefreshAsync 内拒绝）
        var renewedAt = issuedAt.AddMinutes(60);
        token.Renew(renewedAt, renewedAt.AddMinutes(30));

        Assert.True(token.IsActive(renewedAt.AddMinutes(15)));
    }

    [Fact]
    public void Token_RevokedAtIsAlwaysAfterIssuedAt()
    {
        var issuedAt = new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero);
        var token = new SetupConsoleToken(TestTenant, 1, "hash-x", "system", issuedAt, issuedAt.AddMinutes(30));

        Assert.Null(token.RevokedAt);
        token.Revoke(issuedAt.AddMinutes(1));
        Assert.NotNull(token.RevokedAt);
        Assert.True(token.RevokedAt > issuedAt);
    }
}
