using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class SchemaPublishSnapshotQueryService : ISchemaPublishSnapshotQueryService
{
    private readonly ISchemaPublishSnapshotRepository _repository;

    public SchemaPublishSnapshotQueryService(ISchemaPublishSnapshotRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<SchemaPublishSnapshotListItem>> QueryAsync(
        TenantId tenantId,
        string? tableKey,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.QueryPageAsync(
            tenantId, tableKey, request.PageIndex, request.PageSize, cancellationToken);

        var listItems = items.Select(s => new SchemaPublishSnapshotListItem(
            s.Id,
            s.TableKey,
            s.Version,
            s.PublishNote,
            s.PublishedBy,
            s.PublishedAt,
            s.MigrationTaskId)).ToList();

        return new PagedResult<SchemaPublishSnapshotListItem>(
            listItems, total, request.PageIndex, request.PageSize);
    }

    public async Task<SchemaPublishSnapshotDetail?> GetByIdAsync(
        TenantId tenantId,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, snapshotId, cancellationToken);
        return entity is null ? null : MapToDetail(entity);
    }

    public async Task<SchemaPublishSnapshotDetail?> GetLatestAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindLatestByTableAsync(tenantId, tableKey, cancellationToken);
        return entity is null ? null : MapToDetail(entity);
    }

    public async Task<SchemaSnapshotDiffResult?> DiffAsync(
        TenantId tenantId,
        string tableKey,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken)
    {
        var fromSnapshot = await _repository.FindByVersionAsync(tenantId, tableKey, fromVersion, cancellationToken);
        var toSnapshot = await _repository.FindByVersionAsync(tenantId, tableKey, toVersion, cancellationToken);

        if (fromSnapshot is null || toSnapshot is null)
        {
            return null;
        }

        var fromFields = DeserializeFields(fromSnapshot.SnapshotJson);
        var toFields = DeserializeFields(toSnapshot.SnapshotJson);

        var fieldDiffs = ComputeFieldDiffs(fromFields, toFields);
        var indexDiffs = ComputeIndexDiffs(
            DeserializeIndexes(fromSnapshot.SnapshotJson),
            DeserializeIndexes(toSnapshot.SnapshotJson));

        return new SchemaSnapshotDiffResult(fromVersion, toVersion, tableKey, fieldDiffs, indexDiffs);
    }

    private static SchemaPublishSnapshotDetail MapToDetail(Domain.DynamicTables.Entities.SchemaPublishSnapshot entity)
    {
        return new SchemaPublishSnapshotDetail(
            entity.Id,
            entity.TableId,
            entity.TableKey,
            entity.Version,
            entity.SnapshotJson,
            entity.PublishNote,
            entity.PublishedBy,
            entity.PublishedAt,
            entity.MigrationTaskId);
    }

    private static Dictionary<string, JsonElement> DeserializeFields(string snapshotJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (doc.RootElement.TryGetProperty("fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Array)
            {
                var dict = new Dictionary<string, JsonElement>();
                foreach (var f in fieldsEl.EnumerateArray())
                {
                    if (f.TryGetProperty("name", out var nameEl))
                    {
                        dict[nameEl.GetString() ?? ""] = f;
                    }
                }
                return dict;
            }
        }
        catch
        {
            // 快照 JSON 格式不合法时返回空集
        }
        return new Dictionary<string, JsonElement>();
    }

    private static Dictionary<string, JsonElement> DeserializeIndexes(string snapshotJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (doc.RootElement.TryGetProperty("indexes", out var indexesEl) && indexesEl.ValueKind == JsonValueKind.Array)
            {
                var dict = new Dictionary<string, JsonElement>();
                foreach (var idx in indexesEl.EnumerateArray())
                {
                    if (idx.TryGetProperty("name", out var nameEl))
                    {
                        dict[nameEl.GetString() ?? ""] = idx;
                    }
                }
                return dict;
            }
        }
        catch
        {
            // ignore
        }
        return new Dictionary<string, JsonElement>();
    }

    private static List<SchemaFieldDiff> ComputeFieldDiffs(
        Dictionary<string, JsonElement> fromFields,
        Dictionary<string, JsonElement> toFields)
    {
        var diffs = new List<SchemaFieldDiff>();

        foreach (var (name, toEl) in toFields)
        {
            if (!fromFields.TryGetValue(name, out var fromEl))
            {
                diffs.Add(new SchemaFieldDiff(name, "Added", null, toEl.GetRawText()));
            }
            else if (fromEl.GetRawText() != toEl.GetRawText())
            {
                diffs.Add(new SchemaFieldDiff(name, "Modified", fromEl.GetRawText(), toEl.GetRawText()));
            }
        }

        foreach (var name in fromFields.Keys)
        {
            if (!toFields.ContainsKey(name))
            {
                diffs.Add(new SchemaFieldDiff(name, "Removed", fromFields[name].GetRawText(), null));
            }
        }

        return diffs;
    }

    private static List<SchemaIndexDiff> ComputeIndexDiffs(
        Dictionary<string, JsonElement> fromIndexes,
        Dictionary<string, JsonElement> toIndexes)
    {
        var diffs = new List<SchemaIndexDiff>();

        foreach (var (name, toEl) in toIndexes)
        {
            if (!fromIndexes.TryGetValue(name, out var fromEl))
            {
                diffs.Add(new SchemaIndexDiff(name, "Added", null, toEl.GetRawText()));
            }
            else if (fromEl.GetRawText() != toEl.GetRawText())
            {
                diffs.Add(new SchemaIndexDiff(name, "Modified", fromEl.GetRawText(), toEl.GetRawText()));
            }
        }

        foreach (var name in fromIndexes.Keys)
        {
            if (!toIndexes.ContainsKey(name))
            {
                diffs.Add(new SchemaIndexDiff(name, "Removed", fromIndexes[name].GetRawText(), null));
            }
        }

        return diffs;
    }
}
