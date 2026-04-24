using System.Text.Json;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

public sealed class DataMigrationPlanner : IDataMigrationPlanner
{
    private static readonly HashSet<string> DefaultExcludedEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(DataMigrationJob),
        nameof(DataMigrationBatch),
        nameof(DataMigrationCheckpoint),
        nameof(DataMigrationTableProgress),
        nameof(DataMigrationLog),
        nameof(DataMigrationReport),
        nameof(SetupConsoleToken),
        nameof(SetupSeedBundleLog)
    };

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<DataMigrationPlanner> _logger;

    public DataMigrationPlanner(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGen,
        ITenantProvider tenantProvider,
        ILogger<DataMigrationPlanner> logger)
    {
        _db = db;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<DataMigrationPlan> PlanAsync(
        DataMigrationJob job,
        ResolvedMigrationConnection source,
        ResolvedMigrationConnection target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var selectedEntities = DeserializeList(job.SelectedEntitiesJson);
        var selectedTables = DeserializeList(job.SelectedTablesJson);
        var excludedEntities = DeserializeList(job.ExcludedEntitiesJson);
        var excludedTables = DeserializeList(job.ExcludedTablesJson);
        var scopedEntities = ResolveScopedEntityNames(job.ModuleScopeJson);

        if (!job.MigrateSystemTables)
        {
            foreach (var entityName in DefaultExcludedEntities)
            {
                excludedEntities.Add(entityName);
            }
        }

        var units = source.Tables.Count > 0
            ? BuildRawTableUnits(source, selectedTables, excludedTables)
            : BuildEntityUnits(scopedEntities, selectedEntities, excludedEntities, selectedTables, excludedTables);

        var items = new List<DataMigrationPlanItem>();
        var unsupportedTables = new List<string>();
        var targetNonEmptyTables = new List<string>();
        var missingTargetTables = new List<string>();
        var warnings = new List<string>();
        long totalRows = 0;
        var estimatedBatches = 0;

        using var sourceScope = MigrationSqlSugarScopeFactory.Create(source.ConnectionString, source.DbType);
        using var targetScope = MigrationSqlSugarScopeFactory.Create(target.ConnectionString, target.DbType);

        foreach (var unit in units)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long sourceRows;
            long targetRowsBefore;
            var targetTableExists = targetScope.DbMaintenance.IsAnyTable(unit.TableName, false);
            try
            {
                sourceRows = await CountRowsAsync(sourceScope, source.DbType, unit, cancellationToken).ConfigureAwait(false);
                targetRowsBefore = targetTableExists
                    ? await CountRowsAsync(targetScope, target.DbType, unit, cancellationToken).ConfigureAwait(false)
                    : 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to precheck migration table {TableName}", unit.TableName);
                unsupportedTables.Add(unit.TableName);
                warnings.Add($"{unit.TableName}: {ex.Message}");
                continue;
            }

            if (!unit.SupportsResume)
            {
                warnings.Add($"{unit.TableName}: unsupported for safe resume because no stable keyset column was found.");
            }

            if (targetRowsBefore > 0)
            {
                targetNonEmptyTables.Add(unit.TableName);
            }

            if (!targetTableExists)
            {
                missingTargetTables.Add(unit.TableName);
            }

            var totalBatchCount = sourceRows <= 0
                ? 0
                : (int)Math.Ceiling(sourceRows / (double)Math.Max(1, job.BatchSize));

            var item = new DataMigrationPlanItem(
                unit.EntityName,
                unit.TableName,
                unit.KeyColumn,
                unit.SupportsResume,
                unit.EntityType,
                unit.Kind,
                sourceRows,
                targetRowsBefore,
                totalBatchCount);
            items.Add(item);
            totalRows += sourceRows;
            estimatedBatches += totalBatchCount;
        }

        await UpsertTableProgressAsync(job, items, cancellationToken).ConfigureAwait(false);

        return new DataMigrationPlan(
            items,
            totalRows,
            items.Count,
            estimatedBatches,
            unsupportedTables,
            targetNonEmptyTables,
            missingTargetTables,
            warnings);
    }

    private static IReadOnlyList<ResolvedMigrationTable> BuildRawTableUnits(
        ResolvedMigrationConnection source,
        IReadOnlySet<string> selectedTables,
        IReadOnlySet<string> excludedTables)
    {
        return source.Tables
            .Where(item => selectedTables.Count == 0 || selectedTables.Contains(item.TableName))
            .Where(item => !excludedTables.Contains(item.TableName))
            .ToArray();
    }

    private IReadOnlyList<ResolvedMigrationTable> BuildEntityUnits(
        IReadOnlySet<string> scopedEntities,
        IReadOnlySet<string> selectedEntities,
        IReadOnlySet<string> excludedEntities,
        IReadOnlySet<string> selectedTables,
        IReadOnlySet<string> excludedTables)
    {
        var sortedEntities = EntityTopologySorter.Sort(AtlasOrmSchemaCatalog.RuntimeEntities);
        var units = new List<ResolvedMigrationTable>();
        foreach (var entityType in sortedEntities)
        {
            var entityName = entityType.Name;
            if (scopedEntities.Count > 0 && !scopedEntities.Contains(entityName))
            {
                continue;
            }

            if (selectedEntities.Count > 0 && !selectedEntities.Contains(entityName))
            {
                continue;
            }

            if (excludedEntities.Contains(entityName))
            {
                continue;
            }

            var entityInfo = _db.EntityMaintenance.GetEntityInfo(entityType);
            var tableName = entityInfo.DbTableName;
            if (selectedTables.Count > 0 && !selectedTables.Contains(tableName))
            {
                continue;
            }

            if (excludedTables.Contains(tableName))
            {
                continue;
            }

            var primaryColumn = entityInfo.Columns.FirstOrDefault(column => column.IsPrimarykey)
                ?? entityInfo.Columns.FirstOrDefault(column => string.Equals(column.DbColumnName, "Id", StringComparison.OrdinalIgnoreCase));
            units.Add(new ResolvedMigrationTable(
                entityName,
                tableName,
                primaryColumn?.PropertyName ?? "Id",
                primaryColumn is not null,
                entityType,
                "entity"));
        }

        return units;
    }

    private async Task UpsertTableProgressAsync(
        DataMigrationJob job,
        IReadOnlyList<DataMigrationPlanItem> items,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId().Value;
        var existing = await _db.Queryable<DataMigrationTableProgress>()
            .Where(x => x.TenantIdValue == tenantId && x.JobId == job.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingMap = existing.ToDictionary(x => x.TableName, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var item in items)
        {
            if (existingMap.TryGetValue(item.TableName, out var record))
            {
                record.ResetForRetry(item.SourceRows, item.TargetRowsBefore, job.BatchSize, item.TotalBatchCount, now);
                await _db.Updateable(record).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var created = new DataMigrationTableProgress(
                _tenantProvider.GetTenantId(),
                _idGen.NextId(),
                job.Id,
                item.EntityName,
                item.TableName,
                job.BatchSize,
                item.TotalBatchCount,
                item.SourceRows,
                item.TargetRowsBefore,
                now);
            await _db.Insertable(created).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<long> CountRowsAsync(
        ISqlSugarClient scope,
        string dbType,
        ResolvedMigrationTable unit,
        CancellationToken cancellationToken)
    {
        if (unit.Kind == "entity" && unit.EntityType is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CountEntity(scope, unit.EntityType);
        }

        if (!scope.DbMaintenance.IsAnyTable(unit.TableName, false))
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await scope.Ado.GetScalarAsync(MigrationSqlSugarScopeFactory.BuildCountSql(dbType, unit.TableName))
            .ConfigureAwait(false);
        return Convert.ToInt64(result ?? 0);
    }

    private static HashSet<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<List<string>>(json) is { Count: > 0 } items
            ? new HashSet<string>(items.Where(item => !string.IsNullOrWhiteSpace(item)), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ResolveScopedEntityNames(string? moduleScopeJson)
    {
        if (string.IsNullOrWhiteSpace(moduleScopeJson))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var scope = JsonSerializer.Deserialize<DataMigrationModuleScopeDto>(moduleScopeJson);
        if (scope is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var explicitEntities = scope.EntityNames?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (explicitEntities is { Count: > 0 })
        {
            return explicitEntities;
        }

        if (scope.Categories.Count == 0 || scope.Categories.Contains("all", StringComparer.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var rules = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["system-foundation"] = new[] { "Atlas.Domain.System", "Atlas.Domain.Events", "Atlas.Domain.Setup" },
            ["identity-permission"] = new[] { "Atlas.Domain.Identity", "Atlas.Domain.Platform.Entities.AppMembershipEntities", "Atlas.Domain.Platform.Entities.AppOrgEntities" },
            ["workspace"] = new[] { "Atlas.Domain.AiPlatform.Entities" },
            ["business-domain"] = new[] { "Atlas.Domain.AiPlatform", "Atlas.Domain.Approval", "Atlas.Domain.AgentTeam", "Atlas.Domain.LogicFlow", "Atlas.Domain.BatchProcess", "Atlas.Domain.Workflow" },
            ["resource-runtime"] = new[] { "Atlas.Domain.Plugins", "Atlas.Domain.Templates", "Atlas.Domain.Integration", "Atlas.Domain.License", "Atlas.Domain.Assets", "Atlas.Domain.Platform" },
            ["audit-log"] = new[] { "Atlas.Domain.Audit", "Atlas.Domain.Alert" }
        };

        var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in scope.Categories)
        {
            if (!rules.TryGetValue(category, out var prefixes))
            {
                continue;
            }

            foreach (var entityType in AtlasOrmSchemaCatalog.RuntimeEntities)
            {
                if (entityType.Namespace is not null && prefixes.Any(prefix => entityType.Namespace.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    entities.Add(entityType.Name);
                }
            }
        }

        return entities;
    }

    private static long CountEntity(ISqlSugarClient scope, Type entityType)
    {
        var queryableMethod = scope.GetType().GetMethods()
            .First(method => method.Name == "Queryable" && method.IsGenericMethod && method.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null)
            ?? throw new InvalidOperationException($"failed to build queryable for entity {entityType.Name}");
        var countMethod = queryable.GetType().GetMethods()
            .First(method => method.Name == "Count" && method.GetParameters().Length == 0);
        return Convert.ToInt64(countMethod.Invoke(queryable, null) ?? 0);
    }
}
