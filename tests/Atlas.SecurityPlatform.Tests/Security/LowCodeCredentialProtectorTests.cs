using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Configuration;

namespace Atlas.SecurityPlatform.Tests.Security;

/// <summary>
/// LowCodeCredentialProtector 测试（M18 收尾）。
/// 覆盖：加解密 roundtrip、幂等性、空值、Mask、向后兼容（无前缀字符串原样返回）。
/// </summary>
public sealed class LowCodeCredentialProtectorTests
{
    private static LowCodeCredentialProtector CreateProtector(string? overrideKey = null)
    {
        var dict = new Dictionary<string, string?>();
        if (overrideKey is not null)
        {
            dict["Security:LowCode:CredentialProtectorKey"] = overrideKey;
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
        return new LowCodeCredentialProtector(config);
    }

    [Fact]
    public void Encrypt_Then_Decrypt_Roundtrip()
    {
        var p = CreateProtector("test-key-1");
        var plain = "sk-very-secret-1234567890abcdef";
        var cipher = p.Encrypt(plain);
        Assert.StartsWith(LowCodeCredentialProtector.ProtectedPrefix, cipher);
        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, p.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_Empty_Returns_Empty()
    {
        var p = CreateProtector();
        Assert.Equal(string.Empty, p.Encrypt(null));
        Assert.Equal(string.Empty, p.Encrypt(string.Empty));
    }

    [Fact]
    public void Encrypt_Idempotent_For_Already_Protected_Value()
    {
        var p = CreateProtector("test-key-2");
        var cipher1 = p.Encrypt("hello");
        var cipher2 = p.Encrypt(cipher1); // 二次加密原样返回
        Assert.Equal(cipher1, cipher2);
        Assert.Equal("hello", p.Decrypt(cipher2));
    }

    [Fact]
    public void Decrypt_Returns_Same_For_NonPrefixed_Legacy_Value()
    {
        var p = CreateProtector();
        // 旧 base64 占位（无 'lcp:' 前缀）按"非密文"原样返回，保证向后兼容；
        // 后续写入会自动升级为带前缀密文。
        var legacyBase64 = "aGVsbG8=";
        Assert.Equal(legacyBase64, p.Decrypt(legacyBase64));
    }

    [Fact]
    public void Mask_Hides_Most_Of_Secret()
    {
        Assert.Equal(string.Empty, LowCodeCredentialProtector.Mask(null));
        Assert.Equal("**", LowCodeCredentialProtector.Mask("ab"));
        Assert.Equal("******", LowCodeCredentialProtector.Mask("abcdef"));
        Assert.Equal("sk-1****ef", LowCodeCredentialProtector.Mask("sk-12345abcdef"));
    }

    [Fact]
    public void Different_Keys_Produce_Different_Ciphers()
    {
        var p1 = CreateProtector("key-A");
        var p2 = CreateProtector("key-B");
        var c1 = p1.Encrypt("payload");
        var c2 = p2.Encrypt("payload");
        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void IsEncrypted_Detects_Prefix()
    {
        var p = CreateProtector();
        Assert.True(p.IsEncrypted(p.Encrypt("x")));
        Assert.False(p.IsEncrypted("x"));
        Assert.False(p.IsEncrypted(null));
    }
}
