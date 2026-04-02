using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 聚合 5 类兼容性检测 + 高风险预警的 Schema 兼容性检查器。
/// T02-07 ~ T02-11, T02-32, T02-33
/// </summary>
public sealed class SchemaCompatibilityChecker : ISchemaCompatibilityChecker
{
    private readonly IDynamicTableRepository _tableRepo;
    private readonly IDynamicFieldRepository _fieldRepo;
    private readonly IDynamicIndexRepository _indexRepo;
    private readonly IDynamicRelationRepository _relationRepo;

    public SchemaCompatibilityChecker(
        IDynamicTableRepository tableRepo,
        IDynamicFieldRepository fieldRepo,
        IDynamicIndexRepository indexRepo,
        IDynamicRelationRepository relationRepo)
    {
        _tableRepo = tableRepo;
        _fieldRepo = fieldRepo;
        _indexRepo = indexRepo;
        _relationRepo = relationRepo;
    }

    public async Task<SchemaCompatibilityResult> CheckAsync(
        TenantId tenantId,
        SchemaCompatibilityCheckRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepo.FindByKeyAsync(tenantId, request.TableKey, null, cancellationToken);
        if (table is null)
        {
            return new SchemaCompatibilityResult(false,
                new[] { new CompatibilityIssue("General", "Error", request.TableKey, "Table not found", null) },
                Array.Empty<HighRiskWarning>());
        }

        var existingFields = await _fieldRepo.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var existingIndexes = await _indexRepo.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var existingRelations = await _relationRepo.ListByTableIdAsync(tenantId, table.Id, cancellationToken);

        var matrix = DatabaseCapabilityMatrix.ForDbType(table.DbType.ToString());
        var issues = new List<CompatibilityIssue>();
        var warnings = new List<HighRiskWarning>();

        CheckNameConflicts(request, existingFields, existingIndexes, matrix, issues);
        CheckTypeCompatibility(request, existingFields, matrix, issues);
        CheckIndexAndForeignKeyImpact(request, existingFields, existingIndexes, existingRelations, issues);
        CheckHighRiskChanges(request, existingFields, warnings);
        CheckFunctionDependencyImpact(request, existingFields, issues);
        CheckLogicFlowDependencyImpact(request, existingFields, issues);

        return new SchemaCompatibilityResult(issues.Count == 0, issues, warnings);
    }

    /// <summary>T02-08: 名称冲突检测</summary>
    private static void CheckNameConflicts(
        SchemaCompatibilityCheckRequest request,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicField> existingFields,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicIndex> existingIndexes,
        DatabaseCapabilityMatrix matrix,
        List<CompatibilityIssue> issues)
    {
        var fieldNames = new HashSet<string>(existingFields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
        var indexNames = new HashSet<string>(existingIndexes.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);

        if (request.AddFields is not null)
        {
            foreach (var field in request.AddFields)
            {
                if (fieldNames.Contains(field.Name))
                {
                    issues.Add(new CompatibilityIssue("NameConflict", "Error", field.Name,
                        $"Field '{field.Name}' already exists", "Rename the field or remove the conflicting one first"));
                }

                if (field.Name.Length > matrix.MaxIdentifierLength)
                {
                    issues.Add(new CompatibilityIssue("NameConflict", "Error", field.Name,
                        $"Field name exceeds max identifier length ({matrix.MaxIdentifierLength})", "Shorten the name"));
                }
            }
        }

        if (request.AddIndexes is not null)
        {
            foreach (var index in request.AddIndexes)
            {
                if (indexNames.Contains(index.Name))
                {
                    issues.Add(new CompatibilityIssue("NameConflict", "Error", index.Name,
                        $"Index '{index.Name}' already exists", "Use a different index name"));
                }
            }
        }
    }

    /// <summary>T02-09: 类型兼容检测</summary>
    private static void CheckTypeCompatibility(
        SchemaCompatibilityCheckRequest request,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicField> existingFields,
        DatabaseCapabilityMatrix matrix,
        List<CompatibilityIssue> issues)
    {
        if (request.UpdateFields is null)
        {
            return;
        }

        var fieldMap = existingFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var update in request.UpdateFields)
        {
            if (!fieldMap.TryGetValue(update.Name, out var existing))
            {
                issues.Add(new CompatibilityIssue("TypeCompatibility", "Error", update.Name,
                    $"Field '{update.Name}' does not exist", "Add it instead of updating"));
                continue;
            }

            if (update.AllowNull == false && existing.AllowNull)
            {
                issues.Add(new CompatibilityIssue("TypeCompatibility", "Warning", update.Name,
                    "Changing from nullable to non-nullable may fail if existing data contains NULLs",
                    "Ensure all existing data has values before applying"));
            }

            if (update.Length.HasValue && existing.Length.HasValue && update.Length < existing.Length)
            {
                issues.Add(new CompatibilityIssue("TypeCompatibility", "Warning", update.Name,
                    $"Reducing length from {existing.Length} to {update.Length} may truncate data",
                    "Verify no data exceeds the new length"));
            }

            if (!matrix.SupportsAlterColumnType)
            {
                issues.Add(new CompatibilityIssue("TypeCompatibility", "Warning", update.Name,
                    $"The target database ({matrix.DbType}) does not support ALTER COLUMN TYPE",
                    "May require table recreation"));
            }
        }
    }

    /// <summary>T02-10: 索引与外键影响检测</summary>
    private static void CheckIndexAndForeignKeyImpact(
        SchemaCompatibilityCheckRequest request,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicField> existingFields,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicIndex> existingIndexes,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicRelation> existingRelations,
        List<CompatibilityIssue> issues)
    {
        if (request.RemoveFields is null)
        {
            return;
        }

        var removingFields = new HashSet<string>(request.RemoveFields, StringComparer.OrdinalIgnoreCase);

        foreach (var index in existingIndexes)
        {
            var indexFieldsJson = index.FieldsJson ?? "[]";
            foreach (var field in removingFields)
            {
                if (indexFieldsJson.Contains(field, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new CompatibilityIssue("IndexImpact", "Error", index.Name,
                        $"Index '{index.Name}' references field '{field}' being removed",
                        "Remove the index first or update it"));
                }
            }
        }

        foreach (var relation in existingRelations)
        {
            if (removingFields.Contains(relation.SourceField))
            {
                issues.Add(new CompatibilityIssue("ForeignKeyImpact", "Error", relation.SourceField,
                    $"Relation to '{relation.RelatedTableKey}' uses field '{relation.SourceField}' being removed",
                    "Remove the relation first"));
            }
        }
    }

    /// <summary>T02-11: 高风险变更预警</summary>
    private static void CheckHighRiskChanges(
        SchemaCompatibilityCheckRequest request,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicField> existingFields,
        List<HighRiskWarning> warnings)
    {
        if (request.RemoveFields is not null)
        {
            var fieldMap = existingFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var fieldName in request.RemoveFields)
            {
                if (fieldMap.TryGetValue(fieldName, out var field))
                {
                    if (field.IsPrimaryKey)
                    {
                        warnings.Add(new HighRiskWarning("RemovePrimaryKey", fieldName,
                            $"Removing primary key field '{fieldName}' is a destructive operation",
                            "Critical"));
                    }
                    else
                    {
                        warnings.Add(new HighRiskWarning("RemoveField", fieldName,
                            $"Removing field '{fieldName}' will permanently delete data in this column",
                            "High"));
                    }
                }
            }
        }

        if (request.UpdateFields is not null)
        {
            var fieldMap = existingFields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var update in request.UpdateFields)
            {
                if (fieldMap.TryGetValue(update.Name, out var existing) && existing.IsUnique && update.IsUnique == false)
                {
                    warnings.Add(new HighRiskWarning("RemoveUniqueConstraint", update.Name,
                        $"Removing unique constraint on '{update.Name}' may allow duplicate values",
                        "Medium"));
                }
            }
        }
    }

    /// <summary>T02-32: 函数依赖影响检测（占位 — 待 Track-03 T03-16 FunctionDefinition 就绪后联调）</summary>
    private static void CheckFunctionDependencyImpact(
        SchemaCompatibilityCheckRequest request,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicField> existingFields,
        List<CompatibilityIssue> issues)
    {
        if (request.RemoveFields is null)
        {
            return;
        }

        foreach (var fieldName in request.RemoveFields)
        {
            var field = existingFields.FirstOrDefault(f =>
                string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
            if (field is not null && field.IsComputed && field.ComputedExprId.HasValue)
            {
                issues.Add(new CompatibilityIssue("FunctionDependency", "Warning", fieldName,
                    $"Field '{fieldName}' is a computed field (expr: {field.ComputedExprId}); " +
                    "removing it may affect dependent function definitions",
                    "Review function definitions referencing this field"));
            }
        }
    }

    /// <summary>T02-33: 逻辑流依赖影响检测（占位 — 待 Track-05 T05-01 LogicFlowDefinition 就绪后联调）</summary>
    private static void CheckLogicFlowDependencyImpact(
        SchemaCompatibilityCheckRequest request,
        IReadOnlyList<Domain.DynamicTables.Entities.DynamicField> existingFields,
        List<CompatibilityIssue> issues)
    {
        if (request.RemoveFields is null)
        {
            return;
        }

        foreach (var fieldName in request.RemoveFields)
        {
            var field = existingFields.FirstOrDefault(f =>
                string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
            if (field is not null && field.IsStatusField)
            {
                issues.Add(new CompatibilityIssue("LogicFlowDependency", "Warning", fieldName,
                    $"Field '{fieldName}' is a status field, likely referenced by logic flow definitions",
                    "Review logic flow definitions before removing"));
            }
        }
    }
}
