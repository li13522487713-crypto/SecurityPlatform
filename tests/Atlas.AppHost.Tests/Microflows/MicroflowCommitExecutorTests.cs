using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Objects;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowCommitExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(MicroflowRuntimeExecutionMode.TestRun, true)]
    [InlineData(MicroflowRuntimeExecutionMode.PublishedRun, false)]
    public async Task Commit_uses_dry_run_only_for_test_run_mode(string mode, bool expectedDryRun)
    {
        var store = new RecordingObjectStore();
        var executor = new CommitObjectActionExecutor(store);
        var context = Context(new
        {
            objectId = "order-1",
            entityType = "Sales.Order",
            value = new { id = "order-1", status = "Committed" }
        }, mode);

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.NotNull(store.LastCommit);
        Assert.Equal(expectedDryRun, store.LastCommit!.DryRun);
        Assert.Equal("Sales.Order", store.LastCommit.EntityType);
        Assert.Equal("order-1", store.LastCommit.ObjectId);
    }

    private static MicroflowActionExecutionContext Context(object config, string mode)
    {
        var executionPlan = new MicroflowExecutionPlan
        {
            Id = "commit-test",
            SchemaId = "commit-test",
            ResourceId = "mf-commit"
        };
        var runtime = RuntimeExecutionContext.Create(
            $"run-commit-{mode}",
            executionPlan,
            mode,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = executionPlan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "commit-node", ActionId = "commit-action" },
            ObjectId = "commit-node",
            ActionId = "commit-action",
            ActionKind = "commit",
            ActionConfig = JsonSerializer.SerializeToElement(config, JsonOptions),
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            Options = new MicroflowActionExecutionOptions { Mode = mode }
        };
    }

    private sealed class RecordingObjectStore : IMicroflowRuntimeObjectStore
    {
        public MicroflowRuntimeObjectMutation? LastCommit { get; private set; }

        public Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        {
            LastCommit = mutation;
            return Task.FromResult(new MicroflowRuntimeObjectStoreResult
            {
                Success = true,
                Value = mutation.Value,
                Message = mutation.DryRun
                    ? "Dry-run commit accepted without persistent write."
                    : "Commit accepted."
            });
        }

        public Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
