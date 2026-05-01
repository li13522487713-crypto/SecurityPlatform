using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Repositories;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class MendixDomainModelService : IMendixDomainModelService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMendixDomainModelDocumentRepository _documentRepository;
    private readonly IAppDefinitionRepository _appDefinitionRepository;
    private readonly ILowCodeAppResourceBindingService _bindingService;
    private readonly IDatabaseManagementService _databaseManagementService;
    private readonly IDatabaseStructureService _databaseStructureService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly AiDatabasePhysicalInstanceRepository _instanceRepository;

    public MendixDomainModelService(
        IMendixDomainModelDocumentRepository documentRepository,
        IAppDefinitionRepository appDefinitionRepository,
        ILowCodeAppResourceBindingService bindingService,
        IDatabaseManagementService databaseManagementService,
        IDatabaseStructureService databaseStructureService,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IIdGeneratorAccessor idGeneratorAccessor,
        AiDatabasePhysicalInstanceRepository instanceRepository)
    {
        _documentRepository = documentRepository;
        _appDefinitionRepository = appDefinitionRepository;
        _bindingService = bindingService;
        _databaseManagementService = databaseManagementService;
        _databaseStructureService = databaseStructureService;
        _requestContextAccessor = requestContextAccessor;
        _idGeneratorAccessor = idGeneratorAccessor;
        _instanceRepository = instanceRepository;
    }

    public async Task<MendixDomainModelDocumentDto> GetOrCreateAsync(string appId, string workspaceId, string moduleId, CancellationToken cancellationToken)
    {
        var app = await ResolveAppAsync(appId, workspaceId, cancellationToken);
        var existing = await _documentRepository.FindAsync(app.TenantId, app.Id, workspaceId, moduleId, cancellationToken);
        return existing is null
            ? CreateDefaultDocument(appId, workspaceId, moduleId)
            : Deserialize(existing);
    }

    public async Task<MendixDomainModelDocumentDto> SaveAsync(
        string appId,
        string workspaceId,
        string moduleId,
        MendixDomainModelDocumentDto document,
        long? updatedByUserId,
        CancellationToken cancellationToken)
    {
        var app = await ResolveAppAsync(appId, workspaceId, cancellationToken);
        var normalized = NormalizeDocument(document with { AppId = appId, WorkspaceId = workspaceId, ModuleId = moduleId });
        await EnsureDatabaseBindingsAsync(app.TenantId, app.Id, normalized.Bindings, cancellationToken);
        await UpsertAsync(app.TenantId, app.Id, workspaceId, moduleId, normalized, updatedByUserId, cancellationToken);
        return normalized;
    }

    public async Task<MendixDomainModelDocumentDto> UpdateBindingsAsync(
        string appId,
        string workspaceId,
        string moduleId,
        IReadOnlyList<MendixDomainModelBindingDto> bindings,
        long? updatedByUserId,
        CancellationToken cancellationToken)
    {
        var current = await GetOrCreateAsync(appId, workspaceId, moduleId, cancellationToken);
        var next = NormalizeDocument(current with { Bindings = bindings });
        return await SaveAsync(appId, workspaceId, moduleId, next, updatedByUserId, cancellationToken);
    }

    public async Task<MendixDomainModelImportResultDto> ImportTablesAsync(
        string appId,
        string workspaceId,
        string moduleId,
        MendixDomainModelImportTablesRequestDto request,
        long? updatedByUserId,
        CancellationToken cancellationToken)
    {
        if (request.TableNames.Count == 0)
        {
            throw new BusinessException("至少需要选择一张表。", ErrorCodes.ValidationError);
        }

        var document = await GetOrCreateAsync(appId, workspaceId, moduleId, cancellationToken);
        var binding = document.Bindings.FirstOrDefault(item => string.Equals(item.BindingId, request.BindingId, StringComparison.OrdinalIgnoreCase));
        if (binding is null)
        {
            throw new BusinessException("绑定数据库不存在。", ErrorCodes.ValidationError);
        }

        var instance = await ResolveInstanceAsync(request.SourceId, cancellationToken);
        var importedEntities = new List<MendixDomainModelEntityDto>();
        var existingByQualifiedName = document.Entities.ToDictionary(entity => entity.QualifiedName, StringComparer.OrdinalIgnoreCase);
        foreach (var tableName in request.TableNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var columns = await _databaseStructureService.GetTableColumnsAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                instance.Environment,
                tableName,
                request.SchemaName,
                cancellationToken);
            var qualifiedName = BuildQualifiedName(binding.Alias, request.SchemaName, tableName);
            var entity = new MendixDomainModelEntityDto
            {
                EntityId = existingByQualifiedName.TryGetValue(qualifiedName, out var existing) ? existing.EntityId : $"entity:{binding.BindingId}:{request.SchemaName}:{tableName}",
                BindingId = binding.BindingId,
                Name = tableName,
                QualifiedName = qualifiedName,
                SchemaName = request.SchemaName,
                TableName = tableName,
                Origin = "imported",
                SyncStatus = "clean",
                Persistable = true,
                Attributes = columns.Select(column => new MendixDomainModelAttributeDto
                {
                    AttributeId = existing?.Attributes.FirstOrDefault(item => string.Equals(item.ColumnName, column.Name, StringComparison.OrdinalIgnoreCase))?.AttributeId
                        ?? $"attr:{qualifiedName}:{column.Name}",
                    Name = column.Name,
                    ColumnName = column.Name,
                    Type = NormalizeLogicalType(column.DataType),
                    Required = !column.Nullable,
                    PrimaryKey = column.PrimaryKey,
                    Indexed = column.PrimaryKey,
                    DefaultValue = column.DefaultValue
                }).ToArray()
            };
            importedEntities.Add(entity);
        }

        var mergedEntities = document.Entities
            .Where(entity => !importedEntities.Any(imported => string.Equals(imported.QualifiedName, entity.QualifiedName, StringComparison.OrdinalIgnoreCase)))
            .Concat(importedEntities)
            .ToArray();
        var layout = document.Layout.EntityFrames.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        var baseIndex = layout.Count;
        foreach (var entity in importedEntities)
        {
            layout[entity.EntityId] = layout.TryGetValue(entity.EntityId, out var existingFrame)
                ? existingFrame
                : new MendixDomainModelEntityFrameDto
                {
                    X = 48 + ((baseIndex % 3) * 320),
                    Y = 48 + ((baseIndex / 3) * 220),
                    Width = 280,
                    Height = 180
                };
            baseIndex++;
        }

        var next = NormalizeDocument(document with
        {
            Entities = mergedEntities,
            Layout = document.Layout with { EntityFrames = layout }
        });
        var saved = await SaveAsync(appId, workspaceId, moduleId, next, updatedByUserId, cancellationToken);
        return new MendixDomainModelImportResultDto
        {
            Document = saved,
            ImportedEntityIds = importedEntities.Select(item => item.EntityId).ToArray()
        };
    }

    public async Task<MendixDomainModelSyncPlanDto> PreviewSyncAsync(
        string appId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken)
    {
        var document = await GetOrCreateAsync(appId, workspaceId, moduleId, cancellationToken);
        return await BuildSyncPlanAsync(document, cancellationToken);
    }

    public async Task<MendixDomainModelSyncResultDto> SyncDraftAsync(
        string appId,
        string workspaceId,
        string moduleId,
        long? updatedByUserId,
        CancellationToken cancellationToken)
    {
        var document = await GetOrCreateAsync(appId, workspaceId, moduleId, cancellationToken);
        var plan = await BuildSyncPlanAsync(document, cancellationToken);
        if (plan.Errors.Count > 0)
        {
            var failed = NormalizeDocument(document with
            {
                SyncState = new MendixDomainModelSyncStateDto
                {
                    Status = "failed",
                    LastError = string.Join("；", plan.Errors)
                }
            });
            var savedFailed = await SaveAsync(appId, workspaceId, moduleId, failed, updatedByUserId, cancellationToken);
            return new MendixDomainModelSyncResultDto
            {
                Document = savedFailed,
                Plan = plan,
                Applied = false
            };
        }

        foreach (var createTable in plan.CreateTables)
        {
            var entity = document.Entities.First(item => item.EntityId == createTable.EntityId);
            var binding = document.Bindings.First(item => item.BindingId == createTable.BindingId);
            var instance = await ResolveInstanceAsync(binding.SourceId, cancellationToken);
            await _databaseStructureService.CreateTableAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                new CreateTableRequest(
                    entity.SchemaName,
                    entity.TableName,
                    null,
                    entity.Attributes.Select(attribute => new TableColumnDesignDto(
                        attribute.ColumnName,
                        ToDatabaseType(attribute.Type),
                        Nullable: !attribute.Required,
                        PrimaryKey: attribute.PrimaryKey,
                        DefaultValue: attribute.DefaultValue)).ToArray(),
                    new TableOptionsDto(Schema: entity.SchemaName),
                    "visual"),
                cancellationToken);
        }

        foreach (var addColumn in plan.AddColumns)
        {
            var entity = document.Entities.First(item => string.Equals(item.TableName, addColumn.TableName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SchemaName, addColumn.SchemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.BindingId, addColumn.BindingId, StringComparison.OrdinalIgnoreCase));
            var attribute = entity.Attributes.First(item => string.Equals(item.ColumnName, addColumn.ColumnName, StringComparison.OrdinalIgnoreCase));
            var binding = document.Bindings.First(item => item.BindingId == addColumn.BindingId);
            var instance = await ResolveInstanceAsync(binding.SourceId, cancellationToken);
            await _databaseStructureService.AddColumnAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                new AddTableColumnRequest(
                    entity.SchemaName,
                    entity.TableName,
                    new TableColumnDesignDto(
                        attribute.ColumnName,
                        ToDatabaseType(attribute.Type),
                        Nullable: !attribute.Required,
                        PrimaryKey: attribute.PrimaryKey,
                        DefaultValue: attribute.DefaultValue)),
                cancellationToken);
        }

        var synced = NormalizeDocument(document with
        {
            Entities = document.Entities.Select(entity => entity with { SyncStatus = "clean" }).ToArray(),
            SyncState = new MendixDomainModelSyncStateDto
            {
                Status = "synced",
                LastSyncedAt = DateTimeOffset.UtcNow
            }
        });
        var saved = await SaveAsync(appId, workspaceId, moduleId, synced, updatedByUserId, cancellationToken);
        return new MendixDomainModelSyncResultDto
        {
            Document = saved,
            Plan = plan,
            Applied = true
        };
    }

    public async Task<IReadOnlyList<MendixDomainModelModuleSummaryDto>> ListModuleSummariesAsync(
        string appId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var app = await ResolveAppAsync(appId, workspaceId, cancellationToken);
        var items = await _documentRepository.ListByAppAsync(app.TenantId, app.Id, workspaceId, cancellationToken);
        return items
            .Select(Deserialize)
            .Select(item => new MendixDomainModelModuleSummaryDto
            {
                ModuleId = item.ModuleId,
                Entities = item.Entities
            })
            .ToArray();
    }

    public async Task<MendixDomainModelMetadataCatalogDto?> GetMetadataCatalogAsync(
        string? appId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken)
    {
        MendixDomainModelDocumentDto? document = null;
        if (!string.IsNullOrWhiteSpace(appId))
        {
            document = await GetOrCreateAsync(appId, workspaceId, moduleId, cancellationToken);
        }
        else
        {
            var items = await _documentRepository.ListByWorkspaceModuleAsync(CurrentTenantId(), workspaceId, moduleId, cancellationToken);
            document = items.Count == 0 ? null : Deserialize(items[0]);
        }

        if (document is null || document.Entities.Count == 0)
        {
            return null;
        }

        return new MendixDomainModelMetadataCatalogDto
        {
            Entities = document.Entities.Select(entity => ToMetadataEntity(document, entity)).ToArray(),
            Associations = document.Associations.Select(association => ToMetadataAssociation(document, association)).ToArray()
        };
    }

    private async Task UpsertAsync(
        TenantId tenantId,
        long appId,
        string workspaceId,
        string moduleId,
        MendixDomainModelDocumentDto document,
        long? updatedByUserId,
        CancellationToken cancellationToken)
    {
        var existing = await _documentRepository.FindAsync(tenantId, appId, workspaceId, moduleId, cancellationToken);
        var documentJson = JsonSerializer.Serialize(document with { SyncState = new MendixDomainModelSyncStateDto() }, JsonOptions);
        var syncStateJson = JsonSerializer.Serialize(document.SyncState, JsonOptions);
        if (existing is null)
        {
            await _documentRepository.InsertAsync(
                new MendixDomainModelDocument(
                    tenantId,
                    _idGeneratorAccessor.NextId(),
                    appId,
                    workspaceId,
                    moduleId,
                    documentJson,
                    syncStateJson,
                    updatedByUserId),
                cancellationToken);
            return;
        }

        existing.ReplaceDocument(documentJson, syncStateJson, updatedByUserId);
        await _documentRepository.UpdateAsync(existing, cancellationToken);
    }

    private async Task EnsureDatabaseBindingsAsync(TenantId tenantId, long appId, IReadOnlyList<MendixDomainModelBindingDto> bindings, CancellationToken cancellationToken)
    {
        foreach (var binding in bindings)
        {
            if (!long.TryParse(binding.AiDatabaseId, out var databaseId))
            {
                continue;
            }

            await _bindingService.BindAsync(
                tenantId,
                appId,
                new Atlas.Application.AiPlatform.Models.AiAppResourceBindingCreateRequest("database", databaseId, "bound", 0, null),
                cancellationToken);
        }
    }

    private async Task<MendixDomainModelSyncPlanDto> BuildSyncPlanAsync(MendixDomainModelDocumentDto document, CancellationToken cancellationToken)
    {
        var createTables = new List<MendixDomainModelCreateTablePlanDto>();
        var addColumns = new List<MendixDomainModelAddColumnPlanDto>();
        var warnings = new List<string>();
        var errors = new List<string>();

        foreach (var binding in document.Bindings.Where(item => item.Enabled))
        {
            var sourceId = binding.SourceId;
            var bindingEntities = document.Entities.Where(entity => string.Equals(entity.BindingId, binding.BindingId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (bindingEntities.Length == 0)
            {
                continue;
            }

            var schemas = await _databaseManagementService.ListSchemasAsync(CurrentTenantId(), sourceId, cancellationToken);
            var schemaMap = schemas.ToDictionary(schema => schema.Name, StringComparer.OrdinalIgnoreCase);
            var instance = await ResolveInstanceAsync(sourceId, cancellationToken);
            foreach (var entity in bindingEntities)
            {
                if (!schemaMap.TryGetValue(entity.SchemaName, out var schema))
                {
                    createTables.Add(new MendixDomainModelCreateTablePlanDto
                    {
                        BindingId = binding.BindingId,
                        EntityId = entity.EntityId,
                        SchemaName = entity.SchemaName,
                        TableName = entity.TableName
                    });
                    warnings.Add($"Schema {entity.SchemaName} 当前未出现在 {binding.Alias} 中，将按默认方言直接尝试建表。");
                    continue;
                }

                var existingTable = schema.Groups.SelectMany(group => group.Objects)
                    .FirstOrDefault(item => string.Equals(item.ObjectType, "table", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(item.Name, entity.TableName, StringComparison.OrdinalIgnoreCase));
                if (existingTable is null)
                {
                    createTables.Add(new MendixDomainModelCreateTablePlanDto
                    {
                        BindingId = binding.BindingId,
                        EntityId = entity.EntityId,
                        SchemaName = entity.SchemaName,
                        TableName = entity.TableName
                    });
                    continue;
                }

                var columns = await _databaseStructureService.GetTableColumnsAsync(
                    CurrentTenantId(),
                    instance.AiDatabaseId,
                    instance.Environment,
                    entity.TableName,
                    entity.SchemaName,
                    cancellationToken);
                foreach (var attribute in entity.Attributes)
                {
                    if (columns.All(column => !string.Equals(column.Name, attribute.ColumnName, StringComparison.OrdinalIgnoreCase)))
                    {
                        addColumns.Add(new MendixDomainModelAddColumnPlanDto
                        {
                            BindingId = binding.BindingId,
                            SchemaName = entity.SchemaName,
                            TableName = entity.TableName,
                            ColumnName = attribute.ColumnName
                        });
                    }
                }

                var extraColumns = columns.Where(column => entity.Attributes.All(attribute => !string.Equals(attribute.ColumnName, column.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
                if (extraColumns.Length > 0)
                {
                    warnings.Add($"{entity.QualifiedName} 存在 {extraColumns.Length} 个数据库额外列；首版不会自动删列。");
                }
            }
        }

        foreach (var association in document.Associations.Where(item => string.Equals(item.BindingMode, "logicalCrossDb", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add($"跨库关系 {association.Name} 已进入设计态，但本轮 Draft 同步不会自动创建跨库物理外键。");
        }

        return new MendixDomainModelSyncPlanDto
        {
            CreateTables = createTables,
            AddColumns = addColumns,
            Warnings = warnings,
            Errors = errors
        };
    }

    private async Task<AppDefinition> ResolveAppAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(appId, out var parsedAppId) || parsedAppId <= 0)
        {
            throw new BusinessException("appId 无效。", ErrorCodes.ValidationError);
        }

        var app = await _appDefinitionRepository.FindByIdAsync(CurrentTenantId(), parsedAppId, cancellationToken)
            ?? throw new BusinessException("应用不存在。", ErrorCodes.NotFound);
        if (!string.Equals(app.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("应用不属于当前工作区。", ErrorCodes.ValidationError);
        }

        return app;
    }

    private async Task<AiDatabasePhysicalInstance> ResolveInstanceAsync(string sourceId, CancellationToken cancellationToken)
    {
        var rawId = sourceId.StartsWith("ai:", StringComparison.OrdinalIgnoreCase) ? sourceId[3..] : sourceId;
        if (!long.TryParse(rawId, out var instanceId) || instanceId <= 0)
        {
            throw new BusinessException("sourceId 无效。", ErrorCodes.ValidationError);
        }

        return await _instanceRepository.FindByIdAsync(CurrentTenantId(), instanceId, cancellationToken)
            ?? throw new BusinessException("数据库实例不存在。", ErrorCodes.NotFound);
    }

    private static MendixDomainModelDocumentDto CreateDefaultDocument(string appId, string workspaceId, string moduleId)
        => new()
        {
            AppId = appId,
            WorkspaceId = workspaceId,
            ModuleId = moduleId,
            Layout = new MendixDomainModelLayoutDto(),
            SyncState = new MendixDomainModelSyncStateDto()
        };

    private static MendixDomainModelDocumentDto NormalizeDocument(MendixDomainModelDocumentDto document)
    {
        var normalizedBindings = document.Bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.SourceId))
            .Select((binding, index) => binding with
            {
                BindingId = string.IsNullOrWhiteSpace(binding.BindingId) ? $"binding:{index + 1}" : binding.BindingId.Trim(),
                Alias = string.IsNullOrWhiteSpace(binding.Alias) ? $"db{index + 1}" : binding.Alias.Trim()
            })
            .GroupBy(binding => binding.BindingId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var bindingAliasById = normalizedBindings.ToDictionary(binding => binding.BindingId, binding => binding.Alias, StringComparer.OrdinalIgnoreCase);
        var normalizedEntities = document.Entities
            .Select(entity =>
            {
                var alias = bindingAliasById.GetValueOrDefault(entity.BindingId) ?? "db";
                var schemaName = string.IsNullOrWhiteSpace(entity.SchemaName) ? "main" : entity.SchemaName.Trim();
                var tableName = string.IsNullOrWhiteSpace(entity.TableName) ? entity.Name.Trim() : entity.TableName.Trim();
                var qualifiedName = BuildQualifiedName(alias, schemaName, tableName);
                return entity with
                {
                    EntityId = string.IsNullOrWhiteSpace(entity.EntityId) ? $"entity:{qualifiedName}" : entity.EntityId.Trim(),
                    Name = string.IsNullOrWhiteSpace(entity.Name) ? tableName : entity.Name.Trim(),
                    SchemaName = schemaName,
                    TableName = tableName,
                    QualifiedName = qualifiedName,
                    Attributes = entity.Attributes
                        .Where(attribute => !string.IsNullOrWhiteSpace(attribute.ColumnName) || !string.IsNullOrWhiteSpace(attribute.Name))
                        .Select(attribute => attribute with
                        {
                            AttributeId = string.IsNullOrWhiteSpace(attribute.AttributeId)
                                ? $"attr:{qualifiedName}:{attribute.ColumnName}"
                                : attribute.AttributeId.Trim(),
                            Name = string.IsNullOrWhiteSpace(attribute.Name) ? attribute.ColumnName.Trim() : attribute.Name.Trim(),
                            ColumnName = string.IsNullOrWhiteSpace(attribute.ColumnName) ? attribute.Name.Trim() : attribute.ColumnName.Trim(),
                            Type = NormalizeLogicalType(attribute.Type)
                        })
                        .ToArray()
                };
            })
            .GroupBy(entity => entity.EntityId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        return document with
        {
            Bindings = normalizedBindings,
            Entities = normalizedEntities
        };
    }

    private static MendixDomainModelDocumentDto Deserialize(MendixDomainModelDocument document)
    {
        var baseDocument = JsonSerializer.Deserialize<MendixDomainModelDocumentDto>(document.DocumentJson, JsonOptions) ?? new MendixDomainModelDocumentDto();
        var syncState = JsonSerializer.Deserialize<MendixDomainModelSyncStateDto>(document.SyncStateJson, JsonOptions) ?? new MendixDomainModelSyncStateDto();
        return NormalizeDocument(baseDocument with { SyncState = syncState });
    }

    private static string BuildQualifiedName(string alias, string schemaName, string tableName)
        => $"{alias}.{schemaName}.{tableName}";

    private static string NormalizeLogicalType(string? dataType)
    {
        var normalized = dataType?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "int" or "integer" => "integer",
            "bigint" or "long" => "long",
            "decimal" or "numeric" or "real" or "double" => "decimal",
            "bool" or "boolean" or "tinyint" => "boolean",
            "datetime" or "timestamp" or "date" => "dateTime",
            "json" or "jsonb" => "string",
            _ => "string"
        };
    }

    private static string ToDatabaseType(string logicalType)
        => NormalizeLogicalType(logicalType) switch
        {
            "integer" => "INTEGER",
            "long" => "BIGINT",
            "decimal" => "DECIMAL",
            "boolean" => "BOOLEAN",
            "dateTime" => "DATETIME",
            _ => "TEXT"
        };

    private static MetadataEntityDto ToMetadataEntity(MendixDomainModelDocumentDto document, MendixDomainModelEntityDto entity)
    {
        var binding = document.Bindings.FirstOrDefault(item => string.Equals(item.BindingId, entity.BindingId, StringComparison.OrdinalIgnoreCase));
        return new MetadataEntityDto
        {
            Id = entity.EntityId,
            Name = entity.Name,
            QualifiedName = entity.QualifiedName,
            ModuleId = document.ModuleId,
            ModuleName = document.ModuleId,
            BindingId = entity.BindingId,
            SourceId = binding?.SourceId,
            AiDatabaseId = binding?.AiDatabaseId,
            DriverCode = binding?.DriverCode,
            SchemaName = entity.SchemaName,
            TableName = entity.TableName,
            Attributes = entity.Attributes.Select(attribute => new MetadataAttributeDto
            {
                Id = attribute.AttributeId,
                Name = attribute.Name,
                QualifiedName = $"{entity.QualifiedName}.{attribute.Name}",
                Type = CreateMetadataType(attribute),
                Required = attribute.Required,
                PrimaryKey = attribute.PrimaryKey,
                DefaultValue = attribute.DefaultValue,
                ColumnName = attribute.ColumnName
            }).ToArray(),
            Associations = Array.Empty<MetadataAssociationRefDto>(),
            IsPersistable = entity.Persistable
        };
    }

    private static MetadataAssociationDto ToMetadataAssociation(MendixDomainModelDocumentDto document, MendixDomainModelAssociationDto association)
    {
        var source = document.Entities.FirstOrDefault(entity => entity.EntityId == association.FromEntityId);
        var target = document.Entities.FirstOrDefault(entity => entity.EntityId == association.ToEntityId);
        return new MetadataAssociationDto
        {
            Id = association.AssociationId,
            Name = association.Name,
            QualifiedName = $"{source?.QualifiedName}_{target?.QualifiedName}_{association.Name}",
            SourceEntityQualifiedName = source?.QualifiedName ?? association.FromEntityId,
            TargetEntityQualifiedName = target?.QualifiedName ?? association.ToEntityId,
            OwnerEntityQualifiedName = source?.QualifiedName,
            Multiplicity = association.Cardinality,
            Direction = "sourceToTarget",
            Documentation = association.BindingMode,
            BindingMode = association.BindingMode,
            SourceBindingId = source?.BindingId,
            TargetBindingId = target?.BindingId,
            SourceField = association.JoinSpec?.SourceField ?? (association.SourceAttributeId is null ? null : source?.Attributes.FirstOrDefault(item => item.AttributeId == association.SourceAttributeId)?.ColumnName),
            TargetField = association.JoinSpec?.TargetField ?? (association.TargetAttributeId is null ? null : target?.Attributes.FirstOrDefault(item => item.AttributeId == association.TargetAttributeId)?.ColumnName),
            JoinType = association.JoinSpec?.JoinType
        };
    }

    private static JsonElement CreateMetadataType(MendixDomainModelAttributeDto attribute)
        => attribute.Type switch
        {
            "integer" => MicroflowSeedMetadataCatalog.Type("integer"),
            "long" => MicroflowSeedMetadataCatalog.Type("long"),
            "decimal" => MicroflowSeedMetadataCatalog.Type("decimal"),
            "boolean" => MicroflowSeedMetadataCatalog.Type("boolean"),
            "dateTime" => MicroflowSeedMetadataCatalog.Type("dateTime"),
            _ => MicroflowSeedMetadataCatalog.Type("string")
        };

    private TenantId CurrentTenantId() => _requestContextAccessor.Current.TenantId is { Length: > 0 } tenantId && Guid.TryParse(tenantId, out var parsed)
        ? new TenantId(parsed)
        : TenantId.Empty;
}
