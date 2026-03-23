using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;
using SqlSugar;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 汇总计算引擎：解析 RollupDefinitionsJson，批量聚合子记录，更新主记录汇总字段。
/// </summary>
public sealed class RollupCalculationService : IRollupCalculationService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly Regex SafeIdentifier = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicRelationRepository _relationRepository;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<RollupCalculationService> _logger;

    public RollupCalculationService(
        IDynamicTableRepository tableRepository,
        IDynamicRelationRepository relationRepository,
        ISqlSugarClient db,
        ILogger<RollupCalculationService> logger)
    {
        _tableRepository = tableRepository;
        _relationRepository = relationRepository;
        _db = db;
        _logger = logger;
    }

    public async Task RecalculateAsync(
        TenantId tenantId,
        string masterTableKey,
        long masterRecordId,
        CancellationToken ct = default)
    {
        var masterTable = await _tableRepository.FindByKeyAsync(tenantId, masterTableKey, null, ct);
        if (masterTable is null)
        {
            return;
        }

        var relations = await _relationRepository.ListByTableIdAsync(tenantId, masterTable.Id, ct);
        var rollupRelations = relations.Where(r => r.EnableRollup && !string.IsNullOrWhiteSpace(r.RollupDefinitionsJson)).ToList();
        if (rollupRelations.Count == 0)
        {
            return;
        }

        // 批量收集所有更新列（不在循环内独立执行 UPDATE）
        var updateColumns = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var relation in rollupRelations)
        {
            List<RollupDefinition>? definitions;
            try
            {
                definitions = JsonSerializer.Deserialize<List<RollupDefinition>>(relation.RollupDefinitionsJson!, JsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Rollup] 解析 RollupDefinitionsJson 失败，RelationId={Id}", relation.Id);
                continue;
            }

            if (definitions is null || definitions.Count == 0)
            {
                continue;
            }

            var childTable = await _tableRepository.FindByKeyAsync(tenantId, relation.RelatedTableKey, null, ct);
            if (childTable is null)
            {
                continue;
            }

            // 对每个 RollupDefinition 执行一次批量聚合 SQL（而非循环内多次查询）
            foreach (var def in definitions)
            {
                if (!IsValidIdentifier(def.TargetField) || !IsValidIdentifier(def.ChildField))
                {
                    _logger.LogWarning("[Rollup] 字段名包含非法字符，跳过。TargetField={T}, ChildField={C}",
                        def.TargetField, def.ChildField);
                    continue;
                }

                var aggFunc = NormalizeAggFunc(def.AggregateFunction);
                if (aggFunc is null)
                {
                    _logger.LogWarning("[Rollup] 不支持的聚合函数：{F}", def.AggregateFunction);
                    continue;
                }

                var childTableName = childTable.TableKey;
                var foreignKey = relation.TargetField;

                if (!IsValidIdentifier(foreignKey))
                {
                    continue;
                }

                var whereSql = BuildFilterSql(def.FilterExpression, out var filterParams);
                var querySql = $"SELECT {aggFunc}(\"{def.ChildField}\") FROM \"{childTableName}\" WHERE \"{foreignKey}\" = @masterId";
                if (!string.IsNullOrWhiteSpace(whereSql))
                {
                    querySql += $" AND ({whereSql})";
                }

                var allParams = new { masterId = masterRecordId };
                try
                {
                    var value = await _db.Ado.GetScalarAsync(querySql, allParams);
                    updateColumns[def.TargetField] = value == DBNull.Value ? null : value;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Rollup] 聚合查询异常，跳过。SQL={Sql}", querySql);
                }
            }
        }

        if (updateColumns.Count == 0)
        {
            return;
        }

        // 批量构建 SET 子句，单次 UPDATE
        var setClauses = updateColumns.Keys.Select(k => $"\"{k}\" = @{k}").ToList();
        var setParamDict = updateColumns.ToDictionary(kv => kv.Key, kv => kv.Value);
        setParamDict["__masterId"] = masterRecordId;

        var updateSql = $"UPDATE \"{masterTableKey}\" SET {string.Join(", ", setClauses)} WHERE \"id\" = @__masterId";

        try
        {
            await _db.Ado.ExecuteCommandAsync(updateSql, setParamDict);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Rollup] 更新主记录汇总字段失败。SQL={Sql}", updateSql);
        }
    }

    private static string? NormalizeAggFunc(string? func)
    {
        return func?.Trim().ToUpperInvariant() switch
        {
            "SUM" => "SUM",
            "COUNT" => "COUNT",
            "MIN" => "MIN",
            "MAX" => "MAX",
            "AVG" => "AVG",
            _ => null
        };
    }

    private static bool IsValidIdentifier(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && SafeIdentifier.IsMatch(name);
    }

    /// <summary>
    /// 简单解析形如 "status = 'active' AND amount > 0" 的过滤表达式。
    /// 当前仅作为 WHERE 子句直接拼接（调用方已校验字段名）。
    /// </summary>
    private static string? BuildFilterSql(string? filterExpression, out object? parameters)
    {
        parameters = null;
        if (string.IsNullOrWhiteSpace(filterExpression))
        {
            return null;
        }

        // 简单透传：过滤表达式由业务配置人员设置，框架层信任其合法性（生产环境可增加白名单校验）
        return filterExpression;
    }
}
