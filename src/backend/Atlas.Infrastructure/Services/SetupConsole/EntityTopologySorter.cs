using System.Collections.Generic;
using System.Reflection;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 实体拓扑排序（M9/C1）。
///
/// 基于"命名约定"推断引用关系：
/// - 任何后缀为 <c>"Id"</c> 的 long 属性（除 <c>Id</c>、<c>TenantIdValue</c>、<c>CreatedBy</c>、<c>UpdatedBy</c> 等元数据）
///   被视为对 <c>{Prefix}</c> 实体的外键引用；
/// - 例如 <c>WorkspaceMember.WorkspaceId</c> → 引用 <c>Workspace</c> 实体；
/// - 类型名匹配做大小写不敏感比对。
///
/// 使用 Kahn 算法：
/// - 优先排出入度为 0 的实体；
/// - 环依赖（如 <c>Workspace ↔ WorkspaceMember</c>）按"剩余实体集合的入度最小者"按字典序兜底排序，
///   保证算法可终止；环依赖的真实约束由"先建表 + 全量插入再加外键"或"目标库临时关闭外键"运行时处理。
/// </summary>
public static class EntityTopologySorter
{
    private static readonly HashSet<string> IgnoredIdSuffixProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "TenantIdValue",
        "CreatedBy",
        "UpdatedBy",
        "CheckedBy",
        "AppliedBy",
        "AddedBy",
        "AssignedBy",
        "ApprovedBy",
        "RejectedBy",
        "DeletedBy",
        "BatchId", // 业务字段，非外键
        "RequestId",
        "TraceId"
    };

    /// <summary>
    /// 命名约定兜底后缀：当 "UserId" 去掉 Id 后得到 "User"，但实体名是 "UserAccount" 时，
    /// 尝试 "UserAccount" / "UserRecord" / "UserEntity" 等常见变体。
    /// </summary>
    private static readonly string[] NameFallbackSuffixes = new[]
    {
        "Account",
        "Record",
        "Entity",
        "Definition",
        "Config",
        "Info"
    };

    /// <summary>
    /// 给定一组实体类型，返回拓扑排序后的顺序：被引用方在前，引用方在后。
    /// </summary>
    public static IReadOnlyList<Type> Sort(IEnumerable<Type> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);
        var allTypes = entityTypes.Distinct().ToArray();
        if (allTypes.Length == 0)
        {
            return Array.Empty<Type>();
        }

        // 类名 -> Type 的查找索引（大小写不敏感）
        // 注意：`AtlasOrmSchemaCatalog.RuntimeEntities` 可能存在同名类跨 namespace 的情况，
        // 此处按命名空间字典序保留第一个，避免 ToDictionary 抛 duplicate key。
        var typeByName = allTypes
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => t.FullName, StringComparer.Ordinal).First(),
                StringComparer.OrdinalIgnoreCase);

        // 邻接表：source -> 它依赖的 targets（即 source 引用了 target，需先建 target）
        var dependsOn = allTypes.ToDictionary(t => t, _ => new HashSet<Type>());
        var inDegree = allTypes.ToDictionary(t => t, _ => 0);

        foreach (var type in allTypes)
        {
            var refs = ExtractReferencedEntityNames(type);
            foreach (var refName in refs)
            {
                var targetType = ResolveReferencedType(refName, typeByName);
                if (targetType is null || targetType == type)
                {
                    continue;
                }
                if (dependsOn[type].Add(targetType))
                {
                    inDegree[type] += 1;
                }
            }
        }

        var sorted = new List<Type>(allTypes.Length);
        // 优先 Kahn：入度为 0 的进队列
        var queue = new SortedSet<Type>(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name)));
        foreach (var type in allTypes.Where(t => inDegree[t] == 0))
        {
            queue.Add(type);
        }

        while (queue.Count > 0)
        {
            var head = queue.Min!;
            queue.Remove(head);
            sorted.Add(head);

            // 删除 head 出边：所有"依赖于 head"的 source 入度 -1
            foreach (var candidate in allTypes)
            {
                if (dependsOn[candidate].Remove(head))
                {
                    inDegree[candidate] -= 1;
                    if (inDegree[candidate] == 0)
                    {
                        queue.Add(candidate);
                    }
                }
            }
        }

        // 处理环依赖：按入度从小到大 + 字典序补充剩下的
        if (sorted.Count < allTypes.Length)
        {
            var remaining = allTypes.Except(sorted)
                .OrderBy(t => inDegree[t])
                .ThenBy(t => t.Name, StringComparer.Ordinal);
            sorted.AddRange(remaining);
        }

        return sorted;
    }

    /// <summary>
    /// 仅返回拓扑排序中"实际有依赖"的边（用于诊断 / 测试）。
    /// </summary>
    public static IReadOnlyDictionary<Type, IReadOnlyList<Type>> BuildDependencyGraph(IEnumerable<Type> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);
        var allTypes = entityTypes.Distinct().ToArray();
        var typeByName = allTypes
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => t.FullName, StringComparer.Ordinal).First(),
                StringComparer.OrdinalIgnoreCase);
        var graph = new Dictionary<Type, IReadOnlyList<Type>>();
        foreach (var type in allTypes)
        {
            var refs = ExtractReferencedEntityNames(type);
            var resolved = refs
                .Select(name => ResolveReferencedType(name, typeByName))
                .Where(t => t is not null && t != type)
                .Cast<Type>()
                .Distinct()
                .ToList();
            graph[type] = resolved;
        }
        return graph;
    }

    private static Type? ResolveReferencedType(string referencedName, IReadOnlyDictionary<string, Type> typeByName)
    {
        if (typeByName.TryGetValue(referencedName, out var exact))
        {
            return exact;
        }
        // 兜底：User → UserAccount / UserRecord 等常见后缀
        foreach (var suffix in NameFallbackSuffixes)
        {
            var candidateName = referencedName + suffix;
            if (typeByName.TryGetValue(candidateName, out var fallback))
            {
                return fallback;
            }
        }
        return null;
    }

    private static IEnumerable<string> ExtractReferencedEntityNames(Type entityType)
    {
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (IgnoredIdSuffixProperties.Contains(prop.Name))
            {
                continue;
            }
            if (!prop.Name.EndsWith("Id", StringComparison.Ordinal) || prop.Name.Length <= 2)
            {
                continue;
            }
            // 只看 long / long? 类型，避免误判 Guid TenantIdValue 等
            var unwrappedType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (unwrappedType != typeof(long))
            {
                continue;
            }
            // SqlSugar 显式忽略列也跳过
            var sugar = prop.GetCustomAttribute<SugarColumn>();
            if (sugar?.IsIgnore == true)
            {
                continue;
            }
            // 引用名 = 属性名去掉 Id 后缀
            var referencedName = prop.Name[..^2];
            if (string.IsNullOrEmpty(referencedName))
            {
                continue;
            }
            yield return referencedName;
        }
    }
}
