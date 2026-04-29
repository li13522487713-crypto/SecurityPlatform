using System.Text.Json;

namespace Atlas.Application.Microflows.Runtime.Objects;

public interface IMicroflowRuntimeObjectStore
{
    Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct);

    Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct);

    Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct);

    Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct);

    Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct);

    Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct);
}

public sealed record MicroflowRuntimeObjectQuery
{
    public string EntityType { get; init; } = string.Empty;
    public string? ObjectId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? TenantId { get; init; }
    public int Limit { get; init; } = 100;
}

public sealed record MicroflowRuntimeObjectMutation
{
    public string EntityType { get; init; } = string.Empty;
    public string? ObjectId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? TenantId { get; init; }
    public JsonElement? Value { get; init; }
    public bool DryRun { get; init; } = true;
}

public sealed record MicroflowRuntimeObjectStoreResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = "SUCCESS";
    public string Message { get; init; } = "OK";
    public JsonElement? Value { get; init; }
    public IReadOnlyList<JsonElement> Items { get; init; } = Array.Empty<JsonElement>();
}
