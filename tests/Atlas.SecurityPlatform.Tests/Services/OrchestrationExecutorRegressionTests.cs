using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Services.AiPlatform;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class OrchestrationExecutorRegressionTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task ExecuteAsync_ShouldResumeFromCheckpoint_WhenIdempotencyProvided()
    {
        var compiler = new FakeCompiler(new CompiledOrchestrationPlan(
            9527,
            "plan.resume",
            "resume plan",
            "manual",
            1,
            [
                new CompiledOrchestrationNode("n1", "transform", []),
                new CompiledOrchestrationNode("n2", "transform", ["n1"])
            ],
            "{}",
            "{}",
            "hash",
            DateTimeOffset.UtcNow));
        var cache = new InMemoryHybridCache();
        var checkpoints = new InMemoryCheckpointService(initialProcessedCount: 1);
        var executor = new OrchestrationExecutor(
            compiler,
            cache,
            checkpoints,
            new OrchestrationCompensationService());

        var result = await executor.ExecuteAsync(
            Tenant,
            new OrchestrationExecutionRequest(
                PlanId: 9527,
                IdempotencyKey: "resume-key",
                InputJson: "{\"payload\":true}",
                MaxRetries: 0,
                TimeoutSeconds: 5));

        Assert.Equal("Succeeded", result.Status);
        Assert.True(result.ResumeApplied);
        Assert.Single(result.TraceSteps);
        Assert.Equal("n2", result.TraceSteps[0].NodeId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCompensateSucceededSteps_WhenSubsequentNodeFails()
    {
        var compiler = new FakeCompiler(new CompiledOrchestrationPlan(
            9528,
            "plan.compensate",
            "compensate plan",
            "manual",
            1,
            [
                new CompiledOrchestrationNode("n1", "transform", []),
                new CompiledOrchestrationNode("n2", "fail", ["n1"])
            ],
            "{}",
            "{}",
            "hash",
            DateTimeOffset.UtcNow));
        var executor = new OrchestrationExecutor(
            compiler,
            new InMemoryHybridCache(),
            new InMemoryCheckpointService(initialProcessedCount: 0),
            new OrchestrationCompensationService());

        var result = await executor.ExecuteAsync(
            Tenant,
            new OrchestrationExecutionRequest(
                PlanId: 9528,
                IdempotencyKey: "fail-key",
                InputJson: "{}",
                MaxRetries: 0,
                TimeoutSeconds: 5));

        Assert.Equal("Failed", result.Status);
        Assert.True(result.CompensationApplied);
        Assert.Contains(result.TraceSteps, step => step.Status == "Compensated" && step.NodeId == "n1");
    }

    private sealed class FakeCompiler : IOrchestrationCompiler
    {
        private readonly CompiledOrchestrationPlan _plan;

        public FakeCompiler(CompiledOrchestrationPlan plan)
        {
            _plan = plan;
        }

        public Task<CompiledOrchestrationPlan?> CompileByKeyAsync(
            TenantId tenantId,
            long appInstanceId,
            string planKey,
            CancellationToken cancellationToken = default)
        {
            _ = tenantId;
            _ = appInstanceId;
            _ = cancellationToken;
            return Task.FromResult<CompiledOrchestrationPlan?>(string.Equals(_plan.PlanKey, planKey, StringComparison.OrdinalIgnoreCase) ? _plan : null);
        }

        public Task<CompiledOrchestrationPlan?> CompileByIdAsync(TenantId tenantId, long planId, CancellationToken cancellationToken = default)
        {
            _ = tenantId;
            _ = cancellationToken;
            return Task.FromResult<CompiledOrchestrationPlan?>(_plan.PlanId == planId ? _plan : null);
        }
    }

    private sealed class InMemoryCheckpointService : ICheckpointService
    {
        private CheckpointInfo? _checkpoint;

        public InMemoryCheckpointService(long initialProcessedCount)
        {
            _checkpoint = initialProcessedCount <= 0
                ? null
                : new CheckpointInfo
                {
                    CheckpointId = 1,
                    CheckpointKey = "checkpoint",
                    ProcessedUpTo = "n1",
                    ProcessedCount = initialProcessedCount,
                    CreatedAt = DateTime.UtcNow
                };
        }

        public Task<long> SaveAsync(long shardExecutionId, string checkpointKey, string processedUpTo, long processedCount, TenantId tenantId, CancellationToken cancellationToken)
        {
            _ = shardExecutionId;
            _ = tenantId;
            _ = cancellationToken;
            _checkpoint = new CheckpointInfo
            {
                CheckpointId = processedCount,
                CheckpointKey = checkpointKey,
                ProcessedUpTo = processedUpTo,
                ProcessedCount = processedCount,
                CreatedAt = DateTime.UtcNow
            };
            return Task.FromResult(processedCount);
        }

        public Task<CheckpointInfo?> GetLatestAsync(long shardExecutionId, CancellationToken cancellationToken)
        {
            _ = shardExecutionId;
            _ = cancellationToken;
            return Task.FromResult(_checkpoint);
        }
    }

    private sealed class InMemoryHybridCache : IAtlasHybridCache
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

        public ValueTask<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T?>> valueFactory, TimeSpan expiration, IEnumerable<string>? tags = null, bool localOnly = false, CancellationToken cancellationToken = default)
        {
            _ = expiration;
            _ = tags;
            _ = localOnly;
            if (_store.TryGetValue(key, out var value))
            {
                return ValueTask.FromResult((T?)value);
            }

            return CreateAndSetAsync();

            async ValueTask<T?> CreateAndSetAsync()
            {
                var created = await valueFactory(cancellationToken);
                _store[key] = created;
                return created;
            }
        }

        public ValueTask SetAsync<T>(string key, T? value, TimeSpan expiration, IEnumerable<string>? tags = null, bool localOnly = false, CancellationToken cancellationToken = default)
        {
            _ = expiration;
            _ = tags;
            _ = localOnly;
            _ = cancellationToken;
            _store[key] = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask<AtlasCacheLookupResult<T>> TryGetAsync<T>(string key, bool localOnly = false, CancellationToken cancellationToken = default)
        {
            _ = localOnly;
            _ = cancellationToken;
            if (_store.TryGetValue(key, out var value))
            {
                return ValueTask.FromResult(AtlasCacheLookupResult<T>.Hit((T?)value));
            }

            return ValueTask.FromResult(AtlasCacheLookupResult<T>.Miss);
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _store.Remove(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            _ = tag;
            _ = cancellationToken;
            _store.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            _ = tags;
            _ = cancellationToken;
            _store.Clear();
            return ValueTask.CompletedTask;
        }
    }
}
