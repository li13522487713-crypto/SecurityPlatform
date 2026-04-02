using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using SqlSugar;

namespace Atlas.Infrastructure.BatchProcess.Sharding;

/// <summary>
/// 按主键区间均匀划分分片，适用于自增 ID 或雪花 ID 场景。
/// </summary>
public sealed class PrimaryKeyRangeSharder : IPrimaryKeyRangeSharder
{
    private readonly ISqlSugarClient _db;

    public PrimaryKeyRangeSharder(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ShardRange>> ComputeShardsAsync(ShardingRequest request, CancellationToken cancellationToken)
    {
        var table = SanitizeIdentifier(request.TableName);
        var keyCol = SanitizeIdentifier(request.KeyColumn);

        var filterClause = string.IsNullOrEmpty(request.FilterExpression)
            ? string.Empty
            : $" WHERE {request.FilterExpression}";

        var statsSql = $"SELECT MIN({keyCol}) as MinKey, MAX({keyCol}) as MaxKey, COUNT(*) as Total FROM {table}{filterClause}";
        var stats = await _db.Ado.SqlQueryAsync<RangeStats>(statsSql, cancellationToken);

        if (stats.Count == 0 || stats[0].Total == 0)
        {
            return [];
        }

        var minKey = stats[0].MinKey;
        var maxKey = stats[0].MaxKey;
        var totalCount = stats[0].Total;

        var desiredShards = Math.Max(1, Math.Min(request.DesiredShardCount, (int)totalCount));

        if (!long.TryParse(minKey, out var min) || !long.TryParse(maxKey, out var max))
        {
            return [new ShardRange
            {
                ShardIndex = 0,
                ShardKey = $"{minKey}~{maxKey}",
                RangeStart = minKey,
                RangeEnd = maxKey,
                EstimatedCount = totalCount
            }];
        }

        var rangeSize = (max - min + 1) / desiredShards;
        if (rangeSize < 1) rangeSize = 1;

        var shards = new List<ShardRange>();
        for (int i = 0; i < desiredShards; i++)
        {
            var start = min + i * rangeSize;
            var end = (i == desiredShards - 1) ? max : start + rangeSize - 1;

            shards.Add(new ShardRange
            {
                ShardIndex = i,
                ShardKey = $"{start}~{end}",
                RangeStart = start.ToString(),
                RangeEnd = end.ToString(),
                EstimatedCount = totalCount / desiredShards
            });
        }

        return shards;
    }

    private static string SanitizeIdentifier(string identifier)
    {
        return identifier.Replace("\"", "").Replace("'", "").Replace(";", "");
    }

    private sealed class RangeStats
    {
        public string MinKey { get; set; } = string.Empty;
        public string MaxKey { get; set; } = string.Empty;
        public long Total { get; set; }
    }
}
