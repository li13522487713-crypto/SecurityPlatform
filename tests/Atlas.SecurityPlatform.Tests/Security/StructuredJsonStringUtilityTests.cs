using Atlas.Core.Utilities;

namespace Atlas.SecurityPlatform.Tests.Security;

public sealed class StructuredJsonStringUtilityTests
{
    [Fact]
    public void RawJsonString_ReturnsOriginalJson()
    {
        const string input = "{\"kind\":\"workflow-task\",\"trace\":{\"status\":\"Completed\"}}";

        var result = StructuredJsonStringUtility.TryNormalizeJsonString(input, out var normalizedJson);

        Assert.True(result);
        Assert.Equal(input, normalizedJson);
    }

    [Fact]
    public void HtmlEncodedJsonString_ReturnsDecodedJson()
    {
        const string input = "{&quot;kind&quot;:&quot;workflow-task&quot;,&quot;trace&quot;:{&quot;status&quot;:&quot;Completed&quot;}}";

        var result = StructuredJsonStringUtility.TryNormalizeJsonString(input, out var normalizedJson);

        Assert.True(result);
        Assert.Equal("{\"kind\":\"workflow-task\",\"trace\":{\"status\":\"Completed\"}}", normalizedJson);
    }

    [Fact]
    public void NonJsonString_ReturnsFalse()
    {
        const string input = "plain-text";

        var result = StructuredJsonStringUtility.TryNormalizeJsonString(input, out var normalizedJson);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedJson);
    }
}
