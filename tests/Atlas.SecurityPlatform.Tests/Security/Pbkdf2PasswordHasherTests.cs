using Atlas.Infrastructure.Security;

namespace Atlas.SecurityPlatform.Tests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyString()
    {
        var hash = _hasher.HashPassword("P@ssw0rd!");
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void HashPassword_ProducesFormat_PBKDF2_Iterations_Salt_Hash()
    {
        var hash = _hasher.HashPassword("password123");
        var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
        Assert.True(int.TryParse(parts[1], out var iterations));
        Assert.Equal(100_000, iterations);
    }

    [Fact]
    public void HashPassword_SamePassword_ProducesDifferentHashes()
    {
        var hash1 = _hasher.HashPassword("password");
        var hash2 = _hasher.HashPassword("password");
        Assert.NotEqual(hash1, hash2); // different salt each time
    }

    [Fact]
    public void VerifyHashedPassword_CorrectPassword_ReturnsTrue()
    {
        var password = "MyStr0ng!Pass";
        var hash = _hasher.HashPassword(password);
        Assert.True(_hasher.VerifyHashedPassword(hash, password));
    }

    [Fact]
    public void VerifyHashedPassword_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("correct");
        Assert.False(_hasher.VerifyHashedPassword(hash, "wrong"));
    }

    [Fact]
    public void VerifyHashedPassword_EmptyHash_ReturnsFalse()
    {
        Assert.False(_hasher.VerifyHashedPassword("", "password"));
        Assert.False(_hasher.VerifyHashedPassword(null!, "password"));
    }

    [Fact]
    public void VerifyHashedPassword_EmptyPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("test");
        Assert.False(_hasher.VerifyHashedPassword(hash, ""));
        Assert.False(_hasher.VerifyHashedPassword(hash, null!));
    }

    [Fact]
    public void VerifyHashedPassword_MalformedHash_ReturnsFalse()
    {
        Assert.False(_hasher.VerifyHashedPassword("not-a-valid-hash", "password"));
        Assert.False(_hasher.VerifyHashedPassword("PBKDF2$abc$salt$hash", "password"));
    }
}
