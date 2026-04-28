using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowTransactionManagerTests
{
    [Fact]
    public void BeginTrackCommit_CreatesStructuredCommitOperation()
    {
        var manager = CreateManager();
        var context = CreateContext(manager);

        manager.TrackCreate(context, Change("Sales.Order", "order", "create-action"));
        manager.TrackUpdate(context, Change("Sales.Order", "order", "change-action"));
        manager.TrackCommitAction(context, new MicroflowRuntimeCommitActionInput
        {
            ObjectOrListVariableName = "order",
            SourceObjectId = "commit",
            SourceActionId = "commit-action",
            WithEvents = true,
            RefreshInClient = true
        });
        manager.Commit(context, "run completed");

        Assert.Equal(MicroflowRuntimeTransactionStatus.Committed, context.Transaction?.Status);
        Assert.Contains(context.Transaction!.ChangedObjects, change => change.Operation == MicroflowRuntimeObjectChangeOperation.Commit);
        Assert.All(
            context.Transaction.ChangedObjects.Where(change => change.Operation != MicroflowRuntimeObjectChangeOperation.Rollback),
            change => Assert.Equal(MicroflowRuntimeObjectChangeStatus.Committed, change.Status));
        Assert.True(context.Transaction.CommittedObjects.Count >= 3);
        Assert.Contains(context.Transaction.Logs, log => log.Operation == MicroflowRuntimeTransactionLogOperation.Commit);
    }

    [Fact]
    public void RollbackToSavepoint_DoesNotCommitRolledBackStagedChanges()
    {
        var manager = CreateManager();
        var context = CreateContext(manager);

        manager.TrackCreate(context, Change("Sales.Order", "order", "create-action"));
        var savepoint = manager.CreateSavepoint(context, "before-update");
        manager.TrackUpdate(context, Change("Sales.Order", "order", "change-action"));
        manager.RollbackToSavepoint(context, savepoint.Id, "discard update");
        manager.Commit(context, "run completed");

        var update = Assert.Single(context.Transaction!.ChangedObjects, change => change.SourceActionId == "change-action");
        Assert.Equal(MicroflowRuntimeObjectChangeStatus.RolledBack, update.Status);
        Assert.DoesNotContain(context.Transaction.CommittedObjects, item => item.ChangeId == update.Id);
        Assert.Contains(context.Transaction.RolledBackObjects, item => item.ChangeId == update.Id);
        Assert.Equal(MicroflowRuntimeTransactionStatus.Committed, context.Transaction.Status);
    }

    [Fact]
    public void InvalidStates_AreRecordedAsDiagnostics()
    {
        var manager = CreateManager();
        var context = CreateContext(manager, autoBegin: false);

        manager.Commit(context, "no active transaction");

        Assert.Equal(MicroflowRuntimeTransactionStatus.None, context.Transaction?.Status);
        Assert.Contains(context.TransactionDiagnostics, diagnostic => diagnostic.Code == "RUNTIME_TRANSACTION_NOT_ACTIVE");

        manager.Begin(context, new MicroflowRuntimeTransactionOptions());
        manager.Commit(context, "first commit");
        manager.Rollback(context, "rollback after commit");

        Assert.Equal(MicroflowRuntimeTransactionStatus.Committed, context.Transaction?.Status);
        Assert.Contains(context.TransactionDiagnostics, diagnostic => diagnostic.Code == "RUNTIME_TRANSACTION_ALREADY_COMMITTED");
    }

    [Fact]
    public void ErrorHandlingInterfaces_RespectRollbackMode()
    {
        var manager = CreateManager();
        var context = CreateContext(manager);
        var error = new MicroflowRuntimeErrorDto { Code = "REST_ERROR", Message = "simulated", ObjectId = "rest", ActionId = "rest-action" };

        manager.PrepareCustomWithoutRollback(context, error);
        Assert.Equal(MicroflowRuntimeTransactionStatus.Active, context.Transaction?.Status);

        manager.ContinueAfterError(context, error);
        Assert.Equal(MicroflowRuntimeTransactionStatus.Active, context.Transaction?.Status);

        manager.PrepareCustomWithRollback(context, error);
        Assert.Equal(MicroflowRuntimeTransactionStatus.RolledBack, context.Transaction?.Status);
        Assert.Contains(context.Transaction!.Logs, log => log.Operation == MicroflowRuntimeTransactionLogOperation.ErrorHandlingKeepActive);
        Assert.Contains(context.Transaction.Logs, log => log.Operation == MicroflowRuntimeTransactionLogOperation.ErrorHandlingContinue);
    }

    private static RuntimeExecutionContext CreateContext(IMicroflowTransactionManager manager, bool autoBegin = true)
    {
        var options = new MicroflowRuntimeTransactionOptions { AutoBegin = autoBegin };
        return RuntimeExecutionContext.Create(
            "run-transaction-test",
            new MicroflowExecutionPlan
            {
                Id = "plan-transaction-test",
                SchemaId = "schema-transaction-test",
                StartNodeId = "start"
            },
            MicroflowRuntimeExecutionMode.TestRun,
            input: null,
            securityContext: null,
            startedAt: DateTimeOffset.UtcNow,
            transactionManager: manager,
            transactionOptions: options);
    }

    private static MicroflowTransactionManager CreateManager()
        => new(new TestClock());

    private static MicroflowRuntimeObjectChangeInput Change(string entityQualifiedName, string variableName, string actionId)
        => new()
        {
            EntityQualifiedName = entityQualifiedName,
            ObjectId = $"runtime-{actionId}",
            VariableName = variableName,
            SourceObjectId = actionId.Replace("-action", string.Empty, StringComparison.Ordinal),
            SourceActionId = actionId,
            BeforeJson = """{"status":"before"}""",
            AfterJson = """{"status":"after"}""",
            ChangedMembers =
            [
                new MicroflowRuntimeChangedMember
                {
                    MemberQualifiedName = "Sales.Order.Status",
                    MemberKind = "attribute",
                    AssignmentKind = "set",
                    BeforeValueJson = "\"before\"",
                    AfterValueJson = "\"after\"",
                    ValuePreview = "after"
                }
            ],
            Preview = $"{actionId} {variableName}"
        };

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 4, 27, 12, 0, 0, TimeSpan.Zero);
    }
}
