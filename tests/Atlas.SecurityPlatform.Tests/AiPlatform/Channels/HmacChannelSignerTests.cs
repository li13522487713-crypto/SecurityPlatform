using System;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Signatures;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C3 / C4 中的签名工具：HmacChannelSigner。
/// </summary>
public sealed class HmacChannelSignerTests
{
    private const string Secret = "test-secret-1234567890abcdef";

    [Fact]
    public void Compute_ShouldBeStable_ForSameInputs()
    {
        var sig1 = HmacChannelSigner.Compute(Secret, 1700000000, "n1", "{\"x\":1}");
        var sig2 = HmacChannelSigner.Compute(Secret, 1700000000, "n1", "{\"x\":1}");
        Assert.Equal(sig1, sig2);
        Assert.Equal(64, sig1.Length); // HMAC-SHA256 hex 是 64 字符
    }

    [Fact]
    public void Compute_ShouldChange_WhenAnyInputChanges()
    {
        var baseline = HmacChannelSigner.Compute(Secret, 1700000000, "n1", "body-a");
        Assert.NotEqual(baseline, HmacChannelSigner.Compute(Secret + "x", 1700000000, "n1", "body-a"));
        Assert.NotEqual(baseline, HmacChannelSigner.Compute(Secret, 1700000001, "n1", "body-a"));
        Assert.NotEqual(baseline, HmacChannelSigner.Compute(Secret, 1700000000, "n2", "body-a"));
        Assert.NotEqual(baseline, HmacChannelSigner.Compute(Secret, 1700000000, "n1", "body-b"));
    }

    [Fact]
    public void Verify_ShouldAccept_WhenSignatureMatchesAndWithinSkew()
    {
        var ts = 1700000000L;
        var sig = HmacChannelSigner.Compute(Secret, ts, "n1", "body");
        Assert.True(HmacChannelSigner.Verify(Secret, ts, "n1", "body", sig, nowUnixSeconds: ts + 30));
    }

    [Fact]
    public void Verify_ShouldReject_WhenSignatureMismatches()
    {
        var ts = 1700000000L;
        var goodSig = HmacChannelSigner.Compute(Secret, ts, "n1", "body");
        Assert.False(HmacChannelSigner.Verify(Secret, ts, "n1", "body-tampered", goodSig, nowUnixSeconds: ts));
        Assert.False(HmacChannelSigner.Verify(Secret + "x", ts, "n1", "body", goodSig, nowUnixSeconds: ts));
    }

    [Fact]
    public void Verify_ShouldReject_WhenTimestampOutsideSkew()
    {
        var ts = 1700000000L;
        var sig = HmacChannelSigner.Compute(Secret, ts, "n1", "body");
        // 默认 ±300 秒；偏 600 秒应拒绝
        Assert.False(HmacChannelSigner.Verify(Secret, ts, "n1", "body", sig, nowUnixSeconds: ts + 600));
    }

    [Fact]
    public void Verify_ShouldReject_WhenSignatureEmpty()
    {
        Assert.False(HmacChannelSigner.Verify(Secret, 1700000000, "n1", "body", string.Empty));
    }

    [Fact]
    public void GenerateSecret_ShouldReturnUrlSafeString_WithMinLength()
    {
        var s = HmacChannelSigner.GenerateSecret(32);
        Assert.False(string.IsNullOrWhiteSpace(s));
        Assert.DoesNotContain('+', s);
        Assert.DoesNotContain('/', s);
        Assert.DoesNotContain('=', s);
        // 32 字节 base64 长度大约为 43
        Assert.True(s.Length >= 32);
    }
}
