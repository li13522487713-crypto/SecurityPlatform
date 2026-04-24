using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

internal sealed record DbClause(string Field, string Operator, string Value, string Logic);

internal static class AiDatabaseNodeHelper
{
    public static IAiDatabaseService ResolveDatabaseService(NodeExecutionContext context)
    {
        return context.ServiceProvider.GetRequiredService<IAiDatabaseService>();
    }

    public static AiDatabaseRecordEnvironment ResolveEnvironment(NodeExecutionContext context)
        => context.DatabaseEnvironment;

    public static long ResolveDatabaseId(NodeExecutionContext context)
    {
        var id = context.GetConfigInt64("databaseInfoId", 0L);
        if (id <= 0)
        {
            id = context.GetConfigInt64("databaseId", 0L);
        }

        return id;
    }

    public static async Task<List<AiDatabaseRecordListItem>> LoadRecordItemsAsync(
        NodeExecutionContext context,
        long databaseId,
        CancellationToken cancellationToken,
        int pageSize)
    {
        var service = ResolveDatabaseService(context);
        var result = await service.GetRecordsAsync(
            context.TenantId,
            databaseId,
            1,
            Math.Clamp(pageSize, 1, 500_000),
            ResolveEnvironment(context),
            cancellationToken,
            context.UserId,
            context.ChannelId);
        return result.Items.ToList();
    }

    /// <param name="sqlTakeLimit">
    /// 仅在查询场景使用：对 SQL 结果集上限（按 Id 升序截取），防止超大表一次性加载。
    /// Update/Delete 应传 null 以加载策略下的全部候选行。
    /// </param>
    public static async Task<List<AiDatabaseRecord>> LoadRecordsAsync(
        ISqlSugarClient db,
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken,
        AiDatabaseAccessPolicy? policy = null,
        int? sqlTakeLimit = null)
    {
        var query = db.Queryable<AiDatabaseRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId);
        if (policy is { OwnerUserId: { } ownerVal })
        {
            query = query.Where(x => x.OwnerUserId == 0 || x.OwnerUserId == ownerVal);
        }
        if (policy is { ChannelId: { } channelVal } && !string.IsNullOrWhiteSpace(channelVal))
        {
            query = query.Where(x => x.ChannelId == string.Empty || x.ChannelId == channelVal);
        }

        if (sqlTakeLimit is > 0)
        {
            query = query.OrderBy(x => x.Id, OrderByType.Asc).Take(sqlTakeLimit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>D2：从执行上下文 + DB 读取构建访问策略。</summary>
    public static async Task<AiDatabaseAccessPolicy> ResolvePolicyAsync(
        ISqlSugarClient db,
        NodeExecutionContext context,
        long databaseId,
        CancellationToken cancellationToken)
    {
        var entity = await db.Queryable<AiDatabase>()
            .Where(x => x.TenantIdValue == context.TenantId.Value && x.Id == databaseId)
            .FirstAsync(cancellationToken);
        if (entity is null)
        {
            throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);
        }

        return AiDatabaseAccessPolicy.For(entity, context.UserId, context.ChannelId);
    }

    /// <summary>D3：加载数据库 schema JSON——给 Coercer 与节点表单使用。</summary>
    public static async Task<string?> LoadSchemaAsync(
        ISqlSugarClient db,
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken,
        IServiceProvider? serviceProvider = null)
    {
        var cacheKey = $"Atlas:AiDatabase:Schema:{tenantId.Value}:{databaseId}";
        if (serviceProvider?.GetService<IMemoryCache>() is { } cache &&
            cache.TryGetValue(cacheKey, out object? cachedObj) &&
            cachedObj is string cached &&
            !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        var entity = await db.Queryable<AiDatabase>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == databaseId)
            .Select(x => new { x.TableSchema })
            .FirstAsync(cancellationToken);
        var schema = entity?.TableSchema;
        if (serviceProvider?.GetService<IMemoryCache>() is { } mem &&
            !string.IsNullOrWhiteSpace(schema))
        {
            mem.Set(cacheKey, schema, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });
        }

        return schema;
    }

    public static List<DbClause> ResolveClauses(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "clauseGroup", out var raw) ||
            raw.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var clauses = new List<DbClause>();
        foreach (var item in raw.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var field = TryGet(item, "field");
            var op = TryGet(item, "op");
            var value = TryGet(item, "value");
            var logic = TryGet(item, "logic");
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op))
            {
                continue;
            }

            clauses.Add(new DbClause(field, op.Trim(), value ?? string.Empty, string.IsNullOrWhiteSpace(logic) ? "and" : logic.Trim()));
        }

        return clauses;
    }

    public static bool IsMatch(JsonElement record, IReadOnlyList<DbClause> clauses)
    {
        if (clauses.Count == 0)
        {
            return true;
        }

        var aggregate = true;
        var hasValue = false;
        foreach (var clause in clauses)
        {
            if (!TryGetProperty(record, clause.Field, out var current))
            {
                if (string.Equals(clause.Logic, "or", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return false;
            }

            var matched = Compare(current, clause.Operator, clause.Value);
            if (!hasValue)
            {
                aggregate = matched;
                hasValue = true;
                continue;
            }

            aggregate = string.Equals(clause.Logic, "or", StringComparison.OrdinalIgnoreCase)
                ? aggregate || matched
                : aggregate && matched;
        }

        return aggregate;
    }

    public static JsonElement ApplyFieldProjection(JsonElement record, IReadOnlyList<string> fields)
    {
        if (fields.Count == 0 || record.ValueKind != JsonValueKind.Object)
        {
            return record.Clone();
        }

        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (TryGetProperty(record, field, out var value))
            {
                map[field] = value.Clone();
            }
        }

        return JsonSerializer.SerializeToElement(map);
    }

    public static List<string> ResolveFields(IReadOnlyDictionary<string, JsonElement> config, string key = "queryFields")
    {
        if (!VariableResolver.TryGetConfigValue(config, key, out var raw))
        {
            return [];
        }

        if (raw.ValueKind == JsonValueKind.Array)
        {
            return raw.EnumerateArray()
                .Select(VariableResolver.ToDisplayText)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var text = VariableResolver.ToDisplayText(raw);
        return text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static JsonElement? ParseRecordJson(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    public static string MergeObjectJson(JsonElement original, IReadOnlyDictionary<string, JsonElement> updates)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (original.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in original.EnumerateObject())
            {
                map[p.Name] = p.Value.Clone();
            }
        }

        foreach (var kvp in updates)
        {
            map[kvp.Key] = kvp.Value.Clone();
        }

        return JsonSerializer.Serialize(map);
    }

    private static bool Compare(JsonElement left, string op, string rightText)
    {
        op = op.Trim().ToLowerInvariant();
        var right = VariableResolver.ParseLiteral(rightText);
        return op switch
        {
            "eq" or "==" => EqualsValue(left, right),
            "ne" or "!=" => !EqualsValue(left, right),
            "gt" or ">" => CompareNumber(left, right, (l, r) => l > r),
            "lt" or "<" => CompareNumber(left, right, (l, r) => l < r),
            "ge" or ">=" => CompareNumber(left, right, (l, r) => l >= r),
            "le" or "<=" => CompareNumber(left, right, (l, r) => l <= r),
            "contains" => VariableResolver.ToDisplayText(left).Contains(VariableResolver.ToDisplayText(right), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool CompareNumber(JsonElement left, JsonElement right, Func<double, double, bool> comparator)
    {
        if (!double.TryParse(VariableResolver.ToDisplayText(left), NumberStyles.Float, CultureInfo.InvariantCulture, out var l))
        {
            return false;
        }

        if (!double.TryParse(VariableResolver.ToDisplayText(right), NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
        {
            return false;
        }

        return comparator(l, r);
    }

    private static bool EqualsValue(JsonElement left, JsonElement right)
    {
        return string.Equals(
            VariableResolver.ToDisplayText(left),
            VariableResolver.ToDisplayText(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGet(JsonElement node, string key)
    {
        foreach (var p in node.EnumerateObject())
        {
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                return VariableResolver.ToDisplayText(p.Value);
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement record, string key, out JsonElement value)
    {
        value = default;
        if (record.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var p in record.EnumerateObject())
        {
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        return false;
    }
}
