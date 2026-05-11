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
using Atlas.Infrastructure.Services.DatabaseStructure;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class MendixDomainModelService : IMendixDomainModelService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ManagedAuditColumns = ["created_at", "created_by", "updated_at", "updated_by"];

    private readonly IMendixDomainModelDocumentRepository _documentRepository;
    private readonly IAppDefinitionRepository _appDefinitionRepository;
    private readonly ISqlSugarClient _db;
    private readonly ILowCodeAppResourceBindingService _bindingService;
    private readonly IDatabaseManagementService _databaseManagementService;
    private readonly IDatabaseStructureService _databaseStructureService;
    private readonly IDatabaseDialectRegistry _dialectRegistry;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly AiDatabasePhysicalInstanceRepository _instanceRepository;

    public MendixDomainModelService(
        IMendixDomainModelDocumentRepository documentRepository,
        IAppDefinitionRepository appDefinitionRepository,
        ISqlSugarClient db,
        ILowCodeAppResourceBindingService bindingService,
        IDatabaseManagementService databaseManagementService,
        IDatabaseStructureService databaseStructureService,
        IDatabaseDialectRegistry dialectRegistry,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IIdGeneratorAccessor idGeneratorAccessor,
        AiDatabasePhysicalInstanceRepository instanceRepository)
    {
        _documentRepository = documentRepository;
        _appDefinitionRepository = appDefinitionRepository;
        _db = db;
        _bindingService = bindingService;
        _databaseManagementService = databaseManagementService;
        _databaseStructureService = databaseStructureService;
        _dialectRegistry = dialectRegistry;
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
                        ToDatabaseType(attribute.Type, binding.DriverCode),
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
                        ToDatabaseType(attribute.Type, binding.DriverCode),
                        Nullable: !attribute.Required,
                        PrimaryKey: attribute.PrimaryKey,
                        DefaultValue: attribute.DefaultValue)),
                cancellationToken);
        }

        foreach (var renameColumn in plan.RenameColumns)
        {
            var binding = document.Bindings.First(item => item.BindingId == renameColumn.BindingId);
            var instance = await ResolveInstanceAsync(binding.SourceId, cancellationToken);
            await _databaseStructureService.RenameColumnAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                new RenameTableColumnRequest(
                    renameColumn.SchemaName,
                    renameColumn.TableName,
                    renameColumn.ColumnName,
                    renameColumn.NewColumnName),
                cancellationToken);
        }

        foreach (var alterColumn in plan.AlterColumns)
        {
            var binding = document.Bindings.First(item => item.BindingId == alterColumn.BindingId);
            var instance = await ResolveInstanceAsync(binding.SourceId, cancellationToken);
            await _databaseStructureService.AlterColumnAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                new AlterTableColumnRequest(
                    alterColumn.SchemaName,
                    alterColumn.TableName,
                    alterColumn.ColumnName,
                    new TableColumnDesignDto(
                        alterColumn.ColumnName,
                        alterColumn.DataType,
                        Nullable: alterColumn.Nullable,
                        PrimaryKey: alterColumn.PrimaryKey,
                        DefaultValue: alterColumn.DefaultValue)),
                cancellationToken);
        }

        foreach (var dropForeignKey in plan.DropForeignKeys)
        {
            var binding = document.Bindings.First(item => item.BindingId == dropForeignKey.BindingId);
            var instance = await ResolveInstanceAsync(binding.SourceId, cancellationToken);
            await _databaseStructureService.DropForeignKeyAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                new DropForeignKeyRequest(
                    dropForeignKey.SchemaName,
                    dropForeignKey.TableName,
                    dropForeignKey.ForeignKeyName),
                cancellationToken);
        }

        foreach (var dropColumn in plan.DropColumns)
        {
            var binding = document.Bindings.First(item => item.BindingId == dropColumn.BindingId);
            var instance = await ResolveInstanceAsync(binding.SourceId, cancellationToken);
            await _databaseStructureService.DropColumnAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                new DropTableColumnRequest(
                    dropColumn.SchemaName,
                    dropColumn.TableName,
                    dropColumn.ColumnName),
                cancellationToken);
        }

        foreach (var createForeignKey in plan.CreateForeignKeys)
        {
            var binding = document.Bindings.First(item => item.BindingId == createForeignKey.BindingId);
            var instance = await ResolveInstanceAsync(binding.SourceId, cancellationToken);
            await _databaseStructureService.CreateForeignKeyAsync(
                CurrentTenantId(),
                instance.AiDatabaseId,
                new CreateForeignKeyRequest(
                    createForeignKey.SchemaName,
                    createForeignKey.TableName,
                    createForeignKey.ForeignKeyName,
                    createForeignKey.SourceColumns,
                    createForeignKey.ReferencedTableName,
                    createForeignKey.ReferencedSchemaName,
                    createForeignKey.ReferencedColumns,
                    createForeignKey.OnDelete,
                    createForeignKey.OnUpdate),
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

    public async Task<MendixDomainModelMetadataCatalogDto> RefreshMetadataAsync(
        string appId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken)
    {
        return await GetMetadataCatalogAsync(appId, workspaceId, moduleId, cancellationToken)
            ?? new MendixDomainModelMetadataCatalogDto();
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
        var alterColumns = new List<MendixDomainModelAlterColumnPlanDto>();
        var renameColumns = new List<MendixDomainModelRenameColumnPlanDto>();
        var dropColumns = new List<MendixDomainModelDropColumnPlanDto>();
        var createForeignKeys = new List<MendixDomainModelCreateForeignKeyPlanDto>();
        var dropForeignKeys = new List<MendixDomainModelDropForeignKeyPlanDto>();
        var warnings = new List<string>();
        var errors = new List<string>();

        foreach (var binding in document.Bindings.Where(item => item.Enabled))
        {
            var dialect = _dialectRegistry.Resolve(binding.DriverCode);
            var sourceId = binding.SourceId;
            var bindingEntities = document.Entities.Where(entity => string.Equals(entity.BindingId, binding.BindingId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (bindingEntities.Length == 0)
            {
                continue;
            }

            var schemas = await _databaseManagementService.ListSchemasAsync(CurrentTenantId(), sourceId, cancellationToken);
            var schemaMap = schemas.ToDictionary(schema => schema.Name, StringComparer.OrdinalIgnoreCase);
            var instance = await ResolveInstanceAsync(sourceId, cancellationToken);
            var desiredForeignKeys = BuildDesiredForeignKeys(document, binding, warnings);
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
                    AppendCreateForeignKeysForNewTable(entity, desiredForeignKeys, createForeignKeys);
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
                    AppendCreateForeignKeysForNewTable(entity, desiredForeignKeys, createForeignKeys);
                    continue;
                }

                var columns = await _databaseStructureService.GetTableColumnsAsync(
                    CurrentTenantId(),
                    instance.AiDatabaseId,
                    instance.Environment,
                    entity.TableName,
                    entity.SchemaName,
                    cancellationToken);
                AnalyzeColumnDiffs(
                    binding,
                    dialect,
                    entity,
                    columns,
                    addColumns,
                    alterColumns,
                    renameColumns,
                    dropColumns,
                    warnings);

                if (string.Equals(binding.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
                {
                    if (desiredForeignKeys.ContainsKey(BuildTableKey(entity.SchemaName, entity.TableName)))
                    {
                        warnings.Add($"SQLite 绑定 {binding.Alias} 的关系 {entity.QualifiedName} 需要通过重建表结构维护外键，当前不会自动执行 FK 变更。");
                    }
                    continue;
                }

                var existingForeignKeys = await _databaseStructureService.GetTableForeignKeysAsync(
                    CurrentTenantId(),
                    instance.AiDatabaseId,
                    instance.Environment,
                    entity.TableName,
                    entity.SchemaName,
                    cancellationToken);
                AnalyzeForeignKeys(
                    entity,
                    existingForeignKeys,
                    desiredForeignKeys.GetValueOrDefault(BuildTableKey(entity.SchemaName, entity.TableName)) ?? [],
                    createForeignKeys,
                    dropForeignKeys);
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
            AlterColumns = alterColumns,
            RenameColumns = renameColumns,
            DropColumns = dropColumns,
            CreateForeignKeys = createForeignKeys,
            DropForeignKeys = dropForeignKeys,
            Warnings = warnings,
            Errors = errors
        };
    }

    private IReadOnlyDictionary<string, IReadOnlyList<MendixDomainModelCreateForeignKeyPlanDto>> BuildDesiredForeignKeys(
        MendixDomainModelDocumentDto document,
        MendixDomainModelBindingDto binding,
        ICollection<string> warnings)
    {
        var result = new Dictionary<string, List<MendixDomainModelCreateForeignKeyPlanDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var association in document.Associations.Where(item => !string.Equals(item.BindingMode, "logicalCrossDb", StringComparison.OrdinalIgnoreCase)))
        {
            var sourceEntity = document.Entities.FirstOrDefault(entity => entity.EntityId == association.FromEntityId);
            var targetEntity = document.Entities.FirstOrDefault(entity => entity.EntityId == association.ToEntityId);
            if (sourceEntity is null || targetEntity is null)
            {
                continue;
            }

            if (!string.Equals(sourceEntity.BindingId, binding.BindingId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(sourceEntity.BindingId, targetEntity.BindingId, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"关系 {association.Name} 跨越不同数据库绑定，已降级为逻辑关系，不会创建物理外键。");
                continue;
            }

            var sourceColumn = association.JoinSpec?.SourceField ?? (association.SourceAttributeId is null ? null : sourceEntity.Attributes.FirstOrDefault(attribute => attribute.AttributeId == association.SourceAttributeId)?.ColumnName);
            var targetColumn = association.JoinSpec?.TargetField ?? (association.TargetAttributeId is null ? null : targetEntity.Attributes.FirstOrDefault(attribute => attribute.AttributeId == association.TargetAttributeId)?.ColumnName);
            if (string.IsNullOrWhiteSpace(sourceColumn) || string.IsNullOrWhiteSpace(targetColumn))
            {
                warnings.Add($"关系 {association.Name} 缺少源/目标字段映射，无法生成物理外键。");
                continue;
            }

            var key = BuildTableKey(sourceEntity.SchemaName, sourceEntity.TableName);
            if (!result.TryGetValue(key, out var list))
            {
                list = [];
                result[key] = list;
            }

            list.Add(new MendixDomainModelCreateForeignKeyPlanDto
            {
                BindingId = sourceEntity.BindingId,
                SchemaName = sourceEntity.SchemaName,
                TableName = sourceEntity.TableName,
                ForeignKeyName = association.Name,
                ReferencedTableName = targetEntity.TableName,
                ReferencedSchemaName = targetEntity.SchemaName,
                SourceColumns = [sourceColumn],
                ReferencedColumns = [targetColumn],
                OnDelete = "NO ACTION",
                OnUpdate = "NO ACTION"
            });
        }

        return result.ToDictionary(item => item.Key, item => (IReadOnlyList<MendixDomainModelCreateForeignKeyPlanDto>)item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static void AppendCreateForeignKeysForNewTable(
        MendixDomainModelEntityDto entity,
        IReadOnlyDictionary<string, IReadOnlyList<MendixDomainModelCreateForeignKeyPlanDto>> desiredForeignKeys,
        ICollection<MendixDomainModelCreateForeignKeyPlanDto> createForeignKeys)
    {
        foreach (var foreignKey in desiredForeignKeys.GetValueOrDefault(BuildTableKey(entity.SchemaName, entity.TableName)) ?? [])
        {
            createForeignKeys.Add(foreignKey);
        }
    }

    private void AnalyzeColumnDiffs(
        MendixDomainModelBindingDto binding,
        IDatabaseDialect dialect,
        MendixDomainModelEntityDto entity,
        IReadOnlyList<DatabaseColumnDto> existingColumns,
        ICollection<MendixDomainModelAddColumnPlanDto> addColumns,
        ICollection<MendixDomainModelAlterColumnPlanDto> alterColumns,
        ICollection<MendixDomainModelRenameColumnPlanDto> renameColumns,
        ICollection<MendixDomainModelDropColumnPlanDto> dropColumns,
        ICollection<string> warnings)
    {
        var columnsByName = existingColumns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
        var matchedExistingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamedFromByAttributeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in entity.Attributes)
        {
            if (columnsByName.ContainsKey(attribute.ColumnName))
            {
                continue;
            }

            var historicalColumnName = TryGetHistoricalColumnName(attribute);
            if (string.IsNullOrWhiteSpace(historicalColumnName) ||
                !columnsByName.ContainsKey(historicalColumnName) ||
                matchedExistingColumns.Contains(historicalColumnName))
            {
                continue;
            }

            renamedFromByAttributeId[attribute.AttributeId] = historicalColumnName;
            matchedExistingColumns.Add(historicalColumnName);
            renameColumns.Add(new MendixDomainModelRenameColumnPlanDto
            {
                BindingId = binding.BindingId,
                SchemaName = entity.SchemaName,
                TableName = entity.TableName,
                ColumnName = historicalColumnName,
                NewColumnName = attribute.ColumnName
            });
        }

        foreach (var attribute in entity.Attributes)
        {
            var desiredColumnName = attribute.ColumnName;
            DatabaseColumnDto? existingColumn = null;
            if (columnsByName.TryGetValue(desiredColumnName, out var exactColumn))
            {
                existingColumn = exactColumn;
                matchedExistingColumns.Add(desiredColumnName);
            }
            else if (renamedFromByAttributeId.TryGetValue(attribute.AttributeId, out var previousName) &&
                     columnsByName.TryGetValue(previousName, out var renamedColumn))
            {
                existingColumn = renamedColumn;
            }

            if (existingColumn is null)
            {
                addColumns.Add(new MendixDomainModelAddColumnPlanDto
                {
                    BindingId = binding.BindingId,
                    SchemaName = entity.SchemaName,
                    TableName = entity.TableName,
                    ColumnName = desiredColumnName
                });
                continue;
            }

            var desiredDataType = ToDatabaseType(attribute.Type, binding.DriverCode);
            if (!NeedsAlter(existingColumn, desiredDataType, attribute))
            {
                continue;
            }

            if (string.Equals(binding.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{entity.QualifiedName}.{attribute.ColumnName} 在 SQLite Draft 中存在类型/默认值/必填变更，当前方言不支持自动 ALTER COLUMN。");
                continue;
            }

            alterColumns.Add(new MendixDomainModelAlterColumnPlanDto
            {
                BindingId = binding.BindingId,
                SchemaName = entity.SchemaName,
                TableName = entity.TableName,
                ColumnName = attribute.ColumnName,
                DataType = desiredDataType,
                Nullable = !attribute.Required,
                PrimaryKey = attribute.PrimaryKey,
                DefaultValue = attribute.DefaultValue
            });
        }

        foreach (var column in existingColumns)
        {
            if (matchedExistingColumns.Contains(column.Name) ||
                columnsByName.ContainsKey(column.Name) && entity.Attributes.Any(attribute => string.Equals(attribute.ColumnName, column.Name, StringComparison.OrdinalIgnoreCase)) ||
                ManagedAuditColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            dropColumns.Add(new MendixDomainModelDropColumnPlanDto
            {
                BindingId = binding.BindingId,
                SchemaName = entity.SchemaName,
                TableName = entity.TableName,
                ColumnName = column.Name
            });
            warnings.Add($"{entity.QualifiedName}.{column.Name} 将从 Draft 中删除，请确认没有微流或外部依赖仍在使用该列。");
        }
    }

    private static void AnalyzeForeignKeys(
        MendixDomainModelEntityDto entity,
        IReadOnlyList<DatabaseForeignKeyDto> existingForeignKeys,
        IReadOnlyList<MendixDomainModelCreateForeignKeyPlanDto> desiredForeignKeys,
        ICollection<MendixDomainModelCreateForeignKeyPlanDto> createForeignKeys,
        ICollection<MendixDomainModelDropForeignKeyPlanDto> dropForeignKeys)
    {
        var desiredByName = desiredForeignKeys.ToDictionary(item => item.ForeignKeyName, StringComparer.OrdinalIgnoreCase);
        var matchedExistingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var desired in desiredForeignKeys)
        {
            var existing = existingForeignKeys.FirstOrDefault(item => string.Equals(item.Name, desired.ForeignKeyName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                createForeignKeys.Add(desired);
                continue;
            }

            matchedExistingNames.Add(existing.Name);
            if (ForeignKeyEquivalent(existing, desired))
            {
                continue;
            }

            dropForeignKeys.Add(new MendixDomainModelDropForeignKeyPlanDto
            {
                BindingId = desired.BindingId,
                SchemaName = entity.SchemaName,
                TableName = entity.TableName,
                ForeignKeyName = desired.ForeignKeyName
            });
            createForeignKeys.Add(desired);
        }

        foreach (var existing in existingForeignKeys)
        {
            if (matchedExistingNames.Contains(existing.Name) || desiredByName.ContainsKey(existing.Name))
            {
                continue;
            }

            dropForeignKeys.Add(new MendixDomainModelDropForeignKeyPlanDto
            {
                BindingId = entity.BindingId,
                SchemaName = entity.SchemaName,
                TableName = entity.TableName,
                ForeignKeyName = existing.Name
            });
        }
    }

    private static bool ForeignKeyEquivalent(DatabaseForeignKeyDto existing, MendixDomainModelCreateForeignKeyPlanDto desired)
        => existing.SourceColumns.SequenceEqual(desired.SourceColumns, StringComparer.OrdinalIgnoreCase)
           && string.Equals(existing.ReferencedTableName, desired.ReferencedTableName, StringComparison.OrdinalIgnoreCase)
           && string.Equals(existing.ReferencedSchema, desired.ReferencedSchemaName, StringComparison.OrdinalIgnoreCase)
           && existing.ReferencedColumns.SequenceEqual(desired.ReferencedColumns, StringComparer.OrdinalIgnoreCase)
           && string.Equals(existing.OnDelete ?? "NO ACTION", desired.OnDelete, StringComparison.OrdinalIgnoreCase)
           && string.Equals(existing.OnUpdate ?? "NO ACTION", desired.OnUpdate, StringComparison.OrdinalIgnoreCase);

    private static bool NeedsAlter(DatabaseColumnDto existingColumn, string desiredDataType, MendixDomainModelAttributeDto attribute)
    {
        if (!AreDataTypesEquivalent(existingColumn, desiredDataType))
        {
            return true;
        }

        if (existingColumn.Nullable == attribute.Required)
        {
            return true;
        }

        if (existingColumn.PrimaryKey != attribute.PrimaryKey)
        {
            return true;
        }

        return !string.Equals(NormalizeDefaultValue(existingColumn.DefaultValue), NormalizeDefaultValue(attribute.DefaultValue), StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreDataTypesEquivalent(DatabaseColumnDto existingColumn, string desiredDataType)
    {
        var existingCandidates = new[]
        {
            existingColumn.RawDataType,
            existingColumn.DataType
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(NormalizeDataTypeForComparison).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var normalizedDesired = NormalizeDataTypeForComparison(desiredDataType);
        return existingCandidates.Any(candidate => string.Equals(candidate, normalizedDesired, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDataTypeForComparison(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        var parenthesisIndex = normalized.IndexOf('(');
        if (parenthesisIndex >= 0)
        {
            normalized = normalized[..parenthesisIndex];
        }

        return normalized switch
        {
            "INT" => "INTEGER",
            "BOOL" => "BOOLEAN",
            "TIMESTAMP WITHOUT TIME ZONE" => "TIMESTAMP",
            _ => normalized
        };
    }

    private static string? NormalizeDefaultValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Trim('\'', '"').ToUpperInvariant();

    private static string? TryGetHistoricalColumnName(MendixDomainModelAttributeDto attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute.AttributeId))
        {
            return null;
        }

        var lastSeparator = attribute.AttributeId.LastIndexOf(':');
        if (lastSeparator < 0 || lastSeparator == attribute.AttributeId.Length - 1)
        {
            return null;
        }

        var candidate = attribute.AttributeId[(lastSeparator + 1)..];
        return string.Equals(candidate, attribute.ColumnName, StringComparison.OrdinalIgnoreCase) ? null : candidate;
    }

    private static string BuildTableKey(string schemaName, string tableName)
        => $"{schemaName}::{tableName}";

    private async Task<ResolvedDomainModelApp> ResolveAppAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(appId, out var parsedAppId) || parsedAppId <= 0)
        {
            throw new BusinessException("appId 无效。", ErrorCodes.ValidationError);
        }

        var tenantId = CurrentTenantId();
        var aiApp = await _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == parsedAppId)
            .FirstAsync(cancellationToken);
        if (aiApp is not null)
        {
            if (!long.TryParse(workspaceId, out var parsedWorkspaceId) || aiApp.WorkspaceId != parsedWorkspaceId)
            {
                throw new BusinessException("应用不属于当前工作区。", ErrorCodes.ValidationError);
            }

            return new ResolvedDomainModelApp(aiApp.TenantId, aiApp.Id);
        }

        var app = await _appDefinitionRepository.FindByIdAsync(tenantId, parsedAppId, cancellationToken)
            ?? throw new BusinessException("应用不存在。", ErrorCodes.NotFound);
        if (!string.Equals(app.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("应用不属于当前工作区。", ErrorCodes.ValidationError);
        }

        return new ResolvedDomainModelApp(app.TenantId, app.Id);
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

    private static string ToDatabaseType(string logicalType, string? driverCode = null)
        => NormalizeLogicalType(logicalType) switch
        {
            "integer" => string.Equals(driverCode, "MySql", StringComparison.OrdinalIgnoreCase) ? "INT" : "INTEGER",
            "long" => "BIGINT",
            "decimal" => "DECIMAL",
            "boolean" => "BOOLEAN",
            "dateTime" => string.Equals(driverCode, "PostgreSQL", StringComparison.OrdinalIgnoreCase) ? "TIMESTAMP" : "DATETIME",
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

    private sealed record ResolvedDomainModelApp(TenantId TenantId, long Id);
}
