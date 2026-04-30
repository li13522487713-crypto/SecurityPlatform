using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Domain.Microflows.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class SqlSugarMicroflowRuntimeObjectStore : IDatabaseBackedMicroflowRuntimeObjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqlSugarClient _db;

    public SqlSugarMicroflowRuntimeObjectStore(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = ResolveDb(query.RuntimeContext);
        var transactionId = query.RuntimeContext?.CurrentTransactionId;
        var items = await db.Queryable<MicroflowRuntimeObjectStateEntity>()
            .Where(item => item.EntityType == query.EntityType)
            .WhereIF(!string.IsNullOrWhiteSpace(query.WorkspaceId), item => item.WorkspaceId == query.WorkspaceId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.TenantId), item => item.TenantId == query.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.ObjectId), item => item.ObjectId == query.ObjectId)
            .Where(item => item.State == MicroflowRuntimeObjectChangeStatus.Committed
                || (transactionId != null && item.TransactionId == transactionId && item.State == MicroflowRuntimeObjectChangeStatus.Staged))
            .OrderByDescending(item => item.UpdatedAt)
            .Take(Math.Clamp(query.Limit, 1, 1000))
            .ToListAsync(ct);

        var payloads = items
            .Where(item => !string.Equals(item.State, MicroflowRuntimeObjectChangeOperation.Delete, StringComparison.OrdinalIgnoreCase))
            .Select(item => Parse(item.PayloadJson))
            .ToArray();
        return new MicroflowRuntimeObjectStoreResult
        {
            Success = true,
            Items = payloads,
            Value = JsonSerializer.SerializeToElement(new { items = payloads }, JsonOptions),
            Message = $"Retrieved {payloads.Length} object(s)."
        };
    }

    public async Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (mutation.RuntimeContext?.DatabaseSession is null)
        {
            return Failed(RuntimeErrorCode.RuntimeTransactionRequired, "CreateObject requires an active runtime transaction.");
        }

        var db = ResolveDb(mutation.RuntimeContext);
        var objectId = string.IsNullOrWhiteSpace(mutation.ObjectId) ? Guid.NewGuid().ToString("N") : mutation.ObjectId!;
        var payload = mutation.Value ?? JsonSerializer.SerializeToElement(new { id = objectId, entityType = mutation.EntityType }, JsonOptions);
        var entity = new MicroflowRuntimeObjectStateEntity
        {
            RunId = mutation.RuntimeContext?.RunId ?? "runtime",
            RootRunId = mutation.RuntimeContext?.RootRunId,
            ParentRunId = mutation.RuntimeContext?.ParentRunId,
            TransactionId = mutation.RuntimeContext?.CurrentTransactionId,
            WorkspaceId = mutation.WorkspaceId,
            TenantId = mutation.TenantId,
            EntityType = mutation.EntityType,
            ObjectId = objectId,
            State = mutation.RuntimeContext?.CurrentTransactionId is null ? MicroflowRuntimeObjectChangeStatus.Committed : MicroflowRuntimeObjectChangeStatus.Staged,
            PayloadJson = payload.GetRawText(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await db.Insertable(entity).ExecuteCommandAsync(ct);
        return Success(payload, $"Object '{objectId}' created.");
    }

    public async Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (mutation.RuntimeContext?.DatabaseSession is null)
        {
            return Failed(RuntimeErrorCode.RuntimeTransactionRequired, "ChangeMembers requires an active runtime transaction.");
        }

        if (string.IsNullOrWhiteSpace(mutation.ObjectId))
        {
            return Failed(RuntimeErrorCode.RuntimeObjectNotFound, "Object id is required.");
        }

        var db = ResolveDb(mutation.RuntimeContext);
        var latest = await FindLatestAsync(db, mutation, ct);
        if (latest is null)
        {
            return Failed(RuntimeErrorCode.RuntimeObjectNotFound, $"Object '{mutation.ObjectId}' was not found.");
        }

        latest.BeforePayloadJson ??= latest.PayloadJson;
        latest.PayloadJson = (mutation.Value ?? Parse(latest.PayloadJson)).GetRawText();
        latest.TransactionId = mutation.RuntimeContext?.CurrentTransactionId ?? latest.TransactionId;
        latest.State = mutation.RuntimeContext?.CurrentTransactionId is null ? MicroflowRuntimeObjectChangeStatus.Committed : MicroflowRuntimeObjectChangeStatus.Staged;
        latest.UpdatedAt = DateTimeOffset.UtcNow;
        await db.Updateable(latest).ExecuteCommandAsync(ct);
        return Success(Parse(latest.PayloadJson), $"Object '{mutation.ObjectId}' changed.");
    }

    public Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => Task.FromResult(mutation.RuntimeContext?.DatabaseSession is null
            ? Failed(RuntimeErrorCode.RuntimeTransactionRequired, "Commit requires an active runtime transaction.")
            : new MicroflowRuntimeObjectStoreResult
            {
                Success = true,
                Value = mutation.Value,
                Message = mutation.DryRun ? "Dry-run commit accepted." : "Commit accepted."
            });

    public async Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (mutation.RuntimeContext?.DatabaseSession is null)
        {
            return Failed(RuntimeErrorCode.RuntimeTransactionRequired, "Delete requires an active runtime transaction.");
        }

        if (string.IsNullOrWhiteSpace(mutation.ObjectId))
        {
            return Failed(RuntimeErrorCode.RuntimeObjectNotFound, "Object id is required.");
        }

        var db = ResolveDb(mutation.RuntimeContext);
        var latest = await FindLatestAsync(db, mutation, ct);
        if (latest is null)
        {
            return Failed(RuntimeErrorCode.RuntimeObjectNotFound, $"Object '{mutation.ObjectId}' was not found.");
        }

        latest.BeforePayloadJson ??= latest.PayloadJson;
        latest.TransactionId = mutation.RuntimeContext?.CurrentTransactionId ?? latest.TransactionId;
        latest.State = MicroflowRuntimeObjectChangeOperation.Delete;
        latest.UpdatedAt = DateTimeOffset.UtcNow;
        await db.Updateable(latest).ExecuteCommandAsync(ct);
        return Success(Parse(latest.PayloadJson), $"Object '{mutation.ObjectId}' deleted.");
    }

    public async Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (mutation.RuntimeContext?.DatabaseSession is null)
        {
            return Failed(RuntimeErrorCode.RuntimeTransactionRequired, "Rollback requires an active runtime transaction.");
        }

        if (string.IsNullOrWhiteSpace(mutation.ObjectId))
        {
            return Failed(RuntimeErrorCode.RuntimeObjectNotFound, "Object id is required.");
        }

        var db = ResolveDb(mutation.RuntimeContext);
        var latest = await FindLatestAsync(db, mutation, ct);
        if (latest is null)
        {
            return Failed(RuntimeErrorCode.RuntimeObjectNotFound, $"Object '{mutation.ObjectId}' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(latest.BeforePayloadJson))
        {
            latest.PayloadJson = latest.BeforePayloadJson;
        }

        latest.State = MicroflowRuntimeObjectChangeStatus.RolledBack;
        latest.UpdatedAt = DateTimeOffset.UtcNow;
        await db.Updateable(latest).ExecuteCommandAsync(ct);
        return Success(Parse(latest.PayloadJson), $"Object '{mutation.ObjectId}' reverted.");
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

    private static JsonElement Parse(string payloadJson)
        => JsonSerializer.Deserialize<JsonElement>(payloadJson);

    private ISqlSugarClient ResolveDb(RuntimeExecutionContext? runtimeContext)
        => runtimeContext?.DatabaseSession?.GetNativeSession<ISqlSugarClient>() ?? _db;

    private static async Task<MicroflowRuntimeObjectStateEntity?> FindLatestAsync(
        ISqlSugarClient db,
        MicroflowRuntimeObjectMutation mutation,
        CancellationToken ct)
        => await db.Queryable<MicroflowRuntimeObjectStateEntity>()
            .Where(item => item.EntityType == mutation.EntityType && item.ObjectId == mutation.ObjectId)
            .WhereIF(!string.IsNullOrWhiteSpace(mutation.WorkspaceId), item => item.WorkspaceId == mutation.WorkspaceId)
            .WhereIF(!string.IsNullOrWhiteSpace(mutation.TenantId), item => item.TenantId == mutation.TenantId)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstAsync(ct);
}
