using Atlas.Infrastructure.Services.SetupConsole;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// SetupConsoleAuditContext 行为测试（M10/D5）。
///
/// - Capture 写入后可读出
/// - 多次 Capture 以最后一次为准
/// - 空字符串/空白不覆盖已有值
/// - 不同实例之间互相隔离
/// </summary>
public sealed class SetupConsoleAuditContextTests
{
    [Fact]
    public void Capture_WritesIpAndUserAgent()
    {
        var ctx = new SetupConsoleAuditContext();
        ctx.Capture("10.0.0.1", "Mozilla/5.0 e2e");

        Assert.Equal("10.0.0.1", ctx.IpAddress);
        Assert.Equal("Mozilla/5.0 e2e", ctx.UserAgent);
    }

    [Fact]
    public void Capture_LastWriteWinsForBothFields()
    {
        var ctx = new SetupConsoleAuditContext();
        ctx.Capture("10.0.0.1", "ua-A");
        ctx.Capture("10.0.0.2", "ua-B");

        Assert.Equal("10.0.0.2", ctx.IpAddress);
        Assert.Equal("ua-B", ctx.UserAgent);
    }

    [Fact]
    public void Capture_NullOrWhiteSpaceDoesNotOverwriteExisting()
    {
        var ctx = new SetupConsoleAuditContext();
        ctx.Capture("10.0.0.1", "ua-A");
        ctx.Capture(null, null);
        ctx.Capture(string.Empty, "  ");

        Assert.Equal("10.0.0.1", ctx.IpAddress);
        Assert.Equal("ua-A", ctx.UserAgent);
    }

    [Fact]
    public void Capture_TwoInstancesAreIsolated()
    {
        var ctxA = new SetupConsoleAuditContext();
        var ctxB = new SetupConsoleAuditContext();

        ctxA.Capture("1.1.1.1", "ua-1");
        ctxB.Capture("2.2.2.2", "ua-2");

        Assert.Equal("1.1.1.1", ctxA.IpAddress);
        Assert.Equal("ua-1", ctxA.UserAgent);
        Assert.Equal("2.2.2.2", ctxB.IpAddress);
        Assert.Equal("ua-2", ctxB.UserAgent);
    }

    [Fact]
    public void DefaultIsNullForBothFields()
    {
        var ctx = new SetupConsoleAuditContext();
        Assert.Null(ctx.IpAddress);
        Assert.Null(ctx.UserAgent);
    }
}
