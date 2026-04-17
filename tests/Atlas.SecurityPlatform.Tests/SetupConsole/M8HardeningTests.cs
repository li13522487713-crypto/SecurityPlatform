using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Atlas.Infrastructure.Services.SetupConsole;
using Microsoft.Extensions.Configuration;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// M8 生产硬化（A2/A3 + B 系列）的领域 / 工具单测。
///
/// 覆盖：
///  - SetupConsoleToken 实体状态机（颁发、续期、撤销、过期判定）
///  - SystemSetupState.SetBootstrapPasswordHash 字段持久化
///  - SetupSeedBundleLog 实体行为
///  - MigrationSecretProtector 加密 / 解密 / 幂等
///  - SetupConsoleService 的 ParseTenantId 私有逻辑（通过 RunBootstrapUserAsync 间接验证；本测专注静态部分）
/// </summary>
public sealed class M8HardeningTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset Now = new(2026, 4, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetupConsoleToken_IsActiveByDefault()
    {
        var token = new SetupConsoleToken(TestTenant, 1, "hash-abc", "system,workspace,migration", Now, Now.AddMinutes(30));
        Assert.True(token.IsActive(Now.AddMinutes(15)));
    }

    [Fact]
    public void SetupConsoleToken_ExpiresAtBoundary()
    {
        var token = new SetupConsoleToken(TestTenant, 1, "hash-abc", "system", Now, Now.AddMinutes(30));
        Assert.False(token.IsActive(Now.AddMinutes(30))); // ExpiresAt 是开区间
        Assert.False(token.IsActive(Now.AddMinutes(31)));
    }

    [Fact]
    public void SetupConsoleToken_RenewExtendsExpiry()
    {
        var token = new SetupConsoleToken(TestTenant, 1, "hash-abc", "system", Now, Now.AddMinutes(30));
        var renewedAt = Now.AddMinutes(20);
        var newExpiresAt = renewedAt.AddMinutes(30);
        token.Renew(renewedAt, newExpiresAt);

        Assert.Equal(renewedAt, token.IssuedAt);
        Assert.Equal(newExpiresAt, token.ExpiresAt);
        Assert.True(token.IsActive(renewedAt.AddMinutes(15)));
    }

    [Fact]
    public void SetupConsoleToken_RevokeIsImmediate()
    {
        var token = new SetupConsoleToken(TestTenant, 1, "hash-abc", "system", Now, Now.AddMinutes(30));
        token.Revoke(Now.AddMinutes(5));
        Assert.False(token.IsActive(Now.AddMinutes(10)));
    }

    [Fact]
    public void SystemSetupState_SetBootstrapPasswordHash_PersistsAndUpdatesTimestamp()
    {
        var state = new SystemSetupState(TestTenant, 1, "v1", Now);
        Assert.Null(state.BootstrapPasswordHash);

        var later = Now.AddMinutes(5);
        state.SetBootstrapPasswordHash("PBKDF2$1000$abc$def", later);

        Assert.Equal("PBKDF2$1000$abc$def", state.BootstrapPasswordHash);
        Assert.Equal(later, state.LastUpdatedAt);
    }

    [Fact]
    public void SetupSeedBundleLog_ConstructorAssignsAllFields()
    {
        var log = new SetupSeedBundleLog(TestTenant, 1, "roles-permissions", "v1", Now);
        Assert.Equal("roles-permissions", log.Bundle);
        Assert.Equal("v1", log.Version);
        Assert.Equal(Now, log.AppliedAt);
    }

    [Fact]
    public void MigrationSecretProtector_RoundTripsPlainText()
    {
        var protector = BuildProtector("dev-master-key-for-tests");
        var original = "Server=localhost;Port=3306;Database=atlas;Uid=root;Pwd=secret123;";
        var encrypted = protector.Encrypt(original);

        Assert.NotEqual(original, encrypted);
        Assert.StartsWith("protected:", encrypted);
        Assert.Equal(original, protector.Decrypt(encrypted));
    }

    [Fact]
    public void MigrationSecretProtector_EncryptIsIdempotentForAlreadyProtected()
    {
        var protector = BuildProtector("dev-master-key-for-tests");
        var original = "Data Source=atlas.db";
        var firstEncrypt = protector.Encrypt(original);
        var secondEncrypt = protector.Encrypt(firstEncrypt);
        Assert.Equal(firstEncrypt, secondEncrypt);
    }

    [Fact]
    public void MigrationSecretProtector_DecryptUnprotectedReturnsAsIs()
    {
        var protector = BuildProtector("dev-master-key-for-tests");
        const string raw = "Data Source=atlas.db";
        Assert.Equal(raw, protector.Decrypt(raw));
    }

    [Fact]
    public void MigrationSecretProtector_EmptyInputReturnsEmpty()
    {
        var protector = BuildProtector("dev-master-key-for-tests");
        Assert.Equal(string.Empty, protector.Encrypt(null));
        Assert.Equal(string.Empty, protector.Encrypt(string.Empty));
        Assert.Equal(string.Empty, protector.Decrypt(null));
        Assert.Equal(string.Empty, protector.Decrypt(string.Empty));
    }

    [Fact]
    public void MigrationSecretProtector_DifferentMasterKeysProduceIncompatibleCiphertext()
    {
        var protectorA = BuildProtector("master-key-A-1234567890-abcdef");
        var protectorB = BuildProtector("master-key-B-1234567890-abcdef");
        var encryptedA = protectorA.Encrypt("Data Source=atlas.db");

        // protectorB 解密 protectorA 的密文应抛 InvalidOperationException
        Assert.Throws<InvalidOperationException>(() => protectorB.Decrypt(encryptedA));
    }

    [Fact]
    public void MigrationSecretProtector_NullConfigurationThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new MigrationSecretProtector(null!));
    }

    private static MigrationSecretProtector BuildProtector(string masterKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SetupConsole:MigrationProtectorKey"] = masterKey
            })
            .Build();
        return new MigrationSecretProtector(configuration);
    }
}
