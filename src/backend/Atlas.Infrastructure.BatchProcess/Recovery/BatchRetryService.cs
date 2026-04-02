using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.BatchProcess.Recovery;

/// <summary>
/// 批次级重试服务：对 JobExecution 下所有失败分片发起重试。
/// </summary>
public sealed class BatchRetryService : IBatchRetryService
{
    private readonly IBatchJobRepository _repository;
    private readonly IShardRecoveryService _shardRecovery;
    private readonly ILogger<BatchRetryService> _logger;

    public BatchRetryService(
        IBatchJobRepository repository,
        IShardRecoveryService shardRecovery,
        ILogger<BatchRetryService> logger)
    {
        _repository = repository;
        _shardRecovery = shardRecovery;
        _logger = logger;
    }

    public async Task<RetryResult> RetryFailedShardsAsync(long jobExecutionId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var failedShards = await _repository.GetFailedShardsByExecutionIdAsync(jobExecutionId, cancellationToken);

        if (failedShards.Count == 0)
        {
            return new RetryResult
            {
                TotalFailedShards = 0,
                RetriedShards = 0,
                RetriedShardIds = []
            };
        }

        var retriedIds = new List<long>();
        var recoveryResults = await _shardRecovery.RecoverShardsAsync(
            failedShards.Select(x => x.Id).ToArray(),
            tenantId,
            cancellationToken);

        retriedIds.AddRange(recoveryResults.Where(x => x.Success).Select(x => x.ShardExecutionId));

        _logger.LogInformation(
            "Job execution {ExecutionId}: retried {RetriedCount}/{TotalFailed} failed shards",
            jobExecutionId,
            retriedIds.Count,
            failedShards.Count);

        return new RetryResult
        {
            TotalFailedShards = failedShards.Count,
            RetriedShards = retriedIds.Count,
            RetriedShardIds = retriedIds
        };
    }
}
