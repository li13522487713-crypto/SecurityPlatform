using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowMetadataService : IMicroflowMetadataService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowMetadataCacheRepository _metadataCacheRepository;
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowClock _clock;

    public MicroflowMetadataService(
        IMicroflowMetadataCacheRepository metadataCacheRepository,
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowClock clock)
    {
        _metadataCacheRepository = metadataCacheRepository;
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _requestContextAccessor = requestContextAccessor;
        _clock = clock;
    }

    public async Task<MicroflowMetadataCatalogDto> GetCatalogAsync(GetMicroflowMetadataRequestDto request, CancellationToken cancellationToken)
    {
        var context = _requestContextAccessor.Current;
        var workspaceId = request.WorkspaceId ?? context.WorkspaceId;
        var cache = await _metadataCacheRepository.GetLatestAsync(workspaceId, context.TenantId, cancellationToken);
        var sourceCatalog = cache is null ? MicroflowSeedMetadataCatalog.Create(_clock.UtcNow) : DeserializeCatalog(cache);
        var microflows = await BuildMicroflowRefsAsync(
            workspaceId,
            request.ModuleId,
            request.IncludeArchived,
            status: Array.Empty<string>(),
            keyword: null,
            cancellationToken);

        return ApplyCatalogFilters(sourceCatalog with
        {
            Microflows = microflows,
            Version = string.IsNullOrWhiteSpace(sourceCatalog.Version) ? cache?.CatalogVersion ?? MicroflowSeedMetadataCatalog.Version : sourceCatalog.Version,
            UpdatedAt = sourceCatalog.UpdatedAt == default ? cache?.UpdatedAt ?? _clock.UtcNow : sourceCatalog.UpdatedAt
        }, request);
    }

    public async Task<MetadataEntityDto> GetEntityAsync(string qualifiedName, GetMicroflowMetadataRequestDto request, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(request, cancellationToken);
        return catalog.Entities.FirstOrDefault(entity => string.Equals(entity.QualifiedName, qualifiedName, StringComparison.Ordinal))
            ?? throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowMetadataNotFound,
                $"实体元数据不存在：{qualifiedName}",
                404);
    }

    public async Task<MetadataEnumerationDto> GetEnumerationAsync(string qualifiedName, GetMicroflowMetadataRequestDto request, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(request, cancellationToken);
        return catalog.Enumerations.FirstOrDefault(enumeration => string.Equals(enumeration.QualifiedName, qualifiedName, StringComparison.Ordinal))
            ?? throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowMetadataNotFound,
                $"枚举元数据不存在：{qualifiedName}",
                404);
    }

    public Task<IReadOnlyList<MetadataMicroflowRefDto>> GetMicroflowRefsAsync(GetMicroflowRefsRequestDto request, CancellationToken cancellationToken)
    {
        var context = _requestContextAccessor.Current;
        return BuildMicroflowRefsAsync(
            request.WorkspaceId ?? context.WorkspaceId,
            request.ModuleId,
            request.IncludeArchived,
            request.Status,
            request.Keyword,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MetadataPageRefDto>> GetPageRefsAsync(GetPageRefsRequestDto request, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(
            new GetMicroflowMetadataRequestDto { WorkspaceId = request.WorkspaceId, ModuleId = request.ModuleId, IncludeSystem = true },
            cancellationToken);
        return catalog.Pages.Where(page => MatchesModule(page.ModuleName, request.ModuleId) && MatchesKeyword(page.Name, page.QualifiedName, page.Description, request.Keyword)).ToArray();
    }

    public async Task<IReadOnlyList<MetadataWorkflowRefDto>> GetWorkflowRefsAsync(GetWorkflowRefsRequestDto request, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(
            new GetMicroflowMetadataRequestDto { WorkspaceId = request.WorkspaceId, ModuleId = request.ModuleId, IncludeSystem = true },
            cancellationToken);
        return catalog.Workflows
            .Where(workflow => MatchesModule(workflow.ModuleName, request.ModuleId))
            .Where(workflow => string.IsNullOrWhiteSpace(request.ContextEntityQualifiedName)
                || string.Equals(workflow.ContextEntityQualifiedName, request.ContextEntityQualifiedName, StringComparison.Ordinal))
            .Where(workflow => MatchesKeyword(workflow.Name, workflow.QualifiedName, workflow.Description, request.Keyword))
            .ToArray();
    }

    public async Task<MicroflowMetadataHealthDto> GetHealthAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        var context = _requestContextAccessor.Current;
        var resolvedWorkspaceId = workspaceId ?? context.WorkspaceId;
        var cache = await _metadataCacheRepository.GetLatestAsync(resolvedWorkspaceId, context.TenantId, cancellationToken);
        var catalog = await GetCatalogAsync(
            new GetMicroflowMetadataRequestDto { WorkspaceId = resolvedWorkspaceId, IncludeSystem = true, IncludeArchived = true },
            cancellationToken);

        return new MicroflowMetadataHealthDto
        {
            Status = "ok",
            CacheExists = cache is not null,
            CatalogVersion = catalog.Version,
            EntityCount = catalog.Entities.Count,
            AssociationCount = catalog.Associations.Count,
            EnumerationCount = catalog.Enumerations.Count,
            MicroflowRefCount = catalog.Microflows.Count,
            PageCount = catalog.Pages.Count,
            WorkflowCount = catalog.Workflows.Count,
            UpdatedAt = catalog.UpdatedAt,
            Source = cache?.Source ?? "seed"
        };
    }

    private MicroflowMetadataCatalogDto DeserializeCatalog(MicroflowMetadataCacheEntity cache)
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<MicroflowMetadataCatalogDto>(cache.CatalogJson, JsonOptions);
            if (catalog is null)
            {
                throw new JsonException("CatalogJson deserialized to null.");
            }

            return catalog with
            {
                Version = string.IsNullOrWhiteSpace(catalog.Version) ? cache.CatalogVersion : catalog.Version,
                UpdatedAt = catalog.UpdatedAt == default ? cache.UpdatedAt : catalog.UpdatedAt
            };
        }
        catch (JsonException ex)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowMetadataLoadFailed,
                "微流元数据缓存读取失败。",
                500,
                details: ex.Message,
                innerException: ex);
        }
    }

    private async Task<IReadOnlyList<MetadataMicroflowRefDto>> BuildMicroflowRefsAsync(
        string? workspaceId,
        string? moduleId,
        bool includeArchived,
        IReadOnlyList<string> status,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var resources = await _resourceRepository.ListAsync(
            new MicroflowResourceQueryDto
            {
                WorkspaceId = workspaceId,
                ModuleId = moduleId,
                Status = status,
                PageIndex = 1,
                PageSize = 1000,
                SortBy = "name",
                SortOrder = "asc"
            },
            cancellationToken);
        var filtered = resources
            .Where(resource => includeArchived || (!resource.Archived && !string.Equals(resource.Status, "archived", StringComparison.OrdinalIgnoreCase)))
            .Where(resource => MatchesKeyword(resource.Name, resource.DisplayName, BuildQualifiedName(resource), resource.Description, keyword))
            .ToArray();

        var snapshotIds = filtered.Select(resource => resource.CurrentSchemaSnapshotId).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToArray();
        var snapshots = await _schemaSnapshotRepository.ListByIdsAsync(snapshotIds, cancellationToken);
        var snapshotMap = snapshots.ToDictionary(snapshot => snapshot.Id, StringComparer.Ordinal);

        return filtered.Select(resource => ToMicroflowRef(resource, snapshotMap)).ToArray();
    }

    private MetadataMicroflowRefDto ToMicroflowRef(
        MicroflowResourceEntity resource,
        IReadOnlyDictionary<string, MicroflowSchemaSnapshotEntity> snapshotMap)
    {
        JsonElement? schema = null;
        if (!string.IsNullOrWhiteSpace(resource.CurrentSchemaSnapshotId)
            && snapshotMap.TryGetValue(resource.CurrentSchemaSnapshotId, out var snapshot))
        {
            schema = MicroflowResourceMapper.ParseSchemaJson(snapshot.SchemaJson);
        }

        var moduleName = ResolveModuleName(resource);
        return new MetadataMicroflowRefDto
        {
            Id = resource.Id,
            Name = resource.Name,
            DisplayName = resource.DisplayName,
            QualifiedName = $"{moduleName}.{resource.Name}",
            ModuleId = resource.ModuleId,
            ModuleName = moduleName,
            Description = resource.Description,
            Parameters = schema.HasValue ? ReadParameters(schema.Value) : Array.Empty<MetadataMicroflowParameterDto>(),
            ReturnType = schema.HasValue ? ReadReturnType(schema.Value) : MicroflowSeedMetadataCatalog.UnknownType("schema unavailable"),
            Status = NormalizeStatus(resource),
            Version = resource.Version,
            SchemaId = resource.SchemaId
        };
    }

    private static MicroflowMetadataCatalogDto ApplyCatalogFilters(MicroflowMetadataCatalogDto catalog, GetMicroflowMetadataRequestDto request)
    {
        var entities = catalog.Entities
            .Where(entity => request.IncludeSystem || !entity.IsSystemEntity)
            .Where(entity => MatchesModule(entity.ModuleName, request.ModuleId))
            .ToArray();
        var entityNames = entities.Select(entity => entity.QualifiedName).ToHashSet(StringComparer.Ordinal);

        return catalog with
        {
            Entities = entities,
            Associations = catalog.Associations
                .Where(association => entityNames.Count == 0
                    || entityNames.Contains(association.SourceEntityQualifiedName)
                    || entityNames.Contains(association.TargetEntityQualifiedName))
                .ToArray(),
            Enumerations = catalog.Enumerations.Where(enumeration => MatchesModule(enumeration.ModuleName, request.ModuleId)).ToArray(),
            Microflows = catalog.Microflows,
            Pages = catalog.Pages.Where(page => MatchesModule(page.ModuleName, request.ModuleId)).ToArray(),
            Workflows = catalog.Workflows.Where(workflow => MatchesModule(workflow.ModuleName, request.ModuleId)).ToArray()
        };
    }

    private MetadataMicroflowParameterDto[] ReadParameters(JsonElement schema)
    {
        if (!schema.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MetadataMicroflowParameterDto>();
        }

        var result = new List<MetadataMicroflowParameterDto>();
        var order = 0;
        foreach (var parameter in parameters.EnumerateArray())
        {
            var name = ReadString(parameter, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                order++;
                continue;
            }

            var defaultValue = ReadExpressionText(parameter, "defaultValue");
            result.Add(new MetadataMicroflowParameterDto
            {
                Id = ReadString(parameter, "id") ?? ReadString(parameter, "stableId"),
                Name = name,
                Type = ReadType(parameter),
                Required = ReadBool(parameter, "required"),
                DefaultValue = defaultValue,
                DefaultValueExpression = defaultValue,
                Documentation = ReadString(parameter, "documentation"),
                Description = ReadString(parameter, "description") ?? ReadString(parameter, "documentation"),
                Order = order
            });
            order++;
        }

        return result.ToArray();
    }

    private static JsonElement ReadReturnType(JsonElement schema)
        => schema.TryGetProperty("returnType", out var returnType) ? returnType.Clone() : MicroflowSeedMetadataCatalog.Type("void");

    private JsonElement ReadType(JsonElement element)
    {
        if (element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Object)
        {
            return type.Clone();
        }

        var dataType = ReadString(element, "dataType");
        return dataType switch
        {
            null or "" => MicroflowSeedMetadataCatalog.UnknownType("parameter type missing"),
            "String" or "string" => MicroflowSeedMetadataCatalog.Type("string"),
            "Integer" or "integer" or "Int" => MicroflowSeedMetadataCatalog.Type("integer"),
            "Long" or "long" => MicroflowSeedMetadataCatalog.Type("long"),
            "Decimal" or "decimal" => MicroflowSeedMetadataCatalog.Type("decimal"),
            "Boolean" or "boolean" => MicroflowSeedMetadataCatalog.Type("boolean"),
            "DateTime" or "dateTime" => MicroflowSeedMetadataCatalog.Type("dateTime"),
            _ => MicroflowSeedMetadataCatalog.UnknownType($"unsupported dataType: {dataType}")
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadExpressionText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => ReadString(value, "raw") ?? ReadString(value, "text"),
            _ => null
        };
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string ResolveModuleName(MicroflowResourceEntity resource)
        => string.IsNullOrWhiteSpace(resource.ModuleName) ? resource.ModuleId : resource.ModuleName!;

    private static string BuildQualifiedName(MicroflowResourceEntity resource)
        => $"{ResolveModuleName(resource)}.{resource.Name}";

    private static string NormalizeStatus(MicroflowResourceEntity resource)
        => resource.Archived || string.Equals(resource.Status, "archived", StringComparison.OrdinalIgnoreCase)
            ? "archived"
            : string.Equals(resource.Status, "published", StringComparison.OrdinalIgnoreCase)
                ? "published"
                : "draft";

    private static bool MatchesModule(string moduleName, string? moduleId)
        => string.IsNullOrWhiteSpace(moduleId)
            || string.Equals(moduleName, moduleId, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesKeyword(string name, string qualifiedName, string? description, string? keyword)
        => MatchesKeyword(name, null, qualifiedName, description, keyword);

    private static bool MatchesKeyword(string name, string? displayName, string qualifiedName, string? description, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var value = keyword.Trim();
        return name.Contains(value, StringComparison.OrdinalIgnoreCase)
            || (displayName?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
            || qualifiedName.Contains(value, StringComparison.OrdinalIgnoreCase)
            || (description?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
