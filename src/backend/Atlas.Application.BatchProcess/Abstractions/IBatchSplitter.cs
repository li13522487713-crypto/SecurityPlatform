using Atlas.Application.BatchProcess.Models;

namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// 批次切分器：将分片内的数据按 BatchSize 切分为若干小批次。
/// </summary>
public interface IBatchSplitter
{
    IReadOnlyList<BatchSlice> Split(long totalCount, int batchSize);
}
