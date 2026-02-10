using Atlas.Application.Options;
using Atlas.Application.Security;

namespace Atlas.SecurityPlatform.Tests.Security;

public sealed class PasswordPolicyTests
{
    private readonly PasswordPolicyOptions _defaultPolicy = new()
    {
        MinLength = 8,
        RequireUppercase = true,
        RequireLowercase = true,
        RequireDigit = true,
        RequireNonAlphanumeric = true
    };

    [Fact]
    public void CompliantPassword_ReturnsTrue()
    {
        Assert.True(PasswordPolicy.IsCompliant("P@ssw0rd!", _defaultPolicy, out _));
    }

    [Fact]
    public void EmptyPassword_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant("", _defaultPolicy, out var msg));
        Assert.NotEmpty(msg);
    }

    [Fact]
    public void NullPassword_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant(null!, _defaultPolicy, out _));
    }

    [Fact]
    public void WhitespacePassword_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant("   ", _defaultPolicy, out _));
    }

    [Fact]
    public void TooShortPassword_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant("Pa1!", _defaultPolicy, out var msg));
        Assert.Contains("8", msg);
    }

    [Fact]
    public void NoUppercase_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant("p@ssw0rd!", _defaultPolicy, out var msg));
        Assert.Contains("大写", msg);
    }

    [Fact]
    public void NoLowercase_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant("P@SSW0RD!", _defaultPolicy, out var msg));
        Assert.Contains("小写", msg);
    }

    [Fact]
    public void NoDigit_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant("P@ssword!", _defaultPolicy, out var msg));
        Assert.Contains("数字", msg);
    }

    [Fact]
    public void NoSpecialChar_ReturnsFalse()
    {
        Assert.False(PasswordPolicy.IsCompliant("Passw0rd1", _defaultPolicy, out var msg));
        Assert.Contains("特殊", msg);
    }

    [Fact]
    public void RelaxedPolicy_DigitsOnlyOk()
    {
        var relaxed = new PasswordPolicyOptions
        {
            MinLength = 4,
            RequireUppercase = false,
            RequireLowercase = false,
            RequireDigit = true,
            RequireNonAlphanumeric = false
        };
        Assert.True(PasswordPolicy.IsCompliant("1234", relaxed, out _));
    }

    [Fact]
    public void ExactMinLength_IsCompliant()
    {
        Assert.True(PasswordPolicy.IsCompliant("Ab1!cdef", _defaultPolicy, out _));
    }
}
