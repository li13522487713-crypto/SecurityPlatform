using Atlas.Infrastructure.Security;

namespace Atlas.SecurityPlatform.Tests.Security;

public sealed class TotpServiceTests
{
    private readonly TotpService _totp = new();

    [Fact]
    public void GenerateSecretKey_ReturnsNonEmpty_Base32String()
    {
        var key = _totp.GenerateSecretKey();
        Assert.NotNull(key);
        Assert.NotEmpty(key);
        // Base32 only contains A-Z and 2-7
        Assert.All(key, c => Assert.True(
            (c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7'),
            $"Invalid Base32 char: {c}"));
    }

    [Fact]
    public void GenerateSecretKey_ProducesDifferentKeys()
    {
        var key1 = _totp.GenerateSecretKey();
        var key2 = _totp.GenerateSecretKey();
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCode_Returns6DigitCode()
    {
        var key = _totp.GenerateSecretKey();
        var code = _totp.GenerateCode(key);
        Assert.Equal(6, code.Length);
        Assert.True(int.TryParse(code, out _));
    }

    [Fact]
    public void ValidateCode_CurrentCode_ReturnsTrue()
    {
        var key = _totp.GenerateSecretKey();
        var now = DateTimeOffset.UtcNow;
        var code = _totp.GenerateCode(key, now);
        Assert.True(_totp.ValidateCode(key, code, now));
    }

    [Fact]
    public void ValidateCode_WrongCode_ReturnsFalse()
    {
        var key = _totp.GenerateSecretKey();
        Assert.False(_totp.ValidateCode(key, "000000"));
    }

    [Fact]
    public void ValidateCode_EmptyCode_ReturnsFalse()
    {
        var key = _totp.GenerateSecretKey();
        Assert.False(_totp.ValidateCode(key, ""));
        Assert.False(_totp.ValidateCode(key, null!));
    }

    [Fact]
    public void ValidateCode_WithinToleranceWindow_ReturnsTrue()
    {
        var key = _totp.GenerateSecretKey();
        var now = DateTimeOffset.UtcNow;
        // Generate code for 30 seconds ago (1 step back)
        var pastCode = _totp.GenerateCode(key, now.AddSeconds(-30));
        Assert.True(_totp.ValidateCode(key, pastCode, now));
    }

    [Fact]
    public void ValidateCode_OutsideToleranceWindow_ReturnsFalse()
    {
        var key = _totp.GenerateSecretKey();
        var now = DateTimeOffset.UtcNow;
        // Generate code for 2 minutes ago (far outside window)
        var oldCode = _totp.GenerateCode(key, now.AddMinutes(-2));
        Assert.False(_totp.ValidateCode(key, oldCode, now));
    }

    [Fact]
    public void GenerateProvisioningUri_ReturnsValidOtpauthUri()
    {
        var key = _totp.GenerateSecretKey();
        var uri = _totp.GenerateProvisioningUri(key, "admin@example.com", "Atlas");
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains($"secret={key}", uri);
        Assert.Contains("issuer=Atlas", uri);
    }

    [Fact]
    public void SameKey_SameTimestamp_ProducesSameCode()
    {
        var key = _totp.GenerateSecretKey();
        var fixedTime = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var code1 = _totp.GenerateCode(key, fixedTime);
        var code2 = _totp.GenerateCode(key, fixedTime);
        Assert.Equal(code1, code2);
    }

    [Fact]
    public void DifferentKeys_SameTimestamp_ProducesDifferentCodes()
    {
        var key1 = _totp.GenerateSecretKey();
        var key2 = _totp.GenerateSecretKey();
        var fixedTime = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var code1 = _totp.GenerateCode(key1, fixedTime);
        var code2 = _totp.GenerateCode(key2, fixedTime);
        Assert.NotEqual(code1, code2);
    }
}
