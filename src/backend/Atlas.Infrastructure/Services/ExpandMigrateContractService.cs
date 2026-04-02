using System.Text;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Expand / Migrate / Contract 三阶段迁移服务实现。
/// T02-14(模型), T02-15(Expand), T02-29(Migrate), T02-16(Contract)
/// </summary>
public sealed class ExpandMigrateContractService : IExpandMigrateContractService
{
    private readonly IDynamicTableRepository _tableRepo;
    private readonly IDynamicFieldRepository _fieldRepo;
    private readonly IDynamicSchemaMigrationRepository _migrationRepo;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ISqlSugarClient _db;

    public ExpandMigrateContractService(
        IDynamicTableRepository tableRepo,
        IDynamicFieldRepository fieldRepo,
        IDynamicSchemaMigrationRepository migrationRepo,
        IIdGeneratorAccessor idGenerator,
        ISqlSugarClient db)
    {
        _tableRepo = tableRepo;
        _fieldRepo = fieldRepo;
        _migrationRepo = migrationRepo;
        _idGenerator = idGenerator;
        _db = db;
    }

    public async Task<ExpandMigrateContractTask> CreateTaskAsync(
        TenantId tenantId,
        long userId,
        SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepo.FindByKeyAsync(tenantId, request.TableKey, null, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"Table '{request.TableKey}' not found.");

        var matrix = DatabaseCapabilityMatrix.ForDbType(table.DbType.ToString());
        var expandScripts = GenerateExpandScripts(request, table.TableKey, matrix);
        var migrateScripts = GenerateMigrateScripts(request, table.TableKey);
        var contractScripts = GenerateContractScripts(request, table.TableKey, matrix);

        var now = DateTimeOffset.UtcNow;
        var taskId = _idGenerator.NextId();
        var migration = new DynamicSchemaMigration(
            tenantId,
            table.Id,
            table.TableKey,
            "ExpandMigrateContract",
            string.Join("\n", expandScripts.Concat(migrateScripts).Concat(contractScripts)),
            string.Join("\n", contractScripts.Select(s => $"-- Rollback: {s}")),
            MigrationPhase.Pending.ToString(),
            userId,
            taskId,
            now);

        await _migrationRepo.AddAsync(migration, cancellationToken);

        return new ExpandMigrateContractTask(
            taskId,
            table.TableKey,
            MigrationPhase.Pending.ToString(),
            expandScripts,
            migrateScripts,
            contractScripts,
            now);
    }

    public async Task<MigrationPhaseResult> ExecuteExpandAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var migration = await _migrationRepo.GetByIdAsync(tenantId, taskId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "Migration task not found.");

        var scripts = ExtractPhaseScripts(migration.AppliedSql, "expand");
        return await ExecuteScriptsAsync(scripts, "Expand");
    }

    public async Task<MigrationPhaseResult> ExecuteMigrateAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var migration = await _migrationRepo.GetByIdAsync(tenantId, taskId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "Migration task not found.");

        var scripts = ExtractPhaseScripts(migration.AppliedSql, "migrate");
        return await ExecuteScriptsAsync(scripts, "Migrate");
    }

    public async Task<MigrationPhaseResult> ExecuteContractAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var migration = await _migrationRepo.GetByIdAsync(tenantId, taskId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "Migration task not found.");

        var scripts = ExtractPhaseScripts(migration.AppliedSql, "contract");
        return await ExecuteScriptsAsync(scripts, "Contract");
    }

    private async Task<MigrationPhaseResult> ExecuteScriptsAsync(
        IReadOnlyList<string> scripts,
        string phase)
    {
        var executed = new List<string>();
        try
        {
            foreach (var script in scripts)
            {
                if (string.IsNullOrWhiteSpace(script) || script.TrimStart().StartsWith("--"))
                {
                    continue;
                }

                await _db.Ado.ExecuteCommandAsync(script);
                executed.Add(script);
            }

            return new MigrationPhaseResult(phase, true, null, executed);
        }
        catch (Exception ex)
        {
            return new MigrationPhaseResult(phase, false, ex.Message, executed);
        }
    }

    private static IReadOnlyList<string> GenerateExpandScripts(
        SchemaCompatibilityCheckRequest request,
        string tableKey,
        DatabaseCapabilityMatrix matrix)
    {
        var scripts = new List<string>();
        if (request.AddFields is not null)
        {
            foreach (var field in request.AddFields)
            {
                var sqlType = MapFieldType(field.FieldType, field.Length, field.Precision, field.Scale, matrix);
                var nullable = field.AllowNull ? "NULL" : "NOT NULL";
                var dflt = field.DefaultValue is not null ? $" DEFAULT '{field.DefaultValue.Replace("'", "''")}'" : "";
                scripts.Add($"ALTER TABLE [{tableKey}] ADD COLUMN [{field.Name}] {sqlType} {nullable}{dflt};");
            }
        }

        if (request.AddIndexes is not null)
        {
            foreach (var index in request.AddIndexes)
            {
                var unique = index.IsUnique ? "UNIQUE " : "";
                var cols = string.Join(", ", index.Fields.Select(f => $"[{f}]"));
                scripts.Add($"CREATE {unique}INDEX [{index.Name}] ON [{tableKey}] ({cols});");
            }
        }

        return scripts;
    }

    private static IReadOnlyList<string> GenerateMigrateScripts(
        SchemaCompatibilityCheckRequest request,
        string tableKey)
    {
        var scripts = new List<string>();
        if (request.UpdateFields is not null)
        {
            foreach (var update in request.UpdateFields)
            {
                if (update.DefaultValue is not null)
                {
                    scripts.Add($"-- Backfill: UPDATE [{tableKey}] SET [{update.Name}] = '{update.DefaultValue.Replace("'", "''")}' WHERE [{update.Name}] IS NULL;");
                }
            }
        }
        return scripts;
    }

    private static IReadOnlyList<string> GenerateContractScripts(
        SchemaCompatibilityCheckRequest request,
        string tableKey,
        DatabaseCapabilityMatrix matrix)
    {
        var scripts = new List<string>();
        if (request.RemoveIndexes is not null)
        {
            foreach (var idxName in request.RemoveIndexes)
            {
                scripts.Add($"DROP INDEX [{idxName}];");
            }
        }

        if (request.RemoveFields is not null && matrix.SupportsDropColumn)
        {
            foreach (var fieldName in request.RemoveFields)
            {
                scripts.Add($"ALTER TABLE [{tableKey}] DROP COLUMN [{fieldName}];");
            }
        }

        return scripts;
    }

    private static IReadOnlyList<string> ExtractPhaseScripts(string allScripts, string phase)
    {
        var lines = allScripts.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return phase switch
        {
            "expand" => lines.Where(l => l.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase)
                || l.Contains("CREATE", StringComparison.OrdinalIgnoreCase)).ToList(),
            "migrate" => lines.Where(l => l.Contains("Backfill", StringComparison.OrdinalIgnoreCase)
                || l.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)).ToList(),
            "contract" => lines.Where(l => l.Contains("DROP", StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => Array.Empty<string>()
        };
    }

    private static string MapFieldType(string fieldType, int? length, int? precision, int? scale, DatabaseCapabilityMatrix matrix)
    {
        return fieldType switch
        {
            "Int" => "INTEGER",
            "Long" => "BIGINT",
            "Decimal" => precision.HasValue && scale.HasValue ? $"DECIMAL({precision},{scale})" : "DECIMAL(18,2)",
            "String" => length.HasValue ? $"NVARCHAR({length})" : "NVARCHAR(255)",
            "Text" => "TEXT",
            "Bool" => matrix.DbType == "Sqlite" ? "INTEGER" : "BIT",
            "DateTime" or "Date" or "Time" => matrix.DbType == "Sqlite" ? "TEXT" : "DATETIME2",
            "Guid" => matrix.DbType == "Sqlite" ? "TEXT" : "UNIQUEIDENTIFIER",
            _ => "TEXT"
        };
    }
}
