using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Metadata;
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

public sealed class CastObjectActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowEntityAccessService _entityAccessService;

    public CastObjectActionExecutor(IMicroflowEntityAccessService entityAccessService)
    {
        _entityAccessService = entityAccessService;
    }

    public string ActionKind => "cast";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var sourceVariable = ReadString(context.ActionConfig, "sourceVariable")
            ?? ReadString(context.ActionConfig, "sourceVariableName")
            ?? ReadString(context.ActionConfig, "objectVariableName");
        var outputVariable = ReadString(context.ActionConfig, "outputVariable")
            ?? ReadString(context.ActionConfig, "outputVariableName")
            ?? ReadString(context.ActionConfig, "targetVariableName")
            ?? sourceVariable;
        var targetEntity = ReadString(context.ActionConfig, "targetEntity")
            ?? ReadString(context.ActionConfig, "targetEntityQualifiedName")
            ?? ReadString(context.ActionConfig, "entityType")
            ?? ReadString(context.ActionConfig, "entityQualifiedName");
        var sourceEntity = ReadString(context.ActionConfig, "sourceEntity")
            ?? ReadString(context.ActionConfig, "sourceEntityQualifiedName");
        var strict = !string.Equals(ReadString(context.ActionConfig, "castMode"), "allowNull", StringComparison.OrdinalIgnoreCase)
            && ReadBool(context.ActionConfig, "failOnInvalidType", fallback: true);
        var allowNull = ReadBool(context.ActionConfig, "allowNull")
            || string.Equals(ReadString(context.ActionConfig, "castMode"), "allowNull", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(sourceVariable) || string.IsNullOrWhiteSpace(outputVariable) || string.IsNullOrWhiteSpace(targetEntity))
        {
            return Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "Cast requires sourceVariable, targetVariable and targetEntity.", context);
        }

        if (!context.VariableStore.TryGet(sourceVariable!, out var variable) || variable is null || string.IsNullOrWhiteSpace(variable.RawValueJson))
        {
            if (allowNull)
            {
                return SuccessNull(started, context, outputVariable!, targetEntity!, "source-null");
            }

            return Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, $"Cast source variable '{sourceVariable}' was not found.", context);
        }

        var sourceValue = MicroflowVariableStore.ToJsonElement(variable.RawValueJson);
        if (!sourceValue.HasValue || sourceValue.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (allowNull)
            {
                return SuccessNull(started, context, outputVariable!, targetEntity!, "source-null");
            }

            return Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Cast source variable '{sourceVariable}' is null.", context);
        }

        var actualEntity = sourceEntity
            ?? ReadString(sourceValue.Value, "entityType")
            ?? ReadString(sourceValue.Value, "entityQualifiedName")
            ?? ReadString(sourceValue.Value, "$entity");
        if (string.IsNullOrWhiteSpace(actualEntity))
        {
            actualEntity = TryReadEntityFromType(variable.DataTypeJson);
        }

        if (!IsAssignable(actualEntity, targetEntity!, context.MetadataCatalog))
        {
            if (!strict || allowNull)
            {
                return SuccessNull(started, context, outputVariable!, targetEntity!, "type-mismatch");
            }

            return Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Cannot cast '{actualEntity ?? "unknown"}' to '{targetEntity}'.", context);
        }

        var access = await _entityAccessService.CanReadAsync(
            context.RuntimeSecurityContext,
            ResolveEntity(targetEntity!, context.MetadataCatalog),
            ct);
        if (!access.Allowed)
        {
            return Failed(started, RuntimeErrorCode.RuntimeEntityAccessDenied, access.Reason, context);
        }

        DefineCastVariable(context, outputVariable!, targetEntity!, sourceValue.Value);
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = sourceValue.Value.Clone(),
            OutputPreview = $"{outputVariable}:{targetEntity}",
            ProducedVariables =
            [
                new MicroflowRuntimeVariableValueDto
                {
                    Name = outputVariable!,
                    ValuePreview = MicroflowVariableStore.Preview(sourceValue.Value.GetRawText()),
                    RawValueJson = sourceValue.Value.GetRawText(),
                    Source = MicroflowVariableSourceKind.ActionOutput
                }
            ],
            Diagnostics =
            [
                new MicroflowActionExecutionDiagnostic
                {
                    Code = "CAST_OBJECT_SUCCESS",
                    Severity = "info",
                    Message = $"Cast '{sourceVariable}' to '{targetEntity}'.",
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                }
            ],
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private static bool IsAssignable(string? actualEntity, string targetEntity, MicroflowMetadataCatalogDto? catalog)
    {
        if (string.Equals(actualEntity, targetEntity, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(actualEntity) || catalog is null)
        {
            return false;
        }

        var entities = catalog.Entities.ToDictionary(entity => entity.QualifiedName, StringComparer.OrdinalIgnoreCase);
        if (!entities.TryGetValue(actualEntity, out var current))
        {
            return false;
        }

        while (!string.IsNullOrWhiteSpace(current.Generalization))
        {
            if (string.Equals(current.Generalization, targetEntity, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!entities.TryGetValue(current.Generalization, out current!))
            {
                return false;
            }
        }

        return entities.TryGetValue(targetEntity, out var target)
            && target.Specializations.Contains(actualEntity, StringComparer.OrdinalIgnoreCase);
    }

    private static MicroflowResolvedEntity ResolveEntity(string qualifiedName, MicroflowMetadataCatalogDto? catalog)
    {
        var entity = catalog?.Entities.FirstOrDefault(item => string.Equals(item.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase));
        return new MicroflowResolvedEntity
        {
            Found = entity is not null || catalog is null,
            QualifiedName = qualifiedName,
            Entity = entity,
            Generalization = entity?.Generalization,
            Specializations = entity?.Specializations ?? Array.Empty<string>(),
            IsPersistable = entity?.IsPersistable ?? true,
            IsSystemEntity = entity?.IsSystemEntity ?? false
        };
    }

    private static void DefineCastVariable(MicroflowActionExecutionContext context, string outputVariable, string targetEntity, JsonElement sourceValue)
    {
        var raw = sourceValue.GetRawText();
        var value = new MicroflowRuntimeVariableValue
        {
            Name = outputVariable,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "object", entityQualifiedName = targetEntity }, JsonOptions),
            RawValueJson = raw,
            ValuePreview = MicroflowVariableStore.Preview(raw),
            SourceKind = MicroflowVariableSourceKind.ActionOutput,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action
        };

        if (context.VariableStore.Exists(outputVariable))
        {
            context.VariableStore.Set(outputVariable, value);
            return;
        }

        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = outputVariable,
            DataTypeJson = value.DataTypeJson,
            RawValueJson = raw,
            ValuePreview = value.ValuePreview,
            SourceKind = MicroflowVariableSourceKind.ActionOutput,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action,
            Value = value,
            AllowShadowing = true
        });
    }

    private static MicroflowActionExecutionResult SuccessNull(Stopwatch started, MicroflowActionExecutionContext context, string outputVariable, string targetEntity, string reason)
    {
        var value = JsonSerializer.SerializeToElement<object?>(null, JsonOptions);
        DefineCastVariable(context, outputVariable, targetEntity, value);
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = value,
            OutputPreview = $"{outputVariable}: null",
            Diagnostics =
            [
                new MicroflowActionExecutionDiagnostic
                {
                    Code = "CAST_OBJECT_NULL",
                    Severity = "warning",
                    Message = $"Cast returned null ({reason}).",
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                }
            ],
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private static string? TryReadEntityFromType(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return ReadString(document.RootElement, "entityQualifiedName")
                ?? ReadString(document.RootElement, "entityType");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool fallback = false)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
            ? value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed)
            : fallback;

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static MicroflowActionExecutionResult Failed(Stopwatch started, string code, string message, MicroflowActionExecutionContext context)
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

public sealed class CastObjectActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ActionKind => "cast";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var sourceVariable = ReadString(context.ActionConfig, "sourceVariable")
            ?? ReadString(context.ActionConfig, "sourceVariableName")
            ?? ReadString(context.ActionConfig, "objectVariableName");
        var outputVariable = ReadString(context.ActionConfig, "outputVariableName")
            ?? ReadString(context.ActionConfig, "targetVariableName")
            ?? sourceVariable;
        var targetEntity = ReadString(context.ActionConfig, "targetEntity")
            ?? ReadString(context.ActionConfig, "targetEntityQualifiedName")
            ?? ReadString(context.ActionConfig, "entityType");
        var castMode = ReadString(context.ActionConfig, "castMode")
            ?? ReadString(context.ActionConfig, "mode")
            ?? "strict";
        var allowNull = ReadBool(context.ActionConfig, "allowNull");
        var failOnInvalidType = ReadBool(context.ActionConfig, "failOnInvalidType", defaultValue: true)
            || string.Equals(castMode, "strict", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(sourceVariable) || string.IsNullOrWhiteSpace(outputVariable) || string.IsNullOrWhiteSpace(targetEntity))
        {
            return Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "Cast requires sourceVariable, targetVariable/outputVariable and targetEntity.", context);
        }

        if (!context.VariableStore.TryGet(sourceVariable!, out var variable) || variable?.RawValueJson is null)
        {
            if (allowNull)
            {
                return SucceedNull(started, context, outputVariable!, targetEntity!, castMode);
            }

            return Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, $"Cast source variable '{sourceVariable}' was not found.", context);
        }

        var source = MicroflowVariableStore.ToJsonElement(variable.RawValueJson);
        if (source is null || source.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (allowNull)
            {
                return SucceedNull(started, context, outputVariable!, targetEntity!, castMode);
            }

            return Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Cast source variable '{sourceVariable}' is null.", context);
        }

        var sourceEntity = ReadString(source.Value, "entityType")
            ?? ReadString(source.Value, "entityQualifiedName")
            ?? ReadString(source.Value, "$entityType")
            ?? ReadString(source.Value, "__entityType")
            ?? TryReadEntityFromDataType(variable.DataTypeJson);
        var valid = IsAssignable(sourceEntity, targetEntity!, context.MetadataCatalog);
        if (!valid && failOnInvalidType)
        {
            return Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Cannot cast '{sourceEntity ?? "unknown"}' to '{targetEntity}'.", context);
        }

        if (!valid)
        {
            return SucceedNull(started, context, outputVariable!, targetEntity!, castMode, invalidType: sourceEntity);
        }

        if (context.EntityAccessService is not null)
        {
            var access = await context.EntityAccessService.CanReadAsync(
                context.RuntimeSecurityContext,
                new MicroflowResolvedEntity { Found = true, QualifiedName = targetEntity! },
                ct);
            if (!access.Allowed)
            {
                return Failed(started, RuntimeErrorCode.RuntimeEntityAccessDenied, access.Reason, context);
            }
        }

        SetObjectVariable(context, outputVariable!, targetEntity!, source.Value, sourceVariable!);
        var output = JsonSerializer.SerializeToElement(new
        {
            sourceVariable,
            outputVariable,
            sourceEntity,
            targetEntity,
            castMode,
            result = "matched"
        }, JsonOptions);
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = $"{sourceVariable} as {targetEntity}",
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private static MicroflowActionExecutionResult SucceedNull(
        Stopwatch started,
        MicroflowActionExecutionContext context,
        string outputVariable,
        string targetEntity,
        string castMode,
        string? invalidType = null)
    {
        var value = JsonSerializer.SerializeToElement<object?>(null, JsonOptions);
        SetObjectVariable(context, outputVariable, targetEntity, value, sourceVariable: null);
        var output = JsonSerializer.SerializeToElement(new
        {
            outputVariable,
            targetEntity,
            castMode,
            result = invalidType is null ? "null" : "invalid-null",
            invalidType
        }, JsonOptions);
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = $"{outputVariable}=null",
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private static void SetObjectVariable(
        MicroflowActionExecutionContext context,
        string name,
        string targetEntity,
        JsonElement value,
        string? sourceVariable)
    {
        var dataTypeJson = JsonSerializer.Serialize(new { kind = "object", entityQualifiedName = targetEntity }, JsonOptions);
        var raw = value.GetRawText();
        var variable = new MicroflowRuntimeVariableValue
        {
            Name = name,
            DataTypeJson = dataTypeJson,
            RawValueJson = raw,
            ValuePreview = value.ValueKind == JsonValueKind.Null ? "null" : MicroflowVariableStore.Preview(raw),
            SourceKind = MicroflowVariableSourceKind.ActionOutput,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action
        };
        if (context.VariableStore.Exists(name))
        {
            context.VariableStore.Set(name, variable);
        }
        else
        {
            context.VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = name,
                DataTypeJson = dataTypeJson,
                RawValueJson = raw,
                ValuePreview = variable.ValuePreview,
                SourceKind = MicroflowVariableSourceKind.ActionOutput,
                SourceObjectId = context.ObjectId,
                SourceActionId = context.ActionId,
                ScopeKind = MicroflowVariableScopeKind.Action,
                Value = variable,
                AllowShadowing = !string.Equals(name, sourceVariable, StringComparison.Ordinal)
            });
        }
    }

    private static bool IsAssignable(string? sourceEntity, string targetEntity, MicroflowMetadataCatalogDto? catalog)
    {
        if (string.IsNullOrWhiteSpace(sourceEntity))
        {
            return false;
        }

        if (string.Equals(sourceEntity, targetEntity, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entities = catalog?.Entities ?? Array.Empty<MetadataEntityDto>();
        var byName = entities.ToDictionary(entity => entity.QualifiedName, StringComparer.OrdinalIgnoreCase);
        var current = sourceEntity;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            if (!byName.TryGetValue(current, out var entity))
            {
                break;
            }

            var parent = entity.Generalization;
            if (string.Equals(parent, targetEntity, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = parent;
        }

        if (byName.TryGetValue(targetEntity, out var target)
            && target.Specializations.Any(item => string.Equals(item, sourceEntity, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string? TryReadEntityFromDataType(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(dataTypeJson);
            return ReadString(doc.RootElement, "entityQualifiedName")
                ?? ReadString(doc.RootElement, "entityType");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool defaultValue = false)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
            ? value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed)
            : defaultValue;

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
