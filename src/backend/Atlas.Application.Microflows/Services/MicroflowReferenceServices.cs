using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowReferenceIndexer : IMicroflowReferenceIndexer
{
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowReferenceRepository _referenceRepository;
    private readonly IMicroflowClock _clock;

    public MicroflowReferenceIndexer(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowReferenceRepository referenceRepository,
        IMicroflowClock clock)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _referenceRepository = referenceRepository;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MicroflowReferenceEntity>> RebuildReferencesForMicroflowAsync(
        string resourceId,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        var snapshot = !string.IsNullOrWhiteSpace(resource.CurrentSchemaSnapshotId)
            ? await _schemaSnapshotRepository.GetByIdAsync(resource.CurrentSchemaSnapshotId, cancellationToken)
            : await _schemaSnapshotRepository.GetLatestByResourceIdAsync(resource.Id, cancellationToken);

        var references = Array.Empty<MicroflowReferenceEntity>() as IReadOnlyList<MicroflowReferenceEntity>;
        var schema = MicroflowResourceMapper.ParseSchemaJson(snapshot?.SchemaJson);
        if (schema.HasValue)
        {
            references = ExtractReferencesFromSchema(resource, schema.Value);
        }

        var previousTargetIds = (await _referenceRepository.ListBySourceAsync("microflow", resource.Id, cancellationToken))
            .Select(static reference => reference.TargetMicroflowId)
            .ToArray();
        await _referenceRepository.DeleteBySourceAsync("microflow", resource.Id, cancellationToken);
        await _referenceRepository.DeleteBySourceAsync("api", resource.Id, cancellationToken);
        await _referenceRepository.InsertManyAsync(references, cancellationToken);
        await RefreshTargetReferenceCountsAsync(
            previousTargetIds.Concat(references.Select(static reference => reference.TargetMicroflowId)).ToArray(),
            cancellationToken);
        return references;
    }

    public IReadOnlyList<MicroflowReferenceEntity> ExtractReferencesFromSchema(MicroflowResourceEntity sourceResource, JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<MicroflowReferenceEntity>();
        }

        var now = _clock.UtcNow;
        var references = new List<MicroflowReferenceEntity>();
        foreach (var action in EnumerateActions(schema).Where(action => string.Equals(ReadString(action, "kind"), "callMicroflow", StringComparison.OrdinalIgnoreCase)))
        {
            var targetId = ReadString(action, "targetMicroflowId");
            if (string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            references.Add(CreateReference(sourceResource, targetId, "microflow", "callMicroflow", "medium", $"Microflow call from {BuildSourcePath(sourceResource)} to {targetId}", now));
        }

        if (ReadBoolByPath(schema, "exposure", "url", "enabled") || !string.IsNullOrWhiteSpace(ReadStringByPath(schema, "exposure", "url", "path")))
        {
            references.Add(CreateReference(sourceResource, sourceResource.Id, "api", "apiExposure", "low", $"API exposure for {BuildSourcePath(sourceResource)}", now));
        }

        return references
            .GroupBy(r => $"{r.SourceType}|{r.SourceId}|{r.TargetMicroflowId}|{r.ReferenceKind}", StringComparer.Ordinal)
            .Select(g => g.First())
            .ToArray();
    }

    private static MicroflowReferenceEntity CreateReference(
        MicroflowResourceEntity source,
        string targetMicroflowId,
        string sourceType,
        string referenceKind,
        string impactLevel,
        string description,
        DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TargetMicroflowId = targetMicroflowId,
            WorkspaceId = source.WorkspaceId,
            TenantId = source.TenantId,
            SourceType = sourceType,
            SourceId = source.Id,
            SourceName = string.IsNullOrWhiteSpace(source.DisplayName) ? source.Name : source.DisplayName,
            SourcePath = BuildSourcePath(source),
            SourceVersion = source.Version,
            ReferenceKind = referenceKind,
            ImpactLevel = impactLevel,
            Description = description,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static IEnumerable<JsonElement> EnumerateActions(JsonElement schema)
    {
        if (!schema.TryGetProperty("objectCollection", out var collection))
        {
            yield break;
        }

        foreach (var action in EnumerateActionsFromCollection(collection))
        {
            yield return action;
        }
    }

    private static IEnumerable<JsonElement> EnumerateActionsFromCollection(JsonElement collection)
    {
        if (!collection.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var obj in objects.EnumerateArray())
        {
            if (obj.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.Object)
            {
                yield return action;
            }

            if (obj.TryGetProperty("objectCollection", out var nested)
                || obj.TryGetProperty("containedObjectCollection", out nested)
                || obj.TryGetProperty("loopObjectCollection", out nested))
            {
                foreach (var nestedAction in EnumerateActionsFromCollection(nested))
                {
                    yield return nestedAction;
                }
            }
        }
    }

    private static string BuildSourcePath(MicroflowResourceEntity source)
        => $"{(string.IsNullOrWhiteSpace(source.ModuleName) ? source.ModuleId : source.ModuleName)}.{source.Name}";

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private async Task RefreshTargetReferenceCountsAsync(IReadOnlyList<string> targetMicroflowIds, CancellationToken cancellationToken)
    {
        var ids = targetMicroflowIds.Where(static id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var counts = await _referenceRepository.CountByTargetMicroflowIdsAsync(
            ids,
            new MicroflowReferenceQuery { IncludeInactive = false },
            cancellationToken);
        await _resourceRepository.UpdateReferenceCountsAsync(
            ids.ToDictionary(id => id, id => counts.TryGetValue(id, out var count) ? count : 0, StringComparer.Ordinal),
            cancellationToken);
    }

    private static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
    }

    private static bool ReadBoolByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return false;
            }
        }

        return current.ValueKind == JsonValueKind.True;
    }
}

public sealed class MicroflowReferenceService : IMicroflowReferenceService
{
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowReferenceRepository _referenceRepository;
    private readonly IMicroflowReferenceIndexer _referenceIndexer;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowReferenceService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowReferenceRepository referenceRepository,
        IMicroflowReferenceIndexer referenceIndexer,
        IMicroflowRequestContextAccessor requestContextAccessor)
    {
        _resourceRepository = resourceRepository;
        _referenceRepository = referenceRepository;
        _referenceIndexer = referenceIndexer;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<IReadOnlyList<MicroflowReferenceDto>> GetReferencesAsync(
        string targetMicroflowId,
        GetMicroflowReferencesRequestDto request,
        CancellationToken cancellationToken)
    {
        _ = await LoadResourceAsync(targetMicroflowId, cancellationToken);
        var references = await _referenceRepository.ListByTargetMicroflowIdAsync(
            targetMicroflowId,
            ToQuery(request),
            cancellationToken);
        return references.Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<MicroflowReferenceDto>> RebuildReferencesAsync(string resourceId, CancellationToken cancellationToken)
    {
        var references = await _referenceIndexer.RebuildReferencesForMicroflowAsync(resourceId, cancellationToken);
        return references.Select(ToDto).ToArray();
    }

    public async Task<int> RebuildAllReferencesAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        var resources = await _resourceRepository.ListAsync(
            new MicroflowResourceQueryDto
            {
                WorkspaceId = workspaceId ?? _requestContextAccessor.Current.WorkspaceId,
                PageIndex = 1,
                PageSize = 1000
            },
            cancellationToken);

        var count = 0;
        foreach (var resource in resources)
        {
            var references = await _referenceIndexer.RebuildReferencesForMicroflowAsync(resource.Id, cancellationToken);
            count += references.Count;
        }

        return count;
    }

    public static MicroflowReferenceDto ToDto(MicroflowReferenceEntity entity)
        => new()
        {
            Id = entity.Id,
            TargetMicroflowId = entity.TargetMicroflowId,
            SourceType = entity.SourceType,
            SourceId = entity.SourceId,
            SourceName = entity.SourceName,
            SourcePath = entity.SourcePath,
            SourceVersion = entity.SourceVersion,
            ReferencedVersion = entity.ReferencedVersion,
            ReferenceKind = entity.ReferenceKind,
            ImpactLevel = entity.ImpactLevel,
            Description = entity.Description,
            Active = entity.Active,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CanNavigate = string.Equals(entity.SourceType, "microflow", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entity.SourceId)
        };

    public static MicroflowReferenceQuery ToQuery(GetMicroflowReferencesRequestDto request)
        => new()
        {
            IncludeInactive = request.IncludeInactive,
            SourceType = request.SourceType,
            ImpactLevel = request.ImpactLevel
        };

    private async Task<MicroflowResourceEntity> LoadResourceAsync(string id, CancellationToken cancellationToken)
        => await _resourceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
}
