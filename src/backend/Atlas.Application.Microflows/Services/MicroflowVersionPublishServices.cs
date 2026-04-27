using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowVersionDiffService : IMicroflowVersionDiffService
{
    public MicroflowVersionDiffDto Compare(JsonElement beforeSchema, JsonElement afterSchema)
    {
        var beforeParameters = ReadParameters(beforeSchema);
        var afterParameters = ReadParameters(afterSchema);
        var beforeRequired = ReadParameterRequiredFlags(beforeSchema);
        var afterRequired = ReadParameterRequiredFlags(afterSchema);
        var removedParameters = beforeParameters.Keys.Except(afterParameters.Keys).Order().ToArray();
        var addedParameters = afterParameters.Keys.Except(beforeParameters.Keys).Order().ToArray();
        var changedParameters = beforeParameters.Keys.Intersect(afterParameters.Keys)
            .Where(name => !string.Equals(beforeParameters[name], afterParameters[name], StringComparison.Ordinal))
            .Order()
            .Select(name => new MicroflowChangedParameterDto(name, beforeParameters[name], afterParameters[name]))
            .ToArray();
        var requiredChangedParameters = beforeRequired.Keys.Intersect(afterRequired.Keys)
            .Where(name => beforeRequired[name] != afterRequired[name])
            .Order()
            .ToArray();

        var beforeReturnType = ReadCompactJson(beforeSchema, "returnType");
        var afterReturnType = ReadCompactJson(afterSchema, "returnType");
        var returnTypeChanged = !string.Equals(beforeReturnType, afterReturnType, StringComparison.Ordinal)
            ? new MicroflowReturnTypeChangedDto(beforeReturnType, afterReturnType)
            : null;

        var beforeObjects = ReadObjects(beforeSchema);
        var afterObjects = ReadObjects(afterSchema);
        var removedObjects = beforeObjects.Keys.Except(afterObjects.Keys).Order().ToArray();
        var addedObjects = afterObjects.Keys.Except(beforeObjects.Keys).Order().ToArray();
        var changedObjects = beforeObjects.Keys.Intersect(afterObjects.Keys)
            .Where(id => !string.Equals(beforeObjects[id], afterObjects[id], StringComparison.Ordinal))
            .Order()
            .ToArray();

        var beforeFlows = ReadFlows(beforeSchema);
        var afterFlows = ReadFlows(afterSchema);
        var removedFlows = beforeFlows.Keys.Except(afterFlows.Keys).Order().ToArray();
        var addedFlows = afterFlows.Keys.Except(beforeFlows.Keys).Order().ToArray();

        var breakingChanges = new List<MicroflowBreakingChangeDto>();
        breakingChanges.AddRange(removedParameters.Select(name => Breaking("high", "PARAMETER_REMOVED", $"参数 {name} 已删除。", $"parameters.{name}", name, null)));
        breakingChanges.AddRange(changedParameters.Select(change => Breaking("high", "PARAMETER_TYPE_CHANGED", $"参数 {change.Name} 类型已变更。", $"parameters.{change.Name}", change.BeforeType, change.AfterType)));
        if (returnTypeChanged is not null)
        {
            breakingChanges.Add(Breaking("high", "RETURN_TYPE_CHANGED", "返回类型已变更。", "returnType", returnTypeChanged.BeforeType, returnTypeChanged.AfterType));
        }

        var beforePath = ReadStringByPath(beforeSchema, "exposure", "url", "path");
        var afterPath = ReadStringByPath(afterSchema, "exposure", "url", "path");
        if (!string.Equals(beforePath, afterPath, StringComparison.Ordinal))
        {
            breakingChanges.Add(Breaking("medium", "EXPOSED_URL_CHANGED", "对外暴露 URL 路径已变更。", "exposure.url.path", beforePath, afterPath));
        }

        AddExposureDisabledChange(breakingChanges, beforeSchema, afterSchema, "asMicroflowAction", "MICROFLOW_ACTION_EXPOSURE_DISABLED", "微流 Action 暴露已关闭。", "high");
        AddExposureDisabledChange(breakingChanges, beforeSchema, afterSchema, "asWorkflowAction", "WORKFLOW_ACTION_EXPOSURE_DISABLED", "工作流 Action 暴露已关闭。", "medium");
        breakingChanges.AddRange(requiredChangedParameters.Select(name => Breaking("medium", "PARAMETER_REQUIRED_CHANGED", $"参数 {name} 必填属性已变更。", $"parameters.{name}.required", beforeRequired[name].ToString(), afterRequired[name].ToString())));
        breakingChanges.AddRange(removedObjects.Select(id => Breaking("low", "PUBLISHED_NODE_REMOVED", $"对象 {id} 已删除。", $"objectCollection.objects.{id}", id, null)));
        breakingChanges.AddRange(removedFlows.Select(id => Breaking("low", "FLOW_REMOVED", $"连线 {id} 已删除。", $"flows.{id}", id, null)));

        return new MicroflowVersionDiffDto
        {
            AddedParameters = addedParameters,
            RemovedParameters = removedParameters,
            ChangedParameters = changedParameters,
            ReturnTypeChanged = returnTypeChanged,
            AddedObjects = addedObjects,
            RemovedObjects = removedObjects,
            ChangedObjects = changedObjects,
            AddedFlows = addedFlows,
            RemovedFlows = removedFlows,
            BreakingChanges = breakingChanges
        };
    }

    private static Dictionary<string, string> ReadParameters(JsonElement schema)
    {
        if (!schema.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return parameters.EnumerateArray()
            .Select(p => new { Name = ReadString(p, "name"), DataType = ReadDataType(p) })
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().DataType, StringComparer.Ordinal);
    }

    private static Dictionary<string, bool> ReadParameterRequiredFlags(JsonElement schema)
    {
        if (!schema.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        return parameters.EnumerateArray()
            .Select(p => new { Name = ReadString(p, "name"), Required = ReadBool(p, "required") })
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Required, StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ReadObjects(JsonElement schema)
    {
        if (!schema.TryGetProperty("objectCollection", out var collection)
            || !collection.TryGetProperty("objects", out var objects)
            || objects.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return objects.EnumerateArray()
            .Select(o => new { Id = ReadString(o, "id"), Shape = $"{ReadString(o, "kind")}|{ReadString(o, "caption")}|{ReadStringByPath(o, "action", "kind")}" })
            .Where(o => !string.IsNullOrWhiteSpace(o.Id))
            .GroupBy(o => o.Id!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Shape, StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ReadFlows(JsonElement schema)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        AddFlows(result, schema.TryGetProperty("flows", out var rootFlows) ? rootFlows : default);
        if (schema.TryGetProperty("objectCollection", out var collection))
        {
            AddFlowsFromCollection(result, collection);
        }

        return result;
    }

    private static void AddFlowsFromCollection(Dictionary<string, string> result, JsonElement collection)
    {
        if (collection.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        AddFlows(result, collection.TryGetProperty("flows", out var flows) ? flows : default);
        if (!collection.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var obj in objects.EnumerateArray())
        {
            if (obj.TryGetProperty("objectCollection", out var nested)
                || obj.TryGetProperty("containedObjectCollection", out nested)
                || obj.TryGetProperty("loopObjectCollection", out nested))
            {
                AddFlowsFromCollection(result, nested);
            }
        }
    }

    private static void AddFlows(Dictionary<string, string> result, JsonElement flows)
    {
        if (flows.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var flow in flows.EnumerateArray()
            .Select(f => new
            {
                Id = ReadString(f, "id"),
                Shape = $"{ReadString(f, "originObjectId")}|{ReadString(f, "destinationObjectId")}|{ReadString(f, "edgeKind")}|{ReadCompactJson(f, "caseValues")}|{ReadBool(f, "isErrorHandler")}"
            })
            .Where(f => !string.IsNullOrWhiteSpace(f.Id)))
        {
            result.TryAdd(flow.Id!, flow.Shape);
        }
    }

    private static string ReadDataType(JsonElement parameter)
        => ReadString(parameter, "dataType")
            ?? ReadStringByPath(parameter, "type", "name")
            ?? ReadCompactJson(parameter, "type");

    private static string ReadCompactJson(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? value.GetRawText() : string.Empty;

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

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

    private static void AddExposureDisabledChange(
        List<MicroflowBreakingChangeDto> changes,
        JsonElement beforeSchema,
        JsonElement afterSchema,
        string exposureKind,
        string code,
        string message,
        string severity)
    {
        var beforeEnabled = ReadBoolByPath(beforeSchema, "exposure", exposureKind, "enabled");
        var afterEnabled = ReadBoolByPath(afterSchema, "exposure", exposureKind, "enabled");
        if (beforeEnabled && !afterEnabled)
        {
            changes.Add(Breaking(severity, code, message, $"exposure.{exposureKind}.enabled", "true", "false"));
        }
    }

    private static MicroflowBreakingChangeDto Breaking(string severity, string code, string message, string fieldPath, string? before, string? after)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Severity = severity,
            Code = code,
            Message = message,
            FieldPath = fieldPath,
            Before = before,
            After = after
        };
}

public sealed class MicroflowPublishImpactService : IMicroflowPublishImpactService
{
    private readonly IMicroflowVersionDiffService _diffService;

    public MicroflowPublishImpactService(IMicroflowVersionDiffService diffService) => _diffService = diffService;

    public MicroflowPublishImpactAnalysisDto Analyze(
        MicroflowResourceEntity resource,
        JsonElement currentSchema,
        JsonElement? latestPublishedSchema,
        IReadOnlyList<MicroflowReferenceEntity> references,
        string nextVersion)
    {
        if (!latestPublishedSchema.HasValue)
        {
            return new MicroflowPublishImpactAnalysisDto
            {
                ResourceId = resource.Id,
                CurrentVersion = resource.LatestPublishedVersion,
                NextVersion = nextVersion,
                References = references.Select(MicroflowReferenceService.ToDto).ToArray(),
                BreakingChanges = Array.Empty<MicroflowBreakingChangeDto>(),
                ImpactLevel = "none",
                Summary = new MicroflowPublishImpactSummaryDto
                {
                    ReferenceCount = references.Count
                }
            };
        }

        var breakingChanges = latestPublishedSchema.HasValue
            ? _diffService.Compare(latestPublishedSchema.Value, currentSchema).BreakingChanges
            : Array.Empty<MicroflowBreakingChangeDto>();

        var high = breakingChanges.Count(x => string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase));
        var medium = breakingChanges.Count(x => string.Equals(x.Severity, "medium", StringComparison.OrdinalIgnoreCase));
        var low = breakingChanges.Count(x => string.Equals(x.Severity, "low", StringComparison.OrdinalIgnoreCase));
        var referenceCount = references.Count;
        var impactLevel = high > 0
            ? "high"
            : medium > 0 || (referenceCount > 0 && breakingChanges.Count > 0)
                ? "medium"
                : low > 0 || referenceCount > 0
                    ? "low"
                    : "none";

        return new MicroflowPublishImpactAnalysisDto
        {
            ResourceId = resource.Id,
            CurrentVersion = resource.LatestPublishedVersion,
            NextVersion = nextVersion,
            References = references.Select(MicroflowReferenceService.ToDto).ToArray(),
            BreakingChanges = breakingChanges,
            ImpactLevel = impactLevel,
            Summary = new MicroflowPublishImpactSummaryDto
            {
                ReferenceCount = references.Count,
                BreakingChangeCount = breakingChanges.Count,
                HighImpactCount = high,
                MediumImpactCount = medium,
                LowImpactCount = low
            }
        };
    }

}

public sealed class MicroflowPublishService : IMicroflowPublishService
{
    private static readonly Regex VersionRegex = new(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowVersionRepository _versionRepository;
    private readonly IMicroflowPublishSnapshotRepository _publishSnapshotRepository;
    private readonly IMicroflowReferenceRepository _referenceRepository;
    private readonly IMicroflowPublishImpactService _impactService;
    private readonly IMicroflowStorageTransaction _transaction;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowClock _clock;

    public MicroflowPublishService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowVersionRepository versionRepository,
        IMicroflowPublishSnapshotRepository publishSnapshotRepository,
        IMicroflowReferenceRepository referenceRepository,
        IMicroflowPublishImpactService impactService,
        IMicroflowStorageTransaction transaction,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowClock clock)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _versionRepository = versionRepository;
        _publishSnapshotRepository = publishSnapshotRepository;
        _referenceRepository = referenceRepository;
        _impactService = impactService;
        _transaction = transaction;
        _requestContextAccessor = requestContextAccessor;
        _clock = clock;
    }

    public async Task<MicroflowPublishResultDto> PublishAsync(string resourceId, PublishMicroflowApiRequestDto request, CancellationToken cancellationToken)
    {
        ValidateVersion(request.Version);
        var resource = await LoadResourceAsync(resourceId, cancellationToken);
        EnsureNotArchived(resource);
        if (await _versionRepository.GetByResourceVersionAsync(resource.Id, request.Version.Trim(), cancellationToken) is not null)
        {
            throw FieldError(MicroflowApiErrorCode.MicroflowVersionConflict, 409, "version", "VERSION_DUPLICATED", "同一微流下版本号已存在。");
        }

        var currentSnapshot = await LoadSnapshotAsync(resource.CurrentSchemaSnapshotId, cancellationToken);
        var currentSchema = MicroflowSchemaJsonHelper.ParseRequired(currentSnapshot.SchemaJson);
        var validationIssues = MicroflowSchemaJsonHelper.ValidateForPublish(currentSchema);
        var validationSummary = new MicroflowValidationSummaryDto { ErrorCount = validationIssues.Count(x => x.Severity == "error") };
        if (validationSummary.ErrorCount > 0)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowPublishBlocked, "微流发布前校验未通过。", 409, validationIssues: validationIssues);
        }

        var latestPublished = await _publishSnapshotRepository.GetLatestByResourceIdAsync(resource.Id, cancellationToken);
        var latestPublishedSchema = latestPublished is null ? (JsonElement?)null : MicroflowSchemaJsonHelper.ParseRequired(latestPublished.SchemaJson);
        var references = await _referenceRepository.ListByTargetMicroflowIdAsync(resource.Id, includeInactive: false, cancellationToken);
        var impact = _impactService.Analyze(resource, currentSchema, latestPublishedSchema, references, request.Version.Trim());
        if (impact.Summary.HighImpactCount > 0 && !request.ConfirmBreakingChanges)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowPublishBlocked,
                "发布包含高影响破坏性变更，需要 confirmBreakingChanges=true 后重试。",
                409,
                details: "high impact breaking changes require explicit confirmation.",
                validationIssues: impact.BreakingChanges.Select(ToValidationIssue).ToArray());
        }

        var context = _requestContextAccessor.Current;
        var now = _clock.UtcNow;
        var publishSchemaJson = MicroflowSchemaJsonHelper.NormalizeAndValidate(currentSchema);
        var schemaHash = MicroflowSchemaJsonHelper.ComputeSha256(publishSchemaJson);
        var publishSchemaSnapshot = CreateSchemaSnapshot(resource, context, publishSchemaJson, schemaHash, request.Version.Trim(), $"publish:{request.Version}", currentSnapshot.Id, now);
        var publishSnapshot = new MicroflowPublishSnapshotEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ResourceId = resource.Id,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            Version = request.Version.Trim(),
            SchemaSnapshotId = publishSchemaSnapshot.Id,
            SchemaJson = publishSchemaJson,
            ValidationSummaryJson = JsonSerializer.Serialize(validationSummary, JsonOptions),
            ImpactAnalysisJson = JsonSerializer.Serialize(impact, JsonOptions),
            PublishedBy = context.UserId,
            PublishedAt = now,
            Description = request.Description,
            SchemaHash = schemaHash
        };
        var version = new MicroflowVersionEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ResourceId = resource.Id,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            Version = request.Version.Trim(),
            Status = "published",
            SchemaSnapshotId = publishSchemaSnapshot.Id,
            Description = request.Description,
            ValidationSummaryJson = JsonSerializer.Serialize(validationSummary, JsonOptions),
            ReferenceCount = references.Count,
            IsLatestPublished = true,
            CreatedBy = context.UserId,
            CreatedAt = now
        };

        await _transaction.ExecuteAsync(async () =>
        {
            await _schemaSnapshotRepository.InsertAsync(publishSchemaSnapshot, cancellationToken);
            await _publishSnapshotRepository.InsertAsync(publishSnapshot, cancellationToken);
            await _versionRepository.InsertAsync(version, cancellationToken);
            await _versionRepository.MarkLatestPublishedAsync(resource.Id, version.Id, cancellationToken);
            resource.Status = "published";
            resource.PublishStatus = "published";
            resource.Version = request.Version.Trim();
            resource.LatestPublishedVersion = request.Version.Trim();
            resource.CurrentSchemaSnapshotId = publishSchemaSnapshot.Id;
            resource.SchemaId = publishSchemaSnapshot.Id;
            resource.ReferenceCount = references.Count;
            Touch(resource, context, now);
            await _resourceRepository.UpdateAsync(resource, cancellationToken);
        }, cancellationToken);

        return new MicroflowPublishResultDto
        {
            Resource = MicroflowResourceMapper.ToDto(resource, publishSchemaSnapshot),
            Version = ToVersionSummary(version),
            Snapshot = ToPublishedSnapshot(publishSnapshot),
            ValidationSummary = validationSummary,
            ImpactAnalysis = impact
        };
    }

    public async Task<MicroflowPublishImpactAnalysisDto> AnalyzeImpactAsync(string resourceId, AnalyzeMicroflowImpactRequestDto request, CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(resourceId, cancellationToken);
        var currentSnapshot = await LoadSnapshotAsync(resource.CurrentSchemaSnapshotId, cancellationToken);
        var currentSchema = MicroflowSchemaJsonHelper.ParseRequired(currentSnapshot.SchemaJson);
        var latestPublished = await _publishSnapshotRepository.GetLatestByResourceIdAsync(resource.Id, cancellationToken);
        var latestPublishedSchema = latestPublished is null ? (JsonElement?)null : MicroflowSchemaJsonHelper.ParseRequired(latestPublished.SchemaJson);
        var references = await _referenceRepository.ListByTargetMicroflowIdAsync(resource.Id, includeInactive: false, cancellationToken);
        var impact = _impactService.Analyze(resource, currentSchema, latestPublishedSchema, references, request.Version ?? resource.Version);
        return impact with
        {
            References = request.IncludeReferences ? impact.References : Array.Empty<MicroflowReferenceDto>(),
            BreakingChanges = request.IncludeBreakingChanges ? impact.BreakingChanges : Array.Empty<MicroflowBreakingChangeDto>()
        };
    }

    internal static MicroflowVersionSummaryDto ToVersionSummary(MicroflowVersionEntity entity)
        => new()
        {
            Id = entity.Id,
            ResourceId = entity.ResourceId,
            Version = entity.Version,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            Description = entity.Description,
            SchemaSnapshotId = entity.SchemaSnapshotId,
            ValidationSummary = Deserialize<MicroflowValidationSummaryDto>(entity.ValidationSummaryJson) ?? new(),
            ReferenceCount = entity.ReferenceCount,
            IsLatestPublished = entity.IsLatestPublished
        };

    internal static MicroflowPublishedSnapshotDto ToPublishedSnapshot(MicroflowPublishSnapshotEntity entity)
        => new()
        {
            Id = entity.Id,
            ResourceId = entity.ResourceId,
            Version = entity.Version,
            Schema = MicroflowSchemaJsonHelper.ParseRequired(entity.SchemaJson),
            PublishedAt = entity.PublishedAt,
            PublishedBy = entity.PublishedBy,
            Description = entity.Description,
            ValidationSummary = Deserialize<MicroflowValidationSummaryDto>(entity.ValidationSummaryJson) ?? new(),
            SchemaHash = entity.SchemaHash
        };

    internal static MicroflowSchemaSnapshotEntity CreateSchemaSnapshot(
        MicroflowResourceEntity resource,
        MicroflowRequestContext context,
        string schemaJson,
        string schemaHash,
        string schemaVersion,
        string reason,
        string? baseVersion,
        DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            ResourceId = resource.Id,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            SchemaVersion = schemaVersion,
            MigrationVersion = "1.0",
            SchemaJson = schemaJson,
            SchemaHash = schemaHash,
            CreatedBy = context.UserId,
            CreatedAt = now,
            Reason = reason,
            BaseVersion = baseVersion
        };

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw FieldError(MicroflowApiErrorCode.MicroflowValidationFailed, 400, "version", "VERSION_REQUIRED", "version 不能为空。");
        }

        if (version.Any(char.IsWhiteSpace) || !VersionRegex.IsMatch(version.Trim()))
        {
            throw FieldError(MicroflowApiErrorCode.MicroflowValidationFailed, 400, "version", "VERSION_FORMAT_INVALID", "version 必须符合 semver-like 格式。");
        }
    }

    private async Task<MicroflowResourceEntity> LoadResourceAsync(string id, CancellationToken cancellationToken)
        => await _resourceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);

    private async Task<MicroflowSchemaSnapshotEntity> LoadSnapshotAsync(string? snapshotId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowPublishBlocked, "微流当前 Schema 快照不存在，无法发布。", 409);
        }

        return await _schemaSnapshotRepository.GetByIdAsync(snapshotId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowPublishBlocked, "微流当前 Schema 快照不存在，无法发布。", 409);
    }

    private static void EnsureNotArchived(MicroflowResourceEntity resource)
    {
        if (resource.Archived || string.Equals(resource.Status, "archived", StringComparison.OrdinalIgnoreCase))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowArchived, "微流资源已归档，不能发布。", 409);
        }
    }

    private static void Touch(MicroflowResourceEntity resource, MicroflowRequestContext context, DateTimeOffset now)
    {
        resource.UpdatedBy = context.UserId;
        resource.UpdatedAt = now;
        resource.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    private static MicroflowValidationIssueDto ToValidationIssue(MicroflowBreakingChangeDto change)
        => new()
        {
            Id = change.Id,
            Severity = "error",
            Code = change.Code,
            Message = change.Message,
            FieldPath = change.FieldPath,
            Details = $"{change.Before} -> {change.After}"
        };

    private static MicroflowApiException FieldError(string code, int status, string fieldPath, string fieldCode, string message)
        => new(code, message, status, fieldErrors:
        [
            new MicroflowApiFieldError { FieldPath = fieldPath, Code = fieldCode, Message = message }
        ]);
}

public sealed class MicroflowVersionService : IMicroflowVersionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowVersionRepository _versionRepository;
    private readonly IMicroflowPublishSnapshotRepository _publishSnapshotRepository;
    private readonly IMicroflowVersionDiffService _diffService;
    private readonly IMicroflowReferenceIndexer _referenceIndexer;
    private readonly IMicroflowStorageTransaction _transaction;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowClock _clock;

    public MicroflowVersionService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowVersionRepository versionRepository,
        IMicroflowPublishSnapshotRepository publishSnapshotRepository,
        IMicroflowVersionDiffService diffService,
        IMicroflowReferenceIndexer referenceIndexer,
        IMicroflowStorageTransaction transaction,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowClock clock)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _versionRepository = versionRepository;
        _publishSnapshotRepository = publishSnapshotRepository;
        _diffService = diffService;
        _referenceIndexer = referenceIndexer;
        _transaction = transaction;
        _requestContextAccessor = requestContextAccessor;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MicroflowVersionSummaryDto>> ListVersionsAsync(string resourceId, CancellationToken cancellationToken)
    {
        _ = await LoadResourceAsync(resourceId, cancellationToken);
        var versions = await _versionRepository.ListByResourceIdAsync(resourceId, cancellationToken);
        return versions.Select(MicroflowPublishService.ToVersionSummary).ToArray();
    }

    public async Task<MicroflowVersionDetailDto> GetVersionDetailAsync(string resourceId, string versionId, CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(resourceId, cancellationToken);
        var version = await LoadVersionAsync(resourceId, versionId, cancellationToken);
        var snapshot = await LoadPublishSnapshotAsync(resourceId, version.Version, cancellationToken);
        var diff = await CompareCurrentAsync(resource.Id, version.Id, cancellationToken);
        var summary = MicroflowPublishService.ToVersionSummary(version);
        return new MicroflowVersionDetailDto
        {
            Id = summary.Id,
            ResourceId = summary.ResourceId,
            Version = summary.Version,
            Status = summary.Status,
            CreatedAt = summary.CreatedAt,
            CreatedBy = summary.CreatedBy,
            Description = summary.Description,
            SchemaSnapshotId = summary.SchemaSnapshotId,
            ValidationSummary = summary.ValidationSummary,
            ReferenceCount = summary.ReferenceCount,
            IsLatestPublished = summary.IsLatestPublished,
            Snapshot = MicroflowPublishService.ToPublishedSnapshot(snapshot),
            DiffFromCurrent = diff
        };
    }

    public async Task<MicroflowResourceDto> RollbackAsync(string resourceId, string versionId, RollbackMicroflowVersionRequestDto request, CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(resourceId, cancellationToken);
        var version = await LoadVersionAsync(resourceId, versionId, cancellationToken);
        var snapshot = await LoadSchemaSnapshotAsync(version.SchemaSnapshotId, cancellationToken);
        var context = _requestContextAccessor.Current;
        var now = _clock.UtcNow;
        var schemaJson = MicroflowSchemaJsonHelper.NormalizeAndValidate(MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson));
        var newSnapshot = MicroflowPublishService.CreateSchemaSnapshot(resource, context, schemaJson, MicroflowSchemaJsonHelper.ComputeSha256(schemaJson), snapshot.SchemaVersion, $"rollback:{version.Version}", snapshot.Id, now);

        await _transaction.ExecuteAsync(async () =>
        {
            await _schemaSnapshotRepository.InsertAsync(newSnapshot, cancellationToken);
            resource.CurrentSchemaSnapshotId = newSnapshot.Id;
            resource.SchemaId = newSnapshot.Id;
            resource.Status = "draft";
            resource.PublishStatus = string.IsNullOrWhiteSpace(resource.LatestPublishedVersion) ? "neverPublished" : "changedAfterPublish";
            Touch(resource, context, now);
            await _resourceRepository.UpdateAsync(resource, cancellationToken);
        }, cancellationToken);

        await TryRebuildOutgoingReferencesAsync(resource.Id, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, newSnapshot);
    }

    public async Task<MicroflowResourceDto> DuplicateVersionAsync(string resourceId, string versionId, DuplicateMicroflowVersionRequestDto request, CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(resourceId, cancellationToken);
        var version = await LoadVersionAsync(resourceId, versionId, cancellationToken);
        var snapshot = await LoadSchemaSnapshotAsync(version.SchemaSnapshotId, cancellationToken);
        var context = _requestContextAccessor.Current;
        var now = _clock.UtcNow;
        var newResourceId = Guid.NewGuid().ToString("N");
        var name = string.IsNullOrWhiteSpace(request.Name) ? $"{resource.Name}Copy" : request.Name.Trim();
        if (await _resourceRepository.ExistsByNameAsync(resource.WorkspaceId, name, cancellationToken))
        {
            name = $"{name}{now.ToUnixTimeSeconds()}";
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? $"{resource.DisplayName} Copy" : request.DisplayName.Trim();
        var moduleId = string.IsNullOrWhiteSpace(request.ModuleId) ? resource.ModuleId : request.ModuleId.Trim();
        var moduleName = request.ModuleName ?? resource.ModuleName;
        var tags = request.Tags ?? MicroflowResourceMapper.ReadTags(resource.TagsJson);
        var schemaJson = MicroflowSchemaJsonHelper.MutateFields(snapshot.SchemaJson, newResourceId, name, displayName, moduleId, moduleName);
        var schemaSnapshot = new MicroflowSchemaSnapshotEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ResourceId = newResourceId,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            SchemaVersion = snapshot.SchemaVersion,
            MigrationVersion = snapshot.MigrationVersion,
            SchemaJson = schemaJson,
            SchemaHash = MicroflowSchemaJsonHelper.ComputeSha256(schemaJson),
            CreatedBy = context.UserId,
            CreatedAt = now,
            Reason = $"duplicate-version:{version.Version}",
            BaseVersion = snapshot.Id
        };
        var entity = new MicroflowResourceEntity
        {
            Id = newResourceId,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            ModuleId = moduleId,
            ModuleName = moduleName,
            Name = name,
            DisplayName = displayName,
            Description = resource.Description,
            TagsJson = JsonSerializer.Serialize(tags, JsonOptions),
            OwnerId = context.UserId,
            OwnerName = context.UserName,
            CreatedBy = context.UserId,
            CreatedAt = now,
            UpdatedBy = context.UserId,
            UpdatedAt = now,
            Version = "0.1.0",
            Status = "draft",
            PublishStatus = "neverPublished",
            Favorite = false,
            Archived = false,
            ReferenceCount = 0,
            LastRunStatus = "neverRun",
            LastRunAt = now,
            SchemaId = schemaSnapshot.Id,
            CurrentSchemaSnapshotId = schemaSnapshot.Id,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        await _transaction.ExecuteAsync(async () =>
        {
            await _schemaSnapshotRepository.InsertAsync(schemaSnapshot, cancellationToken);
            await _resourceRepository.InsertAsync(entity, cancellationToken);
        }, cancellationToken);

        await TryRebuildOutgoingReferencesAsync(entity.Id, cancellationToken);
        return MicroflowResourceMapper.ToDto(entity, schemaSnapshot);
    }

    public async Task<MicroflowVersionDiffDto> CompareCurrentAsync(string resourceId, string versionId, CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(resourceId, cancellationToken);
        var version = await LoadVersionAsync(resourceId, versionId, cancellationToken);
        var versionSnapshot = await LoadSchemaSnapshotAsync(version.SchemaSnapshotId, cancellationToken);
        var currentSnapshot = await LoadSchemaSnapshotAsync(resource.CurrentSchemaSnapshotId, cancellationToken);
        return _diffService.Compare(
            MicroflowSchemaJsonHelper.ParseRequired(versionSnapshot.SchemaJson),
            MicroflowSchemaJsonHelper.ParseRequired(currentSnapshot.SchemaJson));
    }

    private async Task<MicroflowResourceEntity> LoadResourceAsync(string id, CancellationToken cancellationToken)
        => await _resourceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);

    private async Task<MicroflowVersionEntity> LoadVersionAsync(string resourceId, string versionId, CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetByIdAsync(versionId, cancellationToken);
        if (version is null || !string.Equals(version.ResourceId, resourceId, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流版本不存在。", 404);
        }

        return version;
    }

    private async Task<MicroflowSchemaSnapshotEntity> LoadSchemaSnapshotAsync(string? snapshotId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流 Schema 快照不存在。", 404);
        }

        return await _schemaSnapshotRepository.GetByIdAsync(snapshotId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流 Schema 快照不存在。", 404);
    }

    private async Task<MicroflowPublishSnapshotEntity> LoadPublishSnapshotAsync(string resourceId, string version, CancellationToken cancellationToken)
        => await _publishSnapshotRepository.GetByResourceVersionAsync(resourceId, version, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流发布快照不存在。", 404);

    private static void Touch(MicroflowResourceEntity resource, MicroflowRequestContext context, DateTimeOffset now)
    {
        resource.UpdatedBy = context.UserId;
        resource.UpdatedAt = now;
        resource.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    private async Task TryRebuildOutgoingReferencesAsync(string resourceId, CancellationToken cancellationToken)
    {
        try
        {
            await _referenceIndexer.RebuildReferencesForMicroflowAsync(resourceId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 引用索引失败不阻断版本回滚/复制，后续可通过 rebuild API 修复。
        }
    }
}
