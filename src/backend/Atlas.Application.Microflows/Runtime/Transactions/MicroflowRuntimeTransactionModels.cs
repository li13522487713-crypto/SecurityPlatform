using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Transactions;

public static class MicroflowRuntimeTransactionMode
{
    public const string None = "none";
    public const string SingleRunTransaction = "singleRunTransaction";
    public const string ActionScoped = "actionScoped";
    public const string Custom = "custom";
}

public static class MicroflowRuntimeTransactionStatus
{
    public const string None = "none";
    public const string Active = "active";
    public const string Committed = "committed";
    public const string RolledBack = "rolledBack";
    public const string Failed = "failed";
}

public static class MicroflowRuntimeObjectChangeOperation
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Rollback = "rollback";
    public const string Commit = "commit";
}

public static class MicroflowRuntimeObjectChangeStatus
{
    public const string Staged = "staged";
    public const string Committed = "committed";
    public const string RolledBack = "rolledBack";
    public const string Failed = "failed";
}

public static class MicroflowRuntimeTransactionLogLevel
{
    public const string Trace = "trace";
    public const string Debug = "debug";
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

public static class MicroflowRuntimeTransactionLogOperation
{
    public const string Begin = "begin";
    public const string StageCreate = "stageCreate";
    public const string StageUpdate = "stageUpdate";
    public const string StageDelete = "stageDelete";
    public const string StageRollback = "stageRollback";
    public const string Commit = "commit";
    public const string Rollback = "rollback";
    public const string Savepoint = "savepoint";
    public const string RollbackToSavepoint = "rollbackToSavepoint";
    public const string InvalidState = "invalidState";
}

public sealed record MicroflowRuntimeTransactionOptions
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = MicroflowRuntimeTransactionMode.SingleRunTransaction;

    [JsonPropertyName("autoBegin")]
    public bool AutoBegin { get; init; } = true;

    [JsonPropertyName("allowNested")]
    public bool AllowNested { get; init; }

    [JsonPropertyName("createSavepoints")]
    public bool CreateSavepoints { get; init; } = true;

    [JsonPropertyName("recordBeforeImage")]
    public bool RecordBeforeImage { get; init; } = true;

    [JsonPropertyName("recordAfterImage")]
    public bool RecordAfterImage { get; init; } = true;

    [JsonPropertyName("maxChangedObjects")]
    public int MaxChangedObjects { get; init; } = 200;

    [JsonPropertyName("includeRawObjectPreview")]
    public bool IncludeRawObjectPreview { get; init; }

    [JsonPropertyName("traceTransactions")]
    public bool TraceTransactions { get; init; } = true;

    [JsonPropertyName("failOnInvalidState")]
    public bool FailOnInvalidState { get; init; }
}

public sealed class MicroflowRuntimeTransactionContext
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = MicroflowRuntimeTransactionMode.SingleRunTransaction;

    [JsonPropertyName("status")]
    public string Status { get; set; } = MicroflowRuntimeTransactionStatus.Active;

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("endedAt")]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonPropertyName("startedByObjectId")]
    public string? StartedByObjectId { get; set; }

    [JsonPropertyName("endedByObjectId")]
    public string? EndedByObjectId { get; set; }

    [JsonPropertyName("changedObjects")]
    public List<MicroflowRuntimeChangedObject> ChangedObjects { get; init; } = [];

    [JsonPropertyName("committedObjects")]
    public List<MicroflowRuntimeCommittedObject> CommittedObjects { get; init; } = [];

    [JsonPropertyName("rolledBackObjects")]
    public List<MicroflowRuntimeRolledBackObject> RolledBackObjects { get; init; } = [];

    [JsonPropertyName("deletedObjects")]
    public List<MicroflowRuntimeChangedObject> DeletedObjects { get; init; } = [];

    [JsonPropertyName("savepoints")]
    public List<MicroflowRuntimeSavepoint> Savepoints { get; init; } = [];

    [JsonPropertyName("logs")]
    public List<MicroflowRuntimeTransactionLogEntry> Logs { get; init; } = [];

    [JsonPropertyName("diagnostics")]
    public List<MicroflowRuntimeTransactionDiagnostic> Diagnostics { get; init; } = [];

    [JsonPropertyName("options")]
    public MicroflowRuntimeTransactionOptions Options { get; init; } = new();

    [JsonPropertyName("rollbackReason")]
    public string? RollbackReason { get; set; }

    [JsonPropertyName("commitReason")]
    public string? CommitReason { get; set; }
}

public sealed record MicroflowRuntimeChangedObject
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = MicroflowRuntimeObjectChangeOperation.Update;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("variableName")]
    public string? VariableName { get; init; }

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("beforeJson")]
    public string? BeforeJson { get; init; }

    [JsonPropertyName("afterJson")]
    public string? AfterJson { get; init; }

    [JsonPropertyName("changedMembers")]
    public IReadOnlyList<MicroflowRuntimeChangedMember> ChangedMembers { get; init; } = Array.Empty<MicroflowRuntimeChangedMember>();

    [JsonPropertyName("associationChanges")]
    public IReadOnlyList<MicroflowRuntimeAssociationChange> AssociationChanges { get; init; } = Array.Empty<MicroflowRuntimeAssociationChange>();

    [JsonPropertyName("withEvents")]
    public bool WithEvents { get; init; }

    [JsonPropertyName("refreshInClient")]
    public bool RefreshInClient { get; init; }

    [JsonPropertyName("validateObject")]
    public bool ValidateObject { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowRuntimeObjectChangeStatus.Staged;

    [JsonPropertyName("preview")]
    public string Preview { get; init; } = string.Empty;

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowRuntimeTransactionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowRuntimeTransactionDiagnostic>();
}

public sealed record MicroflowRuntimeCommittedObject
{
    [JsonPropertyName("changeId")]
    public string ChangeId { get; init; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("variableName")]
    public string? VariableName { get; init; }

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("preview")]
    public string Preview { get; init; } = string.Empty;
}

public sealed record MicroflowRuntimeRolledBackObject
{
    [JsonPropertyName("changeId")]
    public string ChangeId { get; init; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("variableName")]
    public string? VariableName { get; init; }

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("preview")]
    public string Preview { get; init; } = string.Empty;
}

public sealed record MicroflowRuntimeChangedMember
{
    [JsonPropertyName("memberQualifiedName")]
    public string MemberQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("memberKind")]
    public string MemberKind { get; init; } = "attribute";

    [JsonPropertyName("assignmentKind")]
    public string AssignmentKind { get; init; } = "set";

    [JsonPropertyName("beforeValueJson")]
    public string? BeforeValueJson { get; init; }

    [JsonPropertyName("afterValueJson")]
    public string? AfterValueJson { get; init; }

    [JsonPropertyName("valuePreview")]
    public string? ValuePreview { get; init; }
}

public sealed record MicroflowRuntimeAssociationChange
{
    [JsonPropertyName("associationQualifiedName")]
    public string AssociationQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("assignmentKind")]
    public string AssignmentKind { get; init; } = "set";

    [JsonPropertyName("targetObjectId")]
    public string? TargetObjectId { get; init; }

    [JsonPropertyName("targetPreview")]
    public string? TargetPreview { get; init; }
}

public sealed record MicroflowRuntimeObjectChangeInput
{
    public string? EntityQualifiedName { get; init; }
    public string? ObjectId { get; init; }
    public string? VariableName { get; init; }
    public string? SourceObjectId { get; init; }
    public string? SourceActionId { get; init; }
    public string? CollectionId { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public IReadOnlyList<MicroflowRuntimeChangedMember> ChangedMembers { get; init; } = Array.Empty<MicroflowRuntimeChangedMember>();
    public IReadOnlyList<MicroflowRuntimeAssociationChange> AssociationChanges { get; init; } = Array.Empty<MicroflowRuntimeAssociationChange>();
    public bool WithEvents { get; init; }
    public bool RefreshInClient { get; init; }
    public bool ValidateObject { get; init; }
    public string? Preview { get; init; }
}

public sealed record MicroflowRuntimeCommitActionInput
{
    public string? ObjectOrListVariableName { get; init; }
    public string? SourceObjectId { get; init; }
    public string? SourceActionId { get; init; }
    public string? CollectionId { get; init; }
    public bool WithEvents { get; init; }
    public bool RefreshInClient { get; init; }
    public string? Reason { get; init; }
}

public sealed record MicroflowRuntimeTransactionOperation
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("changeId")]
    public string? ChangeId { get; init; }

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("runtimeObjectId")]
    public string? RuntimeObjectId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowRuntimeObjectChangeStatus.Staged;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

public sealed record MicroflowRuntimeTransactionLogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("level")]
    public string Level { get; init; } = MicroflowRuntimeTransactionLogLevel.Info;

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("runtimeObjectId")]
    public string? RuntimeObjectId { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("detailsJson")]
    public string? DetailsJson { get; init; }

    public MicroflowRuntimeLogDto ToRuntimeLogDto()
        => new()
        {
            Id = Id,
            Timestamp = Timestamp,
            Level = Level,
            ObjectId = ObjectId,
            ActionId = ActionId,
            Message = $"transaction.{Operation}: {Message}"
        };
}

public sealed record MicroflowRuntimeTransactionDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = RuntimeErrorCode.RuntimeUnknownError;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = MicroflowRuntimeTransactionLogLevel.Warning;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; init; }
}

public sealed record MicroflowRuntimeSavepoint
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("operationIndex")]
    public int OperationIndex { get; init; }

    [JsonPropertyName("changedObjectCount")]
    public int ChangedObjectCount { get; init; }

    [JsonPropertyName("variableSnapshotId")]
    public string? VariableSnapshotId { get; init; }

    [JsonPropertyName("transactionStatus")]
    public string TransactionStatus { get; init; } = MicroflowRuntimeTransactionStatus.Active;

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowRuntimeTransactionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowRuntimeTransactionDiagnostic>();
}

public sealed record MicroflowRuntimeTransactionSnapshotOptions
{
    public string? Operation { get; init; }
    public bool IncludeChangedObjects { get; init; } = true;
    public bool IncludeDiagnostics { get; init; } = true;
    public int MaxChangedObjectPreviewCount { get; init; } = 10;
}

public sealed record MicroflowRuntimeTransactionSnapshot
{
    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; init; }

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = MicroflowRuntimeTransactionMode.None;

    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowRuntimeTransactionStatus.None;

    [JsonPropertyName("operation")]
    public string? Operation { get; init; }

    [JsonPropertyName("changedObjectCount")]
    public int ChangedObjectCount { get; init; }

    [JsonPropertyName("committedObjectCount")]
    public int CommittedObjectCount { get; init; }

    [JsonPropertyName("rolledBackObjectCount")]
    public int RolledBackObjectCount { get; init; }

    [JsonPropertyName("deletedObjectCount")]
    public int DeletedObjectCount { get; init; }

    [JsonPropertyName("savepointCount")]
    public int SavepointCount { get; init; }

    [JsonPropertyName("logCount")]
    public int LogCount { get; init; }

    [JsonPropertyName("diagnosticsCount")]
    public int DiagnosticsCount { get; init; }

    [JsonPropertyName("changedObjectsPreview")]
    public IReadOnlyList<MicroflowRuntimeChangedObjectPreview> ChangedObjectsPreview { get; init; } = Array.Empty<MicroflowRuntimeChangedObjectPreview>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowRuntimeTransactionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowRuntimeTransactionDiagnostic>();
}

public sealed record MicroflowRuntimeChangedObjectPreview
{
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("variableName")]
    public string? VariableName { get; init; }

    [JsonPropertyName("preview")]
    public string Preview { get; init; } = string.Empty;
}

public sealed record MicroflowRuntimeTransactionSummary
{
    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = MicroflowRuntimeTransactionStatus.None;

    [JsonPropertyName("changedObjectCount")]
    public int ChangedObjectCount { get; init; }

    [JsonPropertyName("committedObjectCount")]
    public int CommittedObjectCount { get; init; }

    [JsonPropertyName("rolledBackObjectCount")]
    public int RolledBackObjectCount { get; init; }

    [JsonPropertyName("logCount")]
    public int LogCount { get; init; }

    [JsonPropertyName("diagnosticsCount")]
    public int DiagnosticsCount { get; init; }
}

public sealed record MicroflowRuntimeUnitOfWorkSnapshot
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("changes")]
    public IReadOnlyList<MicroflowRuntimeChangedObject> Changes { get; init; } = Array.Empty<MicroflowRuntimeChangedObject>();

    [JsonPropertyName("operations")]
    public IReadOnlyList<MicroflowRuntimeTransactionOperation> Operations { get; init; } = Array.Empty<MicroflowRuntimeTransactionOperation>();
}

public sealed class RuntimeTransactionException : Exception
{
    public RuntimeTransactionException(MicroflowRuntimeTransactionDiagnostic diagnostic)
        : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    public MicroflowRuntimeTransactionDiagnostic Diagnostic { get; }

    public MicroflowRuntimeErrorDto ToRuntimeError()
        => new()
        {
            Code = Diagnostic.Code,
            Message = Diagnostic.Message,
            ObjectId = Diagnostic.ObjectId,
            ActionId = Diagnostic.ActionId,
            Details = JsonSerializer.Serialize(Diagnostic, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
}
