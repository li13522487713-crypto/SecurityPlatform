using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;

namespace Atlas.Infrastructure.BatchProcess.Splitting;

/// <summary>
/// 将分片内数据按 batchSize 切分为顺序小批次。
/// </summary>
public sealed class BatchSplitter : IBatchSplitter
{
    public IReadOnlyList<BatchSlice> Split(long totalCount, int batchSize)
    {
        if (totalCount <= 0 || batchSize <= 0) return [];

        var slices = new List<BatchSlice>();
        long offset = 0;
        int index = 0;

        while (offset < totalCount)
        {
            var count = (int)Math.Min(batchSize, totalCount - offset);
            slices.Add(new BatchSlice
            {
                BatchIndex = index,
                Offset = offset,
                Count = count
            });
            offset += count;
            index++;
        }

        return slices;
    }
}
