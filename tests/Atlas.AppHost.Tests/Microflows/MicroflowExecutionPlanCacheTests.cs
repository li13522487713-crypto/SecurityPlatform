using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowExecutionPlanCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_Reuses_Same_Key_And_Invalidate_Removes_Cached_Plan()
    {
        IMicroflowExecutionPlanCache cache = new MicroflowExecutionPlanCache();
        var key = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "none");
        var created = 0;

        var first = await cache.GetOrCreateAsync(
            key,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan
                {
                    Id = "plan-v1",
                    ResourceId = "mf-runtime",
                    SchemaId = "schema-runtime",
                    Version = "v1"
                });
            },
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            key,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan
                {
                    Id = "plan-v1-second"
                });
            },
            CancellationToken.None);

        Assert.Equal(1, created);
        Assert.Equal("plan-v1", first.Id);
        Assert.Equal("plan-v1", second.Id);

        cache.Invalidate("mf-runtime", "v1");

        var afterInvalidate = await cache.GetOrCreateAsync(
            key,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan
                {
                    Id = "plan-v1-rebuilt",
                    ResourceId = "mf-runtime",
                    SchemaId = "schema-runtime",
                    Version = "v1"
                });
            },
            CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal("plan-v1-rebuilt", afterInvalidate.Id);
    }
}
