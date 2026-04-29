using Atlas.Application.Microflows.Runtime.Actions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowActionDescriptorNormalizerTests
{
    [Theory]
    [InlineData("webserviceCall", "webServiceCall")]
    [InlineData("webService", "webServiceCall")]
    [InlineData("callExternal", "callExternalAction")]
    [InlineData("externalCall", "callExternalAction")]
    [InlineData("deleteExternal", "deleteExternalObject")]
    [InlineData("sendExternal", "sendExternalObject")]
    [InlineData("rollbackObject", "rollback")]
    [InlineData("castObject", "cast")]
    [InlineData("listUnion", "listOperation")]
    [InlineData("listIntersect", "listOperation")]
    [InlineData("listSubtract", "listOperation")]
    [InlineData("aggregate", "aggregateList")]
    [InlineData("filter", "filterList")]
    [InlineData("sort", "sortList")]
    public void NormalizeActionKind_ReturnsCanonicalKind(string legacy, string canonical)
    {
        var normalizer = new MicroflowActionDescriptorNormalizer();

        var result = normalizer.Normalize(legacy, "$.action.kind");

        Assert.True(result.Changed);
        Assert.Equal(legacy, result.Original);
        Assert.Equal(canonical, result.Canonical);
        Assert.Contains(result.Changes, change => change.Path == "$.action.kind" && change.Original == legacy && change.Canonical == canonical);
    }

    [Fact]
    public void NormalizeActionKind_LeavesCanonicalUnchanged()
    {
        var normalizer = new MicroflowActionDescriptorNormalizer();

        var result = normalizer.Normalize("webServiceCall", "$.action.kind");

        Assert.False(result.Changed);
        Assert.Equal("webServiceCall", result.Canonical);
        Assert.Empty(result.Changes);
    }
}
