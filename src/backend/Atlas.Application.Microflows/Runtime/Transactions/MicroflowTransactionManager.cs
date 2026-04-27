using System.Text.Json;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Transactions;

public sealed class MicroflowTransactionManager : IMicroflowTransactionManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowClock _clock;

    public MicroflowTransactionManager(IMicroflowClock clock)
    {
        _clock = clock;
    }

    public MicroflowRuntimeTransactionContext Begin(
        RuntimeExecutionContext context,
        MicroflowRuntimeTransactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (context.Transaction is not null
            && string.Equals(context.Transaction.Status, MicroflowRuntimeTransactionStatus.Active, StringComparison.OrdinalIgnoreCase)
            && !options.AllowNested)
        {
            InvalidState(context, "RUNTIME_TRANSACTION_ALREADY_ACTIVE", "Transaction is already active.", null, null);
            return context.Transaction;
        }

        var transaction = new MicroflowRuntimeTransactionContext
        {
            Mode = options.Mode,
            Status = string.Equals(options.Mode, MicroflowRuntimeTransactionMode.None, StringComparison.OrdinalIgnoreCase)
                ? MicroflowRuntimeTransactionStatus.None
                : MicroflowRuntimeTransactionStatus.Active,
            StartedAt = _clock.UtcNow,
            Options = options
        };
        context.Transaction = transaction;
        context.UnitOfWork = new MicroflowUnitOfWork();
        context.TransactionOptions = options;
        context.TransactionManager = this;

        AddLog(context, MicroflowRuntimeTransactionLogOperation.Begin, MicroflowRuntimeTransactionLogLevel.Info, "Runtime transaction started.");
        return transaction;
    }

    public void Commit(RuntimeExecutionContext context, string? reason = null)
    {
        var transaction = RequireActive(context, MicroflowRuntimeTransactionLogOperation.Commit);
        if (transaction is null)
        {
            return;
        }

        context.UnitOfWork?.MarkCommitted(reason);
        for (var index = 0; index < transaction.ChangedObjects.Count; index++)
        {
            var committed = transaction.ChangedObjects[index] with { Status = MicroflowRuntimeObjectChangeStatus.Committed };
            transaction.ChangedObjects[index] = committed;
            AddCommitted(transaction, committed, reason);
        }

        transaction.Status = MicroflowRuntimeTransactionStatus.Committed;
        transaction.EndedAt = _clock.UtcNow;
        transaction.CommitReason = reason;
        AddLog(context, MicroflowRuntimeTransactionLogOperation.Commit, MicroflowRuntimeTransactionLogLevel.Info, reason ?? "Runtime transaction committed.");
    }

    public void Rollback(RuntimeExecutionContext context, string? reason = null, MicroflowRuntimeErrorDto? error = null)
    {
        var transaction = context.Transaction;
        if (transaction is null
            || string.Equals(transaction.Status, MicroflowRuntimeTransactionStatus.None, StringComparison.OrdinalIgnoreCase))
        {
            InvalidState(context, "RUNTIME_TRANSACTION_NOT_ACTIVE", "Cannot rollback because no transaction is active.", error?.ObjectId, error?.ActionId);
            return;
        }

        if (string.Equals(transaction.Status, MicroflowRuntimeTransactionStatus.Committed, StringComparison.OrdinalIgnoreCase))
        {
            InvalidState(context, "RUNTIME_TRANSACTION_ALREADY_COMMITTED", "Cannot rollback a committed transaction.", error?.ObjectId, error?.ActionId);
            return;
        }

        if (string.Equals(transaction.Status, MicroflowRuntimeTransactionStatus.RolledBack, StringComparison.OrdinalIgnoreCase))
        {
            InvalidState(context, "RUNTIME_TRANSACTION_ALREADY_ROLLED_BACK", "Transaction is already rolled back.", error?.ObjectId, error?.ActionId);
            return;
        }

        context.UnitOfWork?.MarkRolledBack(reason ?? error?.Message);
        for (var index = 0; index < transaction.ChangedObjects.Count; index++)
        {
            var rolledBack = transaction.ChangedObjects[index] with { Status = MicroflowRuntimeObjectChangeStatus.RolledBack };
            transaction.ChangedObjects[index] = rolledBack;
            AddRolledBack(transaction, rolledBack, reason ?? error?.Message);
        }

        transaction.Status = MicroflowRuntimeTransactionStatus.RolledBack;
        transaction.EndedAt = _clock.UtcNow;
        transaction.RollbackReason = reason ?? error?.Message;
        AddLog(context, MicroflowRuntimeTransactionLogOperation.Rollback, MicroflowRuntimeTransactionLogLevel.Warning, reason ?? error?.Message ?? "Runtime transaction rolled back.", error?.ObjectId, error?.ActionId);
    }

    public MicroflowRuntimeSavepoint CreateSavepoint(RuntimeExecutionContext context, string name)
    {
        var transaction = EnsureTransaction(context);
        if (transaction is null)
        {
            var savepoint = new MicroflowRuntimeSavepoint { Name = name, TransactionStatus = MicroflowRuntimeTransactionStatus.None };
            return savepoint;
        }

        var result = new MicroflowRuntimeSavepoint
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"savepoint-{transaction.Savepoints.Count + 1}" : name,
            CreatedAt = _clock.UtcNow,
            OperationIndex = context.UnitOfWork?.Operations.Count ?? 0,
            ChangedObjectCount = transaction.ChangedObjects.Count,
            VariableSnapshotId = $"{context.RunId}:{context.StepIndex}",
            TransactionStatus = transaction.Status
        };
        transaction.Savepoints.Add(result);
        AddLog(context, MicroflowRuntimeTransactionLogOperation.Savepoint, MicroflowRuntimeTransactionLogLevel.Debug, $"Savepoint '{result.Name}' created.");
        return result;
    }

    public void RollbackToSavepoint(RuntimeExecutionContext context, string savepointId, string? reason = null)
    {
        var transaction = RequireActive(context, MicroflowRuntimeTransactionLogOperation.RollbackToSavepoint);
        if (transaction is null)
        {
            return;
        }

        var savepoint = transaction.Savepoints.FirstOrDefault(item => string.Equals(item.Id, savepointId, StringComparison.Ordinal)
            || string.Equals(item.Name, savepointId, StringComparison.OrdinalIgnoreCase));
        if (savepoint is null)
        {
            InvalidState(context, "RUNTIME_TRANSACTION_SAVEPOINT_NOT_FOUND", $"Savepoint '{savepointId}' was not found.", null, null);
            return;
        }

        var rolledBack = transaction.ChangedObjects.Skip(savepoint.ChangedObjectCount).ToArray();
        for (var index = savepoint.ChangedObjectCount; index < transaction.ChangedObjects.Count; index++)
        {
            var change = transaction.ChangedObjects[index] with { Status = MicroflowRuntimeObjectChangeStatus.RolledBack };
            transaction.ChangedObjects[index] = change;
            AddRolledBack(transaction, change, reason ?? $"Rollback to savepoint '{savepoint.Name}'.");
        }

        if (context.UnitOfWork is MicroflowUnitOfWork unitOfWork)
        {
            var active = transaction.ChangedObjects
                .Take(savepoint.ChangedObjectCount)
                .Where(change => string.Equals(change.Status, MicroflowRuntimeObjectChangeStatus.Staged, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            unitOfWork.Clear();
            foreach (var change in active)
            {
                unitOfWork.Stage(change);
            }
        }

        AddLog(
            context,
            MicroflowRuntimeTransactionLogOperation.RollbackToSavepoint,
            MicroflowRuntimeTransactionLogLevel.Warning,
            $"Rolled back {rolledBack.Length} staged changes to savepoint '{savepoint.Name}'.");
    }

    public void TrackCreate(RuntimeExecutionContext context, MicroflowRuntimeObjectChangeInput input)
        => Stage(context, input, MicroflowRuntimeObjectChangeOperation.Create, MicroflowRuntimeTransactionLogOperation.StageCreate);

    public void TrackUpdate(RuntimeExecutionContext context, MicroflowRuntimeObjectChangeInput input)
        => Stage(context, input, MicroflowRuntimeObjectChangeOperation.Update, MicroflowRuntimeTransactionLogOperation.StageUpdate);

    public void TrackDelete(RuntimeExecutionContext context, MicroflowRuntimeObjectChangeInput input)
    {
        var change = Stage(context, input, MicroflowRuntimeObjectChangeOperation.Delete, MicroflowRuntimeTransactionLogOperation.StageDelete);
        if (change is not null)
        {
            context.Transaction?.DeletedObjects.Add(change);
        }
    }

    public void TrackRollbackObject(RuntimeExecutionContext context, MicroflowRuntimeObjectChangeInput input)
    {
        var change = Stage(context, input, MicroflowRuntimeObjectChangeOperation.Rollback, MicroflowRuntimeTransactionLogOperation.StageRollback, MicroflowRuntimeObjectChangeStatus.RolledBack);
        if (change is not null && context.Transaction is not null)
        {
            AddRolledBack(context.Transaction, change, "RollbackAction");
        }
    }

    public void TrackCommitAction(RuntimeExecutionContext context, MicroflowRuntimeCommitActionInput input)
    {
        var transaction = EnsureTransaction(context);
        if (transaction is null)
        {
            return;
        }

        var matched = transaction.ChangedObjects
            .Where(change => string.IsNullOrWhiteSpace(input.ObjectOrListVariableName)
                || string.Equals(change.VariableName, input.ObjectOrListVariableName, StringComparison.Ordinal))
            .Where(change => string.Equals(change.Status, MicroflowRuntimeObjectChangeStatus.Staged, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var change in matched)
        {
            var committed = change with
            {
                Status = MicroflowRuntimeObjectChangeStatus.Committed,
                WithEvents = input.WithEvents || change.WithEvents,
                RefreshInClient = input.RefreshInClient || change.RefreshInClient
            };
            ReplaceChange(transaction, committed);
            AddCommitted(transaction, committed, input.Reason ?? "CommitAction");
        }

        AddLog(
            context,
            MicroflowRuntimeTransactionLogOperation.Commit,
            MicroflowRuntimeTransactionLogLevel.Info,
            $"CommitAction staged {matched.Length} object changes for variable '{input.ObjectOrListVariableName ?? "*"}'.",
            input.SourceObjectId,
            input.SourceActionId,
            details: new { input.ObjectOrListVariableName, input.WithEvents, input.RefreshInClient, matched = matched.Length });
    }

    public MicroflowRuntimeTransactionSnapshot CreateSnapshot(RuntimeExecutionContext context, MicroflowRuntimeTransactionSnapshotOptions options)
    {
        var transaction = context.Transaction;
        if (transaction is null)
        {
            return new MicroflowRuntimeTransactionSnapshot
            {
                Operation = options.Operation,
                Status = MicroflowRuntimeTransactionStatus.None,
                Mode = MicroflowRuntimeTransactionMode.None
            };
        }

        return new MicroflowRuntimeTransactionSnapshot
        {
            TransactionId = transaction.Id,
            Mode = transaction.Mode,
            Status = transaction.Status,
            Operation = options.Operation,
            ChangedObjectCount = transaction.ChangedObjects.Count,
            CommittedObjectCount = transaction.CommittedObjects.Count,
            RolledBackObjectCount = transaction.RolledBackObjects.Count,
            DeletedObjectCount = transaction.DeletedObjects.Count,
            SavepointCount = transaction.Savepoints.Count,
            LogCount = transaction.Logs.Count,
            DiagnosticsCount = transaction.Diagnostics.Count,
            ChangedObjectsPreview = options.IncludeChangedObjects
                ? transaction.ChangedObjects
                    .Take(Math.Clamp(options.MaxChangedObjectPreviewCount, 0, 100))
                    .Select(ToPreview)
                    .ToArray()
                : Array.Empty<MicroflowRuntimeChangedObjectPreview>(),
            Diagnostics = options.IncludeDiagnostics ? transaction.Diagnostics.ToArray() : Array.Empty<MicroflowRuntimeTransactionDiagnostic>()
        };
    }

    public MicroflowRuntimeTransactionSnapshot RollbackForError(
        RuntimeExecutionContext context,
        MicroflowRuntimeErrorDto error,
        string? sourceObjectId,
        string? sourceActionId)
    {
        var normalized = error with
        {
            ObjectId = error.ObjectId ?? sourceObjectId,
            ActionId = error.ActionId ?? sourceActionId
        };
        Rollback(context, "ErrorHandling rollback", normalized);
        return CreateSnapshot(context, new MicroflowRuntimeTransactionSnapshotOptions { Operation = "errorRollback" });
    }

    public MicroflowRuntimeTransactionSnapshot PrepareCustomWithRollback(RuntimeExecutionContext context, MicroflowRuntimeErrorDto error)
    {
        Rollback(context, "customWithRollback", error);
        return CreateSnapshot(context, new MicroflowRuntimeTransactionSnapshotOptions { Operation = "customWithRollback" });
    }

    public MicroflowRuntimeTransactionSnapshot PrepareCustomWithoutRollback(RuntimeExecutionContext context, MicroflowRuntimeErrorDto error)
    {
        AddLog(context, MicroflowRuntimeTransactionLogOperation.Rollback, MicroflowRuntimeTransactionLogLevel.Warning, "customWithoutRollback kept transaction active.", error.ObjectId, error.ActionId);
        return CreateSnapshot(context, new MicroflowRuntimeTransactionSnapshotOptions { Operation = "customWithoutRollback" });
    }

    public MicroflowRuntimeTransactionSnapshot ContinueAfterError(RuntimeExecutionContext context, MicroflowRuntimeErrorDto error)
    {
        AddLog(context, MicroflowRuntimeTransactionLogOperation.Rollback, MicroflowRuntimeTransactionLogLevel.Warning, "continue error handling kept transaction active.", error.ObjectId, error.ActionId);
        return CreateSnapshot(context, new MicroflowRuntimeTransactionSnapshotOptions { Operation = "continue" });
    }

    private MicroflowRuntimeChangedObject? Stage(
        RuntimeExecutionContext context,
        MicroflowRuntimeObjectChangeInput input,
        string operation,
        string logOperation,
        string status = MicroflowRuntimeObjectChangeStatus.Staged)
    {
        var transaction = EnsureTransaction(context);
        if (transaction is null)
        {
            return null;
        }

        if (transaction.ChangedObjects.Count >= Math.Max(1, transaction.Options.MaxChangedObjects))
        {
            InvalidState(context, "RUNTIME_TRANSACTION_CHANGE_LIMIT", $"Transaction changed object limit {transaction.Options.MaxChangedObjects} was reached.", input.SourceObjectId, input.SourceActionId);
            return null;
        }

        var objectId = string.IsNullOrWhiteSpace(input.ObjectId)
            ? $"runtime-{operation}-{Guid.NewGuid():N}"
            : input.ObjectId!;
        var change = new MicroflowRuntimeChangedObject
        {
            Operation = operation,
            EntityQualifiedName = input.EntityQualifiedName,
            ObjectId = objectId,
            VariableName = input.VariableName,
            SourceObjectId = input.SourceObjectId,
            SourceActionId = input.SourceActionId,
            CollectionId = input.CollectionId,
            BeforeJson = transaction.Options.RecordBeforeImage ? TrimJson(input.BeforeJson) : null,
            AfterJson = transaction.Options.RecordAfterImage ? TrimJson(input.AfterJson) : null,
            ChangedMembers = input.ChangedMembers,
            AssociationChanges = input.AssociationChanges,
            WithEvents = input.WithEvents,
            RefreshInClient = input.RefreshInClient,
            ValidateObject = input.ValidateObject,
            Timestamp = _clock.UtcNow,
            Status = status,
            Preview = TrimPreview(input.Preview ?? $"{operation} {input.EntityQualifiedName ?? input.VariableName ?? objectId}", 240)
        };

        transaction.ChangedObjects.Add(change);
        context.UnitOfWork?.Stage(change);
        AddLog(context, logOperation, MicroflowRuntimeTransactionLogLevel.Debug, change.Preview, input.SourceObjectId, input.SourceActionId, input.EntityQualifiedName, objectId);
        return change;
    }

    private MicroflowRuntimeTransactionContext? EnsureTransaction(RuntimeExecutionContext context)
    {
        if (context.Transaction is not null
            && string.Equals(context.Transaction.Status, MicroflowRuntimeTransactionStatus.Active, StringComparison.OrdinalIgnoreCase))
        {
            return context.Transaction;
        }

        var options = context.TransactionOptions ?? new MicroflowRuntimeTransactionOptions();
        if (!options.AutoBegin || string.Equals(options.Mode, MicroflowRuntimeTransactionMode.None, StringComparison.OrdinalIgnoreCase))
        {
            InvalidState(context, "RUNTIME_TRANSACTION_NOT_ACTIVE", "Transaction is not active.", null, null);
            return null;
        }

        return Begin(context, options);
    }

    private MicroflowRuntimeTransactionContext? RequireActive(RuntimeExecutionContext context, string operation)
    {
        if (context.Transaction is null
            || !string.Equals(context.Transaction.Status, MicroflowRuntimeTransactionStatus.Active, StringComparison.OrdinalIgnoreCase))
        {
            InvalidState(context, "RUNTIME_TRANSACTION_NOT_ACTIVE", $"Cannot {operation} because transaction is not active.", null, null);
            return null;
        }

        return context.Transaction;
    }

    private void InvalidState(RuntimeExecutionContext context, string code, string message, string? objectId, string? actionId)
    {
        var diagnostic = new MicroflowRuntimeTransactionDiagnostic
        {
            Code = code,
            Severity = MicroflowRuntimeTransactionLogLevel.Warning,
            Message = message,
            ObjectId = objectId,
            ActionId = actionId,
            TransactionId = context.Transaction?.Id
        };
        context.Transaction?.Diagnostics.Add(diagnostic);
        AddLog(context, MicroflowRuntimeTransactionLogOperation.InvalidState, MicroflowRuntimeTransactionLogLevel.Warning, message, objectId, actionId);

        if (context.TransactionOptions?.FailOnInvalidState == true || context.Transaction?.Options.FailOnInvalidState == true)
        {
            throw new RuntimeTransactionException(diagnostic);
        }
    }

    private void AddLog(
        RuntimeExecutionContext context,
        string operation,
        string level,
        string message,
        string? objectId = null,
        string? actionId = null,
        string? entityQualifiedName = null,
        string? runtimeObjectId = null,
        object? details = null)
    {
        var transaction = context.Transaction;
        if (transaction is null)
        {
            return;
        }

        transaction.Logs.Add(new MicroflowRuntimeTransactionLogEntry
        {
            Timestamp = _clock.UtcNow,
            Level = level,
            Operation = operation,
            ObjectId = objectId,
            ActionId = actionId,
            EntityQualifiedName = entityQualifiedName,
            RuntimeObjectId = runtimeObjectId,
            Message = TrimPreview(message, 500),
            DetailsJson = details is null ? null : TrimJson(JsonSerializer.Serialize(details, JsonOptions), 1000)
        });
    }

    private static void AddCommitted(MicroflowRuntimeTransactionContext transaction, MicroflowRuntimeChangedObject change, string? reason)
    {
        if (transaction.CommittedObjects.Any(item => string.Equals(item.ChangeId, change.Id, StringComparison.Ordinal)))
        {
            return;
        }

        transaction.CommittedObjects.Add(new MicroflowRuntimeCommittedObject
        {
            ChangeId = change.Id,
            Operation = change.Operation,
            EntityQualifiedName = change.EntityQualifiedName,
            ObjectId = change.ObjectId,
            VariableName = change.VariableName,
            SourceObjectId = change.SourceObjectId,
            SourceActionId = change.SourceActionId,
            Reason = reason,
            Preview = change.Preview
        });
    }

    private static void AddRolledBack(MicroflowRuntimeTransactionContext transaction, MicroflowRuntimeChangedObject change, string? reason)
    {
        if (transaction.RolledBackObjects.Any(item => string.Equals(item.ChangeId, change.Id, StringComparison.Ordinal)))
        {
            return;
        }

        transaction.RolledBackObjects.Add(new MicroflowRuntimeRolledBackObject
        {
            ChangeId = change.Id,
            Operation = change.Operation,
            EntityQualifiedName = change.EntityQualifiedName,
            ObjectId = change.ObjectId,
            VariableName = change.VariableName,
            SourceObjectId = change.SourceObjectId,
            SourceActionId = change.SourceActionId,
            Reason = reason,
            Preview = change.Preview
        });
    }

    private static void ReplaceChange(MicroflowRuntimeTransactionContext transaction, MicroflowRuntimeChangedObject change)
    {
        var index = transaction.ChangedObjects.FindIndex(item => string.Equals(item.Id, change.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            transaction.ChangedObjects[index] = change;
        }
    }

    private static MicroflowRuntimeChangedObjectPreview ToPreview(MicroflowRuntimeChangedObject change)
        => new()
        {
            Operation = change.Operation,
            Status = change.Status,
            EntityQualifiedName = change.EntityQualifiedName,
            ObjectId = change.ObjectId,
            VariableName = change.VariableName,
            Preview = change.Preview
        };

    private static string? TrimJson(string? json, int maxLength = 4000)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return TrimPreview(document.RootElement.GetRawText(), maxLength);
        }
        catch (JsonException)
        {
            return TrimPreview(JsonSerializer.Serialize(json, JsonOptions), maxLength);
        }
    }

    private static string TrimPreview(string? value, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "transaction" : value!;
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }
}
