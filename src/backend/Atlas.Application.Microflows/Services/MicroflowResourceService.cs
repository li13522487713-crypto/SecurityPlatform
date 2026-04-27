using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowResourceService : IMicroflowResourceService
{
    private static readonly Regex MicroflowNameRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowReferenceRepository _referenceRepository;
    private readonly IMicroflowReferenceIndexer _referenceIndexer;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowClock _clock;

    public MicroflowResourceService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowReferenceRepository referenceRepository,
        IMicroflowReferenceIndexer referenceIndexer,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowClock clock)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _referenceRepository = referenceRepository;
        _referenceIndexer = referenceIndexer;
        _requestContextAccessor = requestContextAccessor;
        _clock = clock;
    }

    public async Task<MicroflowApiPageResult<MicroflowResourceDto>> ListAsync(
        ListMicroflowsRequestDto request,
        CancellationToken cancellationToken)
    {
        var context = _requestContextAccessor.Current;
        var query = new MicroflowResourceQueryDto
        {
            WorkspaceId = request.WorkspaceId ?? context.WorkspaceId,
            TenantId = context.TenantId,
            Keyword = request.Keyword,
            Status = request.Status,
            PublishStatus = request.PublishStatus,
            FavoriteOnly = request.FavoriteOnly,
            OwnerId = request.OwnerId,
            ModuleId = request.ModuleId,
            Tags = request.Tags,
            UpdatedFrom = request.UpdatedFrom,
            UpdatedTo = request.UpdatedTo,
            SortBy = request.SortBy,
            SortOrder = request.SortOrder,
            PageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex,
            PageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 100)
        };

        var items = await _resourceRepository.ListAsync(query, cancellationToken);
        var total = await _resourceRepository.CountAsync(query, cancellationToken);
        var dtos = items.Select(entity => MicroflowResourceMapper.ToDto(entity)).ToArray();

        return new MicroflowApiPageResult<MicroflowResourceDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            HasMore = query.PageIndex * query.PageSize < total
        };
    }

    public async Task<MicroflowResourceDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var (resource, snapshot) = await LoadResourceAndSnapshotAsync(id, requireSnapshot: false, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, snapshot);
    }

    public async Task<MicroflowResourceDto> CreateAsync(CreateMicroflowRequestDto request, CancellationToken cancellationToken)
    {
        ValidateCreateInput(request.Input);
        var context = _requestContextAccessor.Current;
        var workspaceId = request.WorkspaceId ?? context.WorkspaceId;
        var name = request.Input.Name.Trim();

        await EnsureNameAvailableAsync(workspaceId, name, null, cancellationToken);

        var now = _clock.UtcNow;
        var resourceId = Guid.NewGuid().ToString("N");
        var schema = request.Input.Schema.HasValue
            ? request.Input.Schema.Value
            : CreateBlankSchemaElement(request.Input, resourceId, context.UserName ?? context.UserId ?? "Current User", now);
        var schemaJson = NormalizeAndValidateAuthoringSchema(schema);
        var snapshot = CreateSnapshot(resourceId, workspaceId, context, schemaJson, "1.0.0", "initial create", null, now);
        var entity = new MicroflowResourceEntity
        {
            Id = resourceId,
            WorkspaceId = workspaceId,
            TenantId = context.TenantId,
            ModuleId = request.Input.ModuleId.Trim(),
            ModuleName = request.Input.ModuleName,
            Name = name,
            DisplayName = string.IsNullOrWhiteSpace(request.Input.DisplayName) ? name : request.Input.DisplayName.Trim(),
            Description = request.Input.Description,
            TagsJson = JsonSerializer.Serialize(request.Input.Tags ?? Array.Empty<string>(), JsonOptions),
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
            SchemaId = snapshot.Id,
            CurrentSchemaSnapshotId = snapshot.Id,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        try
        {
            await _schemaSnapshotRepository.InsertAsync(snapshot, cancellationToken);
            await _resourceRepository.InsertAsync(entity, cancellationToken);
        }
        catch (Exception ex) when (ex is not MicroflowApiException)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowStorageError,
                "微流资源创建写入失败。",
                500,
                details: ex.Message,
                innerException: ex);
        }
        return MicroflowResourceMapper.ToDto(entity, snapshot);
    }

    public async Task<MicroflowResourceDto> UpdateAsync(
        string id,
        UpdateMicroflowResourceRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(id, cancellationToken);
        EnsureEditable(resource);

        var context = _requestContextAccessor.Current;
        var patch = request.Patch;
        if (!string.IsNullOrWhiteSpace(patch.Name) && !string.Equals(resource.Name, patch.Name.Trim(), StringComparison.Ordinal))
        {
            ValidateName(patch.Name);
            await EnsureNameAvailableAsync(resource.WorkspaceId, patch.Name.Trim(), resource.Id, cancellationToken);
            resource.Name = patch.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(patch.DisplayName))
        {
            resource.DisplayName = patch.DisplayName.Trim();
        }

        if (patch.Description is not null)
        {
            resource.Description = patch.Description;
        }

        if (!string.IsNullOrWhiteSpace(patch.ModuleId))
        {
            resource.ModuleId = patch.ModuleId.Trim();
        }

        if (patch.ModuleName is not null)
        {
            resource.ModuleName = patch.ModuleName;
        }

        if (patch.Tags is not null)
        {
            resource.TagsJson = JsonSerializer.Serialize(patch.Tags, JsonOptions);
        }

        if (patch.OwnerId is not null)
        {
            resource.OwnerId = patch.OwnerId;
        }

        if (patch.OwnerName is not null)
        {
            resource.OwnerName = patch.OwnerName;
        }

        Touch(resource, context);
        await _resourceRepository.UpdateAsync(resource, cancellationToken);
        var snapshot = await LoadCurrentSnapshotAsync(resource, false, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, snapshot);
    }

    public async Task<GetMicroflowSchemaResponseDto> GetSchemaAsync(string id, CancellationToken cancellationToken)
    {
        var (resource, snapshot) = await LoadResourceAndSnapshotAsync(id, requireSnapshot: true, cancellationToken);
        var schema = MicroflowResourceMapper.ParseSchemaJson(snapshot!.SchemaJson)
            ?? throw SchemaInvalid("微流 Schema JSON 无法解析。");

        return new GetMicroflowSchemaResponseDto
        {
            ResourceId = resource.Id,
            Schema = schema,
            SchemaVersion = snapshot.SchemaVersion,
            MigrationVersion = snapshot.MigrationVersion,
            UpdatedAt = snapshot.CreatedAt,
            UpdatedBy = snapshot.CreatedBy
        };
    }

    public async Task<SaveMicroflowSchemaResponseDto> SaveSchemaAsync(
        string id,
        SaveMicroflowSchemaRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(id, cancellationToken);
        EnsureEditable(resource);
        var currentSnapshot = await LoadCurrentSnapshotAsync(resource, false, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.BaseVersion)
            && !string.Equals(request.BaseVersion, resource.Version, StringComparison.Ordinal)
            && !string.Equals(request.BaseVersion, resource.ConcurrencyStamp, StringComparison.Ordinal)
            && !string.Equals(request.BaseVersion, currentSnapshot?.Id, StringComparison.Ordinal)
            && !string.Equals(request.BaseVersion, currentSnapshot?.SchemaVersion, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowVersionConflict,
                "微流 Schema 已被其他操作更新，请刷新后重试。",
                409);
        }

        if (!request.Schema.HasValue)
        {
            throw SchemaInvalid("schema 不能为空。");
        }

        var schemaJson = NormalizeAndValidateAuthoringSchema(request.Schema.Value);
        var context = _requestContextAccessor.Current;
        var now = _clock.UtcNow;
        var snapshot = CreateSnapshot(resource.Id, resource.WorkspaceId, context, schemaJson, "1.0.0", request.SaveReason, request.BaseVersion, now);
        await _schemaSnapshotRepository.InsertAsync(snapshot, cancellationToken);

        resource.CurrentSchemaSnapshotId = snapshot.Id;
        resource.SchemaId = snapshot.Id;
        var changedAfterPublish = string.Equals(resource.Status, "published", StringComparison.OrdinalIgnoreCase);
        if (changedAfterPublish)
        {
            resource.PublishStatus = "changedAfterPublish";
        }

        Touch(resource, context);
        await _resourceRepository.UpdateAsync(resource, cancellationToken);
        await TryRebuildOutgoingReferencesAsync(resource.Id, cancellationToken);

        return new SaveMicroflowSchemaResponseDto
        {
            Resource = MicroflowResourceMapper.ToDto(resource, snapshot),
            SchemaVersion = snapshot.SchemaVersion,
            UpdatedAt = resource.UpdatedAt,
            ChangedAfterPublish = changedAfterPublish
        };
    }

    public async Task<MicroflowResourceDto> DuplicateAsync(
        string id,
        DuplicateMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var (source, sourceSnapshot) = await LoadResourceAndSnapshotAsync(id, requireSnapshot: true, cancellationToken);
        var name = string.IsNullOrWhiteSpace(request.Name) ? $"{source.Name}Copy" : request.Name.Trim();
        ValidateName(name);
        var workspaceId = source.WorkspaceId;
        await EnsureNameAvailableAsync(workspaceId, name, null, cancellationToken);

        var context = _requestContextAccessor.Current;
        var now = _clock.UtcNow;
        var newId = Guid.NewGuid().ToString("N");
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? $"{source.DisplayName} Copy" : request.DisplayName.Trim();
        var schemaJson = MutateSchemaFields(sourceSnapshot!.SchemaJson, newId, name, displayName, request.ModuleId ?? source.ModuleId, request.ModuleName ?? source.ModuleName);
        var snapshot = CreateSnapshot(newId, workspaceId, context, schemaJson, "1.0.0", "duplicate", null, now);
        var resource = new MicroflowResourceEntity
        {
            Id = newId,
            WorkspaceId = workspaceId,
            TenantId = source.TenantId,
            ModuleId = request.ModuleId ?? source.ModuleId,
            ModuleName = request.ModuleName ?? source.ModuleName,
            Name = name,
            DisplayName = displayName,
            Description = source.Description,
            TagsJson = JsonSerializer.Serialize(request.Tags ?? MicroflowResourceMapper.ReadTags(source.TagsJson), JsonOptions),
            OwnerId = context.UserId ?? source.OwnerId,
            OwnerName = context.UserName ?? source.OwnerName,
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
            SchemaId = snapshot.Id,
            CurrentSchemaSnapshotId = snapshot.Id,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        await _schemaSnapshotRepository.InsertAsync(snapshot, cancellationToken);
        await _resourceRepository.InsertAsync(resource, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, snapshot);
    }

    public async Task<MicroflowResourceDto> RenameAsync(
        string id,
        RenameMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateName(request.Name);
        var resource = await LoadResourceAsync(id, cancellationToken);
        EnsureEditable(resource);
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(resource.WorkspaceId, name, resource.Id, cancellationToken);
        var context = _requestContextAccessor.Current;
        var now = _clock.UtcNow;
        var currentSnapshot = await LoadCurrentSnapshotAsync(resource, true, cancellationToken);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? name : request.DisplayName.Trim();
        var schemaJson = MutateSchemaFields(currentSnapshot!.SchemaJson, resource.Id, name, displayName, resource.ModuleId, resource.ModuleName);
        var snapshot = CreateSnapshot(resource.Id, resource.WorkspaceId, context, schemaJson, currentSnapshot.SchemaVersion, "rename", currentSnapshot.Id, now);
        await _schemaSnapshotRepository.InsertAsync(snapshot, cancellationToken);

        resource.Name = name;
        resource.DisplayName = displayName;
        resource.SchemaId = snapshot.Id;
        resource.CurrentSchemaSnapshotId = snapshot.Id;
        if (string.Equals(resource.Status, "published", StringComparison.OrdinalIgnoreCase))
        {
            resource.PublishStatus = "changedAfterPublish";
        }

        Touch(resource, context);
        await _resourceRepository.UpdateAsync(resource, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, snapshot);
    }

    public async Task<MicroflowResourceDto> ToggleFavoriteAsync(
        string id,
        ToggleFavoriteMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(id, cancellationToken);
        resource.Favorite = request.Favorite;
        Touch(resource, _requestContextAccessor.Current);
        await _resourceRepository.UpdateAsync(resource, cancellationToken);
        var snapshot = await LoadCurrentSnapshotAsync(resource, false, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, snapshot);
    }

    public async Task<MicroflowResourceDto> ArchiveAsync(string id, CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(id, cancellationToken);
        await EnsureNoActiveTargetReferencesAsync(resource.Id, cancellationToken);
        if (!resource.Archived)
        {
            resource.Archived = true;
            resource.Status = "archived";
            Touch(resource, _requestContextAccessor.Current);
            await _resourceRepository.UpdateAsync(resource, cancellationToken);
        }

        var snapshot = await LoadCurrentSnapshotAsync(resource, false, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, snapshot);
    }

    public async Task<MicroflowResourceDto> RestoreAsync(string id, CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(id, cancellationToken);
        resource.Archived = false;
        resource.Status = "draft";
        resource.PublishStatus = string.IsNullOrWhiteSpace(resource.LatestPublishedVersion) ? "neverPublished" : "changedAfterPublish";
        Touch(resource, _requestContextAccessor.Current);
        await _resourceRepository.UpdateAsync(resource, cancellationToken);
        var snapshot = await LoadCurrentSnapshotAsync(resource, false, cancellationToken);
        return MicroflowResourceMapper.ToDto(resource, snapshot);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        _ = await LoadResourceAsync(id, cancellationToken);
        await EnsureNoActiveTargetReferencesAsync(id, cancellationToken);
        await _resourceRepository.DeleteAsync(id, cancellationToken);
        await _referenceRepository.DeleteBySourceAsync("microflow", id, cancellationToken);
    }

    private async Task EnsureNoActiveTargetReferencesAsync(string resourceId, CancellationToken cancellationToken)
    {
        var count = await _referenceRepository.CountByTargetMicroflowIdAsync(
            resourceId,
            new MicroflowReferenceQuery { IncludeInactive = false },
            cancellationToken);
        if (count > 0)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowReferenceBlocked,
                $"当前微流仍被 {count} 个 active reference 引用，不能删除或归档。",
                409);
        }
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
            // 引用索引失败不阻断 schema 保存；手动 rebuild API 可用于恢复。
        }
    }

    private async Task<MicroflowResourceEntity> LoadResourceAsync(string id, CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetByIdAsync(id, cancellationToken);
        if (resource is null)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        }

        return resource;
    }

    private async Task<(MicroflowResourceEntity Resource, MicroflowSchemaSnapshotEntity? Snapshot)> LoadResourceAndSnapshotAsync(
        string id,
        bool requireSnapshot,
        CancellationToken cancellationToken)
    {
        var resource = await LoadResourceAsync(id, cancellationToken);
        var snapshot = await LoadCurrentSnapshotAsync(resource, requireSnapshot, cancellationToken);
        return (resource, snapshot);
    }

    private async Task<MicroflowSchemaSnapshotEntity?> LoadCurrentSnapshotAsync(
        MicroflowResourceEntity resource,
        bool requireSnapshot,
        CancellationToken cancellationToken)
    {
        MicroflowSchemaSnapshotEntity? snapshot = null;
        if (!string.IsNullOrWhiteSpace(resource.CurrentSchemaSnapshotId))
        {
            snapshot = await _schemaSnapshotRepository.GetByIdAsync(resource.CurrentSchemaSnapshotId, cancellationToken);
        }

        snapshot ??= await _schemaSnapshotRepository.GetLatestByResourceIdAsync(resource.Id, cancellationToken);
        if (requireSnapshot && snapshot is null)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowStorageError, "微流 Schema 快照不存在。", 500);
        }

        return snapshot;
    }

    private async Task EnsureNameAvailableAsync(string? workspaceId, string name, string? excludeId, CancellationToken cancellationToken)
    {
        if (await _resourceRepository.ExistsByNameAsync(workspaceId, name, excludeId, cancellationToken))
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowNameDuplicated,
                "同一工作区下已存在同名微流。",
                409);
        }
    }

    private static void ValidateCreateInput(MicroflowCreateInputDto input)
    {
        ValidateName(input.Name);
        if (string.IsNullOrWhiteSpace(input.ModuleId))
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowValidationFailed,
                "moduleId 不能为空。",
                422,
                fieldErrors: [new MicroflowApiFieldError { FieldPath = "input.moduleId", Code = "REQUIRED", Message = "moduleId 不能为空。" }]);
        }
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !MicroflowNameRegex.IsMatch(name.Trim()))
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowValidationFailed,
                "微流 name 必须以字母开头，且只能包含字母、数字和下划线。",
                422,
                fieldErrors: [new MicroflowApiFieldError { FieldPath = "name", Code = "INVALID_FORMAT", Message = "name 格式非法。" }]);
        }
    }

    private static void EnsureEditable(MicroflowResourceEntity resource)
    {
        if (resource.Archived || string.Equals(resource.Status, "archived", StringComparison.OrdinalIgnoreCase))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowArchived, "归档微流不可编辑。", 409);
        }
    }

    private static string NormalizeAndValidateAuthoringSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw SchemaInvalid("schema 必须是对象。");
        }

        if (schema.TryGetProperty("nodes", out _)
            || schema.TryGetProperty("edges", out _)
            || schema.TryGetProperty("workflowJson", out _)
            || schema.TryGetProperty("flowgram", out _))
        {
            throw SchemaInvalid("不允许保存 FlowGram JSON。");
        }

        RequireProperty(schema, "schemaVersion");
        RequireProperty(schema, "id");
        RequireProperty(schema, "name");
        RequireProperty(schema, "objectCollection");
        RequireProperty(schema, "flows");
        RequireProperty(schema, "parameters");
        RequireProperty(schema, "returnType");
        return JsonSerializer.Serialize(schema, JsonOptions);
    }

    private static void RequireProperty(JsonElement schema, string propertyName)
    {
        if (!schema.TryGetProperty(propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw SchemaInvalid($"schema.{propertyName} 不能为空。");
        }
    }

    private static MicroflowApiException SchemaInvalid(string message)
        => new(MicroflowApiErrorCode.MicroflowSchemaInvalid, message, 400);

    private MicroflowSchemaSnapshotEntity CreateSnapshot(
        string resourceId,
        string? workspaceId,
        MicroflowRequestContext context,
        string schemaJson,
        string schemaVersion,
        string? reason,
        string? baseVersion,
        DateTimeOffset now)
    {
        return new MicroflowSchemaSnapshotEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ResourceId = resourceId,
            WorkspaceId = workspaceId,
            TenantId = context.TenantId,
            SchemaVersion = schemaVersion,
            MigrationVersion = "backend-resource-crud",
            SchemaJson = schemaJson,
            SchemaHash = ComputeSha256(schemaJson),
            CreatedBy = context.UserId,
            CreatedAt = now,
            Reason = reason,
            BaseVersion = baseVersion
        };
    }

    private static void Touch(MicroflowResourceEntity resource, MicroflowRequestContext context)
    {
        resource.UpdatedAt = DateTimeOffset.UtcNow;
        resource.UpdatedBy = context.UserId;
    }

    private static JsonElement CreateBlankSchemaElement(
        MicroflowCreateInputDto input,
        string id,
        string ownerName,
        DateTimeOffset timestamp)
    {
        var returnType = input.ReturnType ?? JsonSerializer.SerializeToElement(new { kind = "void" }, JsonOptions);
        var parameters = input.Parameters ?? JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions);
        var schema = new
        {
            schemaVersion = "1.0.0",
            mendixProfile = "mx10",
            id,
            stableId = id,
            name = input.Name,
            displayName = string.IsNullOrWhiteSpace(input.DisplayName) ? input.Name : input.DisplayName,
            description = input.Description,
            moduleId = input.ModuleId,
            moduleName = input.ModuleName,
            parameters,
            returnType,
            returnVariableName = input.ReturnVariableName,
            objectCollection = new
            {
                id = "root-collection",
                officialType = "Microflows$MicroflowObjectCollection",
                objects = new object[]
                {
                    new
                    {
                        id = "start",
                        stableId = "start",
                        kind = "startEvent",
                        officialType = "Microflows$StartEvent",
                        caption = "Start",
                        documentation = "",
                        relativeMiddlePoint = new { x = 320, y = 200 },
                        size = new { width = 132, height = 70 },
                        editor = new { iconKey = "startEvent" },
                        trigger = new { type = "manual" }
                    },
                    new
                    {
                        id = "end",
                        stableId = "end",
                        kind = "endEvent",
                        officialType = "Microflows$EndEvent",
                        caption = "End",
                        documentation = "",
                        relativeMiddlePoint = new { x = 560, y = 200 },
                        size = new { width = 132, height = 70 },
                        editor = new { iconKey = "endEvent" },
                        endBehavior = new { type = "normalReturn" }
                    }
                }
            },
            flows = new object[]
            {
                new
                {
                    id = "flow-start-end",
                    stableId = "flow-start-end",
                    kind = "sequence",
                    officialType = "Microflows$SequenceFlow",
                    originObjectId = "start",
                    destinationObjectId = "end",
                    originConnectionIndex = 0,
                    destinationConnectionIndex = 0,
                    caseValues = Array.Empty<object>(),
                    isErrorHandler = false,
                    line = new
                    {
                        kind = "orthogonal",
                        points = Array.Empty<object>(),
                        routing = new { mode = "auto", bendPoints = Array.Empty<object>() },
                        style = new { strokeType = "solid", strokeWidth = 2, arrow = "target" }
                    },
                    editor = new { edgeKind = "sequence" }
                }
            },
            security = input.Security ?? JsonSerializer.SerializeToElement(new { applyEntityAccess = true, allowedModuleRoleIds = Array.Empty<string>(), allowedRoleNames = Array.Empty<string>() }, JsonOptions),
            concurrency = input.Concurrency ?? JsonSerializer.SerializeToElement(new { allowConcurrentExecution = true, errorMicroflowId = (string?)null }, JsonOptions),
            exposure = input.Exposure ?? JsonSerializer.SerializeToElement(new { exportLevel = "module", markAsUsed = true, asMicroflowAction = new { enabled = false }, asWorkflowAction = new { enabled = false }, url = new { enabled = false } }, JsonOptions),
            variables = new { schemaId = id, builtAt = timestamp, all = Array.Empty<object>(), parameters = new { }, localVariables = new { }, objectOutputs = new { }, listOutputs = new { }, loopVariables = new { }, errorVariables = new { }, systemVariables = new { } },
            validation = new { issues = Array.Empty<object>() },
            editor = new { selection = new { }, viewport = new { x = 0, y = 0, zoom = 1 } },
            audit = new { version = "0.1.0", status = "draft", createdBy = ownerName, createdAt = timestamp, updatedBy = ownerName, updatedAt = timestamp }
        };

        return JsonSerializer.SerializeToElement(schema, JsonOptions);
    }

    private static string MutateSchemaFields(
        string schemaJson,
        string id,
        string name,
        string displayName,
        string moduleId,
        string? moduleName)
    {
        var node = JsonNode.Parse(schemaJson) as JsonObject
            ?? throw SchemaInvalid("微流 Schema JSON 无法解析。");
        node["id"] = id;
        node["stableId"] = id;
        node["name"] = name;
        node["displayName"] = displayName;
        node["moduleId"] = moduleId;
        node["moduleName"] = moduleName;
        return NormalizeAndValidateAuthoringSchema(JsonSerializer.SerializeToElement(node, JsonOptions));
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
