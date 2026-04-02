using Atlas.Application.BatchProcess.Models;

namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// 主键范围分片器：按主键区间将数据划分为可并行处理的分片。
/// </summary>
public interface IPrimaryKeyRangeSharder
{
    Task<IReadOnlyList<ShardRange>> ComputeShardsAsync(ShardingRequest request, CancellationToken cancellationToken);
}
