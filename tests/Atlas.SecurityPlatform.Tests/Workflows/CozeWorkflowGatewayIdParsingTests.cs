using Atlas.Presentation.Shared.Controllers.Ai;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Workflows;

public sealed class CozeWorkflowGatewayIdParsingTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("9223372036854775807", true)]
    [InlineData("9007199254740993", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("9223372036854775808", false)]
    [InlineData("not-a-number", false)]
    [InlineData("", false)]
    public void TryParseWorkflowId_ShouldOnlyAcceptPositiveInt64Strings(string raw, bool expected)
    {
        var actual = CozeCompatGatewaySupport.TryParsePositiveLongId(raw, out var parsed);

        Assert.Equal(expected, actual);
        if (expected)
        {
            Assert.True(parsed > 0);
        }
    }
}
