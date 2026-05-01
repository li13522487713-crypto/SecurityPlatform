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
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "none",
            ActionExecutorCapabilitiesHash: "executors-v1");
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

    [Fact]
    public async Task Invalidate_Does_Not_Remove_Similar_ResourceId_Or_Version()
    {
        IMicroflowExecutionPlanCache cache = new MicroflowExecutionPlanCache();
        var targetKey = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "none",
            ActionExecutorCapabilitiesHash: "executors-v1");
        var siblingKey = targetKey with
        {
            ResourceId = "mf-runtime-extra",
            SchemaId = "schema-runtime-extra",
            Version = "v10"
        };
        var targetCreated = 0;
        var siblingCreated = 0;

        await cache.GetOrCreateAsync(
            targetKey,
            _ =>
            {
                targetCreated++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "target-v1" });
            },
            CancellationToken.None);
        await cache.GetOrCreateAsync(
            siblingKey,
            _ =>
            {
                siblingCreated++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "sibling-v10" });
            },
            CancellationToken.None);

        cache.Invalidate("mf-runtime", "v1");

        var target = await cache.GetOrCreateAsync(
            targetKey,
            _ =>
            {
                targetCreated++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "target-rebuilt" });
            },
            CancellationToken.None);
        var sibling = await cache.GetOrCreateAsync(
            siblingKey,
            _ =>
            {
                siblingCreated++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "sibling-rebuilt" });
            },
            CancellationToken.None);

        Assert.Equal(2, targetCreated);
        Assert.Equal(1, siblingCreated);
        Assert.Equal("target-rebuilt", target.Id);
        Assert.Equal("sibling-v10", sibling.Id);
    }

    [Fact]
    public async Task GetOrCreateAsync_Treats_SchemaHash_As_Cache_Boundary()
    {
        IMicroflowExecutionPlanCache cache = new MicroflowExecutionPlanCache();
        var firstKey = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "none",
            ActionExecutorCapabilitiesHash: "executors-v1");
        var secondKey = firstKey with { SchemaHash = "schema-hash-v2" };
        var created = 0;

        var first = await cache.GetOrCreateAsync(
            firstKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-schema-v1" });
            },
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            secondKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-schema-v2" });
            },
            CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal("plan-schema-v1", first.Id);
        Assert.Equal("plan-schema-v2", second.Id);
    }

    [Fact]
    public async Task GetOrCreateAsync_Treats_MetadataVersion_As_Cache_Boundary()
    {
        IMicroflowExecutionPlanCache cache = new MicroflowExecutionPlanCache();
        var firstKey = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "none",
            ActionExecutorCapabilitiesHash: "executors-v1");
        var secondKey = firstKey with { MetadataVersion = "meta-v2" };
        var created = 0;

        var first = await cache.GetOrCreateAsync(
            firstKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-meta-v1" });
            },
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            secondKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-meta-v2" });
            },
            CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal("plan-meta-v1", first.Id);
        Assert.Equal("plan-meta-v2", second.Id);
    }

    [Fact]
    public async Task GetOrCreateAsync_Treats_ConnectorCapabilitiesHash_As_Cache_Boundary()
    {
        IMicroflowExecutionPlanCache cache = new MicroflowExecutionPlanCache();
        var firstKey = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "connectors-v1",
            ActionExecutorCapabilitiesHash: "executors-v1");
        var secondKey = firstKey with { ConnectorCapabilitiesHash = "connectors-v2" };
        var created = 0;

        var first = await cache.GetOrCreateAsync(
            firstKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-connectors-v1" });
            },
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            secondKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-connectors-v2" });
            },
            CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal("plan-connectors-v1", first.Id);
        Assert.Equal("plan-connectors-v2", second.Id);
    }

    [Fact]
    public async Task GetOrCreateAsync_Treats_Mode_As_Cache_Boundary()
    {
        IMicroflowExecutionPlanCache cache = new MicroflowExecutionPlanCache();
        var firstKey = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "connectors-v1",
            ActionExecutorCapabilitiesHash: "executors-v1");
        var secondKey = firstKey with { Mode = MicroflowExecutionPlanMode.PublishedRun };
        var created = 0;

        var first = await cache.GetOrCreateAsync(
            firstKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-test-run" });
            },
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            secondKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-published-run" });
            },
            CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal("plan-test-run", first.Id);
        Assert.Equal("plan-published-run", second.Id);
    }

    [Fact]
    public async Task GetOrCreateAsync_Treats_ActionExecutorCapabilitiesHash_As_Cache_Boundary()
    {
        IMicroflowExecutionPlanCache cache = new MicroflowExecutionPlanCache();
        var firstKey = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "connectors-v1",
            ActionExecutorCapabilitiesHash: "executors-v1");
        var secondKey = firstKey with { ActionExecutorCapabilitiesHash = "executors-v2" };
        var created = 0;

        var first = await cache.GetOrCreateAsync(
            firstKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-executors-v1" });
            },
            CancellationToken.None);
        var second = await cache.GetOrCreateAsync(
            secondKey,
            _ =>
            {
                created++;
                return Task.FromResult(new MicroflowExecutionPlan { Id = "plan-executors-v2" });
            },
            CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal("plan-executors-v1", first.Id);
        Assert.Equal("plan-executors-v2", second.Id);
    }

    [Fact]
    public void StableKey_Includes_ActionExecutorCapabilitiesHash()
    {
        var firstKey = new MicroflowExecutionPlanCacheKey(
            ResourceId: "mf-runtime",
            SchemaId: "schema-runtime",
            SchemaHash: "schema-hash-v1",
            Version: "v1",
            SchemaVersion: "1.0.0",
            Mode: MicroflowExecutionPlanMode.TestRun,
            MetadataVersion: "meta-v1",
            ConnectorCapabilitiesHash: "connectors-v1",
            ActionExecutorCapabilitiesHash: "executors-v1");
        var secondKey = firstKey with { ActionExecutorCapabilitiesHash = "executors-v2" };

        Assert.NotEqual(firstKey.StableKey, secondKey.StableKey);
        Assert.EndsWith("|executors-v1", firstKey.StableKey, StringComparison.Ordinal);
        Assert.EndsWith("|executors-v2", secondKey.StableKey, StringComparison.Ordinal);
    }
}
