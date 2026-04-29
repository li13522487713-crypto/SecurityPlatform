using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Objects;

namespace Atlas.Application.Microflows.Runtime.Actions;

public abstract class ObjectActionExecutorBase : IMicroflowActionExecutor
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowRuntimeObjectStore _objectStore;

    protected ObjectActionExecutorBase(IMicroflowRuntimeObjectStore objectStore)
    {
        _objectStore = objectStore;
    }

    public abstract string ActionKind { get; }

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        var result = await ExecuteCoreAsync(_objectStore, context, ct);
        started.Stop();
        if (!result.Success)
        {
            return new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Failed,
                Error = new MicroflowRuntimeErrorDto
                {
                    Code = result.Code,
                    Message = result.Message,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                },
                Message = result.Message,
                DurationMs = (int)started.ElapsedMilliseconds,
                ShouldContinueNormalFlow = false,
                ShouldStopRun = true
            };
        }

        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = result.Value ?? JsonSerializer.SerializeToElement(new { items = result.Items }, JsonOptions),
            OutputPreview = result.Message,
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    protected abstract Task<MicroflowRuntimeObjectStoreResult> ExecuteCoreAsync(
        IMicroflowRuntimeObjectStore objectStore,
        MicroflowActionExecutionContext context,
        CancellationToken ct);

    protected static MicroflowRuntimeObjectMutation Mutation(MicroflowActionExecutionContext context)
        => new()
        {
            EntityType = ReadEntityType(context),
            ObjectId = ReadString(context.ActionConfig, "objectId") ?? ReadString(context.ActionConfig, "id"),
            WorkspaceId = context.RuntimeExecutionContext.SecurityContext.WorkspaceId,
            TenantId = context.RuntimeExecutionContext.SecurityContext.TenantId,
            Value = ReadValue(context.ActionConfig),
            DryRun = string.Equals(context.Options.Mode, MicroflowRuntimeExecutionMode.TestRun, StringComparison.OrdinalIgnoreCase)
        };

    protected static MicroflowRuntimeObjectQuery Query(MicroflowActionExecutionContext context)
        => new()
        {
            EntityType = ReadEntityType(context),
            ObjectId = ReadString(context.ActionConfig, "objectId") ?? ReadString(context.ActionConfig, "id"),
            WorkspaceId = context.RuntimeExecutionContext.SecurityContext.WorkspaceId,
            TenantId = context.RuntimeExecutionContext.SecurityContext.TenantId,
            Limit = ReadInt(context.ActionConfig, "limit") ?? 100
        };

    private static string ReadEntityType(MicroflowActionExecutionContext context)
        => ReadString(context.ActionConfig, "entityType")
            ?? ReadString(context.ActionConfig, "entityQualifiedName")
            ?? ReadStringByPath(context.ActionConfig, "entity", "qualifiedName")
            ?? "Unknown.Entity";

    private static JsonElement? ReadValue(JsonElement config)
    {
        if (config.ValueKind == JsonValueKind.Object && config.TryGetProperty("value", out var value))
        {
            return value.Clone();
        }

        if (config.ValueKind == JsonValueKind.Object && config.TryGetProperty("object", out var obj))
        {
            return obj.Clone();
        }

        return null;
    }

    protected static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    protected static int? ReadInt(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.TryGetInt32(out var number)
                ? number
                : null;

    private static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}

public sealed class RetrieveObjectActionExecutor : ObjectActionExecutorBase
{
    public RetrieveObjectActionExecutor(IMicroflowRuntimeObjectStore objectStore) : base(objectStore) { }

    public override string ActionKind => "retrieve";

    protected override Task<MicroflowRuntimeObjectStoreResult> ExecuteCoreAsync(IMicroflowRuntimeObjectStore objectStore, MicroflowActionExecutionContext context, CancellationToken ct)
        => objectStore.RetrieveAsync(Query(context), ct);
}

public sealed class CreateObjectActionExecutor : ObjectActionExecutorBase
{
    public CreateObjectActionExecutor(IMicroflowRuntimeObjectStore objectStore) : base(objectStore) { }

    public override string ActionKind => "createObject";

    protected override Task<MicroflowRuntimeObjectStoreResult> ExecuteCoreAsync(IMicroflowRuntimeObjectStore objectStore, MicroflowActionExecutionContext context, CancellationToken ct)
        => objectStore.CreateAsync(Mutation(context), ct);
}

public sealed class ChangeObjectActionExecutor : ObjectActionExecutorBase
{
    public ChangeObjectActionExecutor(IMicroflowRuntimeObjectStore objectStore) : base(objectStore) { }

    public override string ActionKind => "changeMembers";

    protected override Task<MicroflowRuntimeObjectStoreResult> ExecuteCoreAsync(IMicroflowRuntimeObjectStore objectStore, MicroflowActionExecutionContext context, CancellationToken ct)
        => objectStore.ChangeAsync(Mutation(context), ct);
}

public sealed class CommitObjectActionExecutor : ObjectActionExecutorBase
{
    public CommitObjectActionExecutor(IMicroflowRuntimeObjectStore objectStore) : base(objectStore) { }

    public override string ActionKind => "commit";

    protected override Task<MicroflowRuntimeObjectStoreResult> ExecuteCoreAsync(IMicroflowRuntimeObjectStore objectStore, MicroflowActionExecutionContext context, CancellationToken ct)
        => objectStore.CommitAsync(Mutation(context), ct);
}

public sealed class DeleteObjectActionExecutor : ObjectActionExecutorBase
{
    public DeleteObjectActionExecutor(IMicroflowRuntimeObjectStore objectStore) : base(objectStore) { }

    public override string ActionKind => "delete";

    protected override Task<MicroflowRuntimeObjectStoreResult> ExecuteCoreAsync(IMicroflowRuntimeObjectStore objectStore, MicroflowActionExecutionContext context, CancellationToken ct)
        => objectStore.DeleteAsync(Mutation(context), ct);
}

public sealed class RollbackObjectActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowRuntimeObjectStore _objectStore;

    public RollbackObjectActionExecutor(IMicroflowRuntimeObjectStore objectStore)
    {
        _objectStore = objectStore;
    }

    public string ActionKind => "rollback";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        if (IsProductionRun(context) && context.RuntimeExecutionContext.UnitOfWork is null)
        {
            return Failed(started, "TRANSACTION_REQUIRED", "Rollback requires an active UnitOfWork in production run.", context);
        }

        var objectId = ReadString(context.ActionConfig, "objectId")
            ?? ReadString(context.ActionConfig, "sourceObjectId")
            ?? ReadString(context.ActionConfig, "targetObjectId")
            ?? ReadString(context.ActionConfig, "id");
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Failed(started, RuntimeErrorCode.RuntimeObjectNotFound, "Rollback requires objectId/sourceObjectId.", context);
        }

        var entityType = ReadString(context.ActionConfig, "entityType")
            ?? ReadString(context.ActionConfig, "entityQualifiedName")
            ?? "Unknown.Entity";
        var mode = ReadString(context.ActionConfig, "rollbackMode") ?? "revert";
        var clearValidationErrors = ReadBool(context.ActionConfig, "clearValidationErrors");
        var failIfNotChanged = ReadBool(context.ActionConfig, "failIfNotChanged");
        var result = await _objectStore.RollbackAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = entityType,
            ObjectId = objectId,
            WorkspaceId = context.RuntimeExecutionContext.SecurityContext.WorkspaceId,
            TenantId = context.RuntimeExecutionContext.SecurityContext.TenantId,
            DryRun = string.Equals(context.Options.Mode, MicroflowRuntimeExecutionMode.TestRun, StringComparison.OrdinalIgnoreCase)
        }, ct);

        var state = result.Success
            ? "reverted"
            : string.Equals(result.Code, RuntimeErrorCode.RuntimeObjectNotFound, StringComparison.OrdinalIgnoreCase)
                ? "noop"
                : "invalidated";
        if (state == "noop" && failIfNotChanged)
        {
            return Failed(started, RuntimeErrorCode.RuntimeRollbackFailed, $"Object '{objectId}' has no staged changes to rollback.", context);
        }

        context.TransactionManager?.TrackRollbackObject(context.RuntimeExecutionContext, new MicroflowRuntimeObjectChangeInput
        {
            EntityQualifiedName = entityType,
            ObjectId = objectId,
            VariableName = ReadString(context.ActionConfig, "objectVariableName"),
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            CollectionId = context.CollectionId,
            Preview = $"rollback {objectId}: {state}"
        });

        var output = JsonSerializer.SerializeToElement(new
        {
            objectId,
            entityType,
            rollbackState = state,
            mode,
            clearValidationErrors,
            message = result.Message
        }, JsonOptions);
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = $"{objectId}: {state}",
            TransactionSnapshot = context.RuntimeExecutionContext.CreateTransactionSnapshot("rollback"),
            Diagnostics =
            [
                new MicroflowActionExecutionDiagnostic
                {
                    Code = "ROLLBACK_OBJECT_STATE",
                    Severity = state == "reverted" ? "info" : "warning",
                    Message = $"Rollback result for '{objectId}' is {state}.",
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                }
            ],
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private static bool IsProductionRun(MicroflowActionExecutionContext context)
        => string.Equals(context.Options.Mode, MicroflowRuntimeExecutionMode.PublishedRun, StringComparison.OrdinalIgnoreCase)
           || string.Equals(context.RuntimeExecutionContext.Mode, MicroflowRuntimeExecutionMode.PublishedRun, StringComparison.OrdinalIgnoreCase);

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.True;

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static MicroflowActionExecutionResult Failed(
        Stopwatch started,
        string code,
        string message,
        MicroflowActionExecutionContext context)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId
            },
            Message = message,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = true,
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }
}
