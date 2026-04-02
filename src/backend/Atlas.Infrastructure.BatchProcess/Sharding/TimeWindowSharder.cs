using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;

namespace Atlas.Infrastructure.BatchProcess.Sharding;

/// <summary>
/// 按时间窗口划分分片，适用于按时间范围处理的场景。
/// </summary>
public sealed class TimeWindowSharder : ITimeWindowSharder
{
    public IReadOnlyList<ShardRange> ComputeShards(TimeWindowShardingRequest request)
    {
        var shards = new List<ShardRange>();
        var current = request.StartTime;
        var index = 0;

        while (current < request.EndTime)
        {
            var windowEnd = current + request.WindowSize;
            if (windowEnd > request.EndTime)
            {
                windowEnd = request.EndTime;
            }

            var startStr = current.ToString("yyyy-MM-ddTHH:mm:ss");
            var endStr = windowEnd.ToString("yyyy-MM-ddTHH:mm:ss");

            shards.Add(new ShardRange
            {
                ShardIndex = index,
                ShardKey = $"{startStr}~{endStr}",
                RangeStart = startStr,
                RangeEnd = endStr,
                EstimatedCount = 0
            });

            current = windowEnd;
            index++;
        }

        return shards;
    }
}
