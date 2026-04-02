using Atlas.Application.BatchProcess.Models;

namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// 时间窗口分片器：按时间区间将数据划分为可并行处理的分片。
/// </summary>
public interface ITimeWindowSharder
{
    IReadOnlyList<ShardRange> ComputeShards(TimeWindowShardingRequest request);
}
