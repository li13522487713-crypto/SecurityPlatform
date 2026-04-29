using System.Collections.Concurrent;
using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Objects;

public sealed class InMemoryRuntimeObjectStore : IMicroflowRuntimeObjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, JsonElement> _objects = new(StringComparer.Ordinal);

    public Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var items = _objects
            .Where(pair => pair.Key.StartsWith(KeyPrefix(query), StringComparison.Ordinal))
            .Take(Math.Clamp(query.Limit, 1, 1000))
            .Select(pair => pair.Value.Clone())
            .ToArray();
        return Task.FromResult(new MicroflowRuntimeObjectStoreResult
        {
            Success = true,
            Items = items,
            Value = JsonSerializer.SerializeToElement(new { items }, JsonOptions)
        });
    }

    public Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var objectId = string.IsNullOrWhiteSpace(mutation.ObjectId) ? Guid.NewGuid().ToString("N") : mutation.ObjectId!;
        var value = mutation.Value ?? JsonSerializer.SerializeToElement(new { id = objectId, entityType = mutation.EntityType }, JsonOptions);
        var key = Key(mutation, objectId);
        _objects[key] = value.Clone();
        return Task.FromResult(Success(value, $"Object '{objectId}' created."));
    }

    public Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(mutation.ObjectId))
        {
            return Task.FromResult(Failed(RuntimeErrorCode.RuntimeObjectNotFound, "Object id is required."));
        }

        var key = Key(mutation, mutation.ObjectId!);
        if (!_objects.ContainsKey(key))
        {
            return Task.FromResult(Failed(RuntimeErrorCode.RuntimeObjectNotFound, $"Object '{mutation.ObjectId}' was not found."));
        }

        var value = mutation.Value ?? _objects[key];
        _objects[key] = value.Clone();
        return Task.FromResult(Success(value, $"Object '{mutation.ObjectId}' changed."));
    }

    public Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new MicroflowRuntimeObjectStoreResult
        {
            Success = true,
            Value = mutation.Value,
            Message = mutation.DryRun ? "Dry-run commit accepted without persistent write." : "Commit accepted."
        });
    }

    public Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(mutation.ObjectId))
        {
            return Task.FromResult(Failed(RuntimeErrorCode.RuntimeObjectNotFound, "Object id is required."));
        }

        var key = Key(mutation, mutation.ObjectId!);
        if (!_objects.TryGetValue(key, out var current))
        {
            return Task.FromResult(Failed(RuntimeErrorCode.RuntimeObjectNotFound, $"Object '{mutation.ObjectId}' was not found."));
        }

        if (mutation.Value.HasValue)
        {
            _objects[key] = mutation.Value.Value.Clone();
            return Task.FromResult(Success(mutation.Value.Value, $"Object '{mutation.ObjectId}' reverted."));
        }

        _objects.TryRemove(key, out _);
        return Task.FromResult(Success(current, $"Object '{mutation.ObjectId}' invalidated."));
    }

    public Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(mutation.ObjectId))
        {
            return Task.FromResult(Failed(RuntimeErrorCode.RuntimeObjectNotFound, "Object id is required."));
        }

        var key = Key(mutation, mutation.ObjectId!);
        var removed = _objects.TryRemove(key, out var value);
        return Task.FromResult(removed
            ? Success(value, $"Object '{mutation.ObjectId}' deleted.")
            : Failed(RuntimeErrorCode.RuntimeObjectNotFound, $"Object '{mutation.ObjectId}' was not found."));
    }

    private static MicroflowRuntimeObjectStoreResult Success(JsonElement? value, string message)
        => new()
        {
            Success = true,
            Value = value,
            Message = message
        };

    private static MicroflowRuntimeObjectStoreResult Failed(string code, string message)
        => new()
        {
            Success = false,
            Code = code,
            Message = message
        };

    private static string KeyPrefix(MicroflowRuntimeObjectQuery query)
        => $"{query.TenantId ?? "tenant"}:{query.WorkspaceId ?? "workspace"}:{query.EntityType}:";

    private static string Key(MicroflowRuntimeObjectMutation mutation, string objectId)
        => $"{mutation.TenantId ?? "tenant"}:{mutation.WorkspaceId ?? "workspace"}:{mutation.EntityType}:{objectId}";
}
