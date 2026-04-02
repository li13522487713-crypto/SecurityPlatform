using System.Text;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// DDL 预览实现：生成 UP / DOWN 脚本 + 警告列表 + 能力矩阵对齐告警。
/// T02-12, T02-13, T02-30, T02-34
/// </summary>
public sealed class DdlPreviewService : IDdlPreviewService
{
    private readonly IDynamicTableRepository _tableRepo;
    private readonly IDynamicFieldRepository _fieldRepo;

    public DdlPreviewService(
        IDynamicTableRepository tableRepo,
        IDynamicFieldRepository fieldRepo)
    {
        _tableRepo = tableRepo;
        _fieldRepo = fieldRepo;
    }

    public async Task<DdlPreviewResult> PreviewAsync(
        TenantId tenantId,
        SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepo.FindByKeyAsync(tenantId, request.TableKey, null, cancellationToken);
        if (table is null)
        {
            return new DdlPreviewResult(request.TableKey, "-- Table not found", null,
                new[] { "Table does not exist" }, Array.Empty<DdlCapabilityWarning>());
        }

        var matrix = DatabaseCapabilityMatrix.ForDbType(table.DbType.ToString());
        var existingFields = await _fieldRepo.ListByTableIdAsync(tenantId, table.Id, cancellationToken);

        var upBuilder = new StringBuilder();
        var downBuilder = new StringBuilder();
        var warnings = new List<string>();
        var capabilityWarnings = new List<DdlCapabilityWarning>();

        GenerateAddFieldScripts(request, table.TableKey, matrix, upBuilder, downBuilder, warnings, capabilityWarnings);
        GenerateUpdateFieldScripts(request, table.TableKey, existingFields, matrix, upBuilder, downBuilder, warnings, capabilityWarnings);
        GenerateRemoveFieldScripts(request, table.TableKey, matrix, upBuilder, downBuilder, warnings, capabilityWarnings);
        GenerateIndexScripts(request, table.TableKey, matrix, upBuilder, downBuilder, warnings, capabilityWarnings);

        return new DdlPreviewResult(
            request.TableKey,
            upBuilder.Length > 0 ? upBuilder.ToString().TrimEnd() : "-- No changes",
            downBuilder.Length > 0 ? downBuilder.ToString().TrimEnd() : null,
            warnings,
            capabilityWarnings);
    }

    private static void GenerateAddFieldScripts(
        SchemaCompatibilityCheckRequest request,
        string tableKey,
        DatabaseCapabilityMatrix matrix,
        StringBuilder up,
        StringBuilder down,
        List<string> warnings,
        List<DdlCapabilityWarning> capWarnings)
    {
        if (request.AddFields is null) return;

        foreach (var field in request.AddFields)
        {
            var sqlType = MapFieldTypeToSql(field.FieldType, field.Length, field.Precision, field.Scale, matrix);
            var nullable = field.AllowNull ? "NULL" : "NOT NULL";
            var defaultClause = field.DefaultValue is not null ? $" DEFAULT {QuoteLiteral(field.DefaultValue)}" : "";

            up.AppendLine($"ALTER TABLE [{tableKey}] ADD COLUMN [{field.Name}] {sqlType} {nullable}{defaultClause};");
            down.AppendLine($"-- Rollback: ALTER TABLE [{tableKey}] DROP COLUMN [{field.Name}];");

            if (!field.AllowNull && field.DefaultValue is null)
            {
                warnings.Add($"Adding non-nullable field '{field.Name}' without default value may fail on existing data");
            }

            if (!matrix.SupportsAddColumnWithDefault && field.DefaultValue is not null)
            {
                capWarnings.Add(new DdlCapabilityWarning("AddColumnWithDefault", matrix.DbType,
                    $"Database does not support adding column with default in a single statement"));
            }
        }
    }

    private static void GenerateUpdateFieldScripts(
        SchemaCompatibilityCheckRequest request,
        string tableKey,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicField> existingFields,
        DatabaseCapabilityMatrix matrix,
        StringBuilder up,
        StringBuilder down,
        List<string> warnings,
        List<DdlCapabilityWarning> capWarnings)
    {
        if (request.UpdateFields is null) return;

        if (!matrix.SupportsAlterColumnType)
        {
            capWarnings.Add(new DdlCapabilityWarning("AlterColumnType", matrix.DbType,
                "Database does not support ALTER COLUMN TYPE; table recreation may be required"));
        }

        foreach (var update in request.UpdateFields)
        {
            var existing = existingFields.FirstOrDefault(f =>
                string.Equals(f.Name, update.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null) continue;

            if (update.AllowNull.HasValue && update.AllowNull != existing.AllowNull)
            {
                var newNullability = update.AllowNull.Value ? "NULL" : "NOT NULL";
                up.AppendLine($"-- ALTER TABLE [{tableKey}] ALTER COLUMN [{update.Name}] SET {newNullability};");
                down.AppendLine($"-- Rollback: restore original nullability for [{update.Name}]");
                if (!update.AllowNull.Value)
                {
                    warnings.Add($"Setting '{update.Name}' to NOT NULL requires all existing rows to have values");
                }
            }

            if (update.DefaultValue is not null)
            {
                up.AppendLine($"-- ALTER TABLE [{tableKey}] ALTER COLUMN [{update.Name}] SET DEFAULT {QuoteLiteral(update.DefaultValue)};");
                down.AppendLine($"-- Rollback: remove default for [{update.Name}]");
            }
        }
    }

    private static void GenerateRemoveFieldScripts(
        SchemaCompatibilityCheckRequest request,
        string tableKey,
        DatabaseCapabilityMatrix matrix,
        StringBuilder up,
        StringBuilder down,
        List<string> warnings,
        List<DdlCapabilityWarning> capWarnings)
    {
        if (request.RemoveFields is null) return;

        foreach (var fieldName in request.RemoveFields)
        {
            up.AppendLine($"ALTER TABLE [{tableKey}] DROP COLUMN [{fieldName}];");
            down.AppendLine($"-- Rollback: re-add column [{fieldName}] (manual type restore required)");
            warnings.Add($"Dropping column '{fieldName}' is irreversible and will permanently delete data");

            if (!matrix.SupportsDropColumn)
            {
                capWarnings.Add(new DdlCapabilityWarning("DropColumn", matrix.DbType,
                    $"Database does not support DROP COLUMN; table recreation required"));
            }
        }
    }

    private static void GenerateIndexScripts(
        SchemaCompatibilityCheckRequest request,
        string tableKey,
        DatabaseCapabilityMatrix matrix,
        StringBuilder up,
        StringBuilder down,
        List<string> warnings,
        List<DdlCapabilityWarning> capWarnings)
    {
        if (request.AddIndexes is not null)
        {
            foreach (var index in request.AddIndexes)
            {
                var unique = index.IsUnique ? "UNIQUE " : "";
                var fields = string.Join(", ", index.Fields.Select(f => $"[{f}]"));
                up.AppendLine($"CREATE {unique}INDEX [{index.Name}] ON [{tableKey}] ({fields});");
                down.AppendLine($"DROP INDEX [{index.Name}];");
            }
        }

        if (request.RemoveIndexes is not null)
        {
            foreach (var indexName in request.RemoveIndexes)
            {
                up.AppendLine($"DROP INDEX [{indexName}];");
                down.AppendLine($"-- Rollback: recreate index [{indexName}] (definition required)");
            }
        }
    }

    private static string MapFieldTypeToSql(string fieldType, int? length, int? precision, int? scale, DatabaseCapabilityMatrix matrix)
    {
        return fieldType switch
        {
            "Int" => "INTEGER",
            "Long" => "BIGINT",
            "Decimal" => precision.HasValue && scale.HasValue ? $"DECIMAL({precision},{scale})" : "DECIMAL(18,2)",
            "String" => length.HasValue ? $"NVARCHAR({length})" : "NVARCHAR(255)",
            "Text" => "TEXT",
            "Bool" => matrix.DbType == "Sqlite" ? "INTEGER" : "BIT",
            "DateTime" => matrix.DbType == "Sqlite" ? "TEXT" : "DATETIME2",
            "Date" => matrix.DbType == "Sqlite" ? "TEXT" : "DATE",
            "Time" => matrix.DbType == "Sqlite" ? "TEXT" : "TIME",
            "Guid" => matrix.DbType == "Sqlite" ? "TEXT" : "UNIQUEIDENTIFIER",
            "Json" => "TEXT",
            _ => "TEXT"
        };
    }

    private static string QuoteLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }
}
