using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowRollbackExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProductionRunWithoutUnitOfWorkFails()
    {
        var store = new InMemoryRuntimeObjectStore();
        var executor = new RollbackObjectActionExecutor(store);
        var context = Context(new { objectId = "order-1", entityType = "Sales.Order" }, MicroflowRuntimeExecutionMode.PublishedRun);

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal("TRANSACTION_REQUIRED", result.Error?.Code);
    }

    [Fact]
    public async Task MissingObjectIdFails()
    {
        var store = new InMemoryRuntimeObjectStore();
        var executor = new RollbackObjectActionExecutor(store);
        var context = Context(new { entityType = "Sales.Order" });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeObjectNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task RollbackExistingObjectInvalidatesObjectAndWritesTraceOutput()
    {
        var store = new InMemoryRuntimeObjectStore();
        await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-1",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            Value = JsonSerializer.SerializeToElement(new { id = "order-1", entityType = "Sales.Order", status = "Changed" }, JsonOptions)
        }, CancellationToken.None);
        var executor = new RollbackObjectActionExecutor(store);
        var context = Context(new { objectId = "order-1", entityType = "Sales.Order", rollbackMode = "objectOnly" });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Equal("invalidated", result.OutputJson?.GetProperty("rollbackState").GetString());
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ROLLBACK_OBJECT_STATE");
        var retrieve = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1"
        }, CancellationToken.None);
        Assert.Empty(retrieve.Items);
    }

    [Fact]
    public async Task FailIfNotChangedOnMissingObjectReturnsRollbackFailure()
    {
        var store = new InMemoryRuntimeObjectStore();
        var executor = new RollbackObjectActionExecutor(store);
        var context = Context(new { objectId = "missing", entityType = "Sales.Order", failIfNotChanged = true });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeRollbackFailed, result.Error?.Code);
    }

    private static MicroflowActionExecutionContext Context(object config, string mode = MicroflowRuntimeExecutionMode.TestRun)
    {
        var transactionManager = new MicroflowTransactionManager(new FixedClock());
        var executionPlan = new MicroflowExecutionPlan
        {
            Id = "rollback-test",
            SchemaId = "rollback-test",
            ResourceId = "mf-rollback"
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-rollback",
            executionPlan,
            mode,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            transactionManager: transactionManager);
        if (mode != MicroflowRuntimeExecutionMode.PublishedRun)
        {
            runtime.UnitOfWork = new MicroflowUnitOfWork();
        }
        else
        {
            runtime.UnitOfWork = null;
        }

        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = executionPlan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "rollback-node", ActionId = "rollback-action" },
            ObjectId = "rollback-node",
            ActionId = "rollback-action",
            ActionKind = "rollback",
            ActionConfig = JsonSerializer.SerializeToElement(config, JsonOptions),
            VariableStore = runtime.VariableStore,
            TransactionManager = transactionManager,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            Options = new MicroflowActionExecutionOptions { Mode = mode }
        };
    }

    private sealed class FixedClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow => new(2026, 4, 29, 0, 0, 0, TimeSpan.Zero);
    }
}
