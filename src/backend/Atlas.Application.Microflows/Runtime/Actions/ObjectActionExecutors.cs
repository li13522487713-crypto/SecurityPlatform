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
