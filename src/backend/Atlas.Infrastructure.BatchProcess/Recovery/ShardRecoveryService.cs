using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.BatchProcess.Recovery;

/// <summary>
/// 分片恢复服务：从最近的 checkpoint 恢复失败的分片。
/// </summary>
public sealed class ShardRecoveryService : IShardRecoveryService
{
    private readonly IBatchJobRepository _jobRepository;
    private readonly IBatchCheckpointRepository _checkpointRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<ShardRecoveryService> _logger;

    public ShardRecoveryService(
        IBatchJobRepository jobRepository,
        IBatchCheckpointRepository checkpointRepository,
        IMapper mapper,
        ILogger<ShardRecoveryService> logger)
    {
        _jobRepository = jobRepository;
        _checkpointRepository = checkpointRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<RecoveryResult> RecoverShardAsync(long shardExecutionId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var shard = await _jobRepository.GetShardByIdAsync(shardExecutionId, cancellationToken);
        if (shard is null)
        {
            return new RecoveryResult
            {
                Success = false,
                ShardExecutionId = shardExecutionId,
                ErrorMessage = "分片不存在"
            };
        }

        if (shard.Status != ShardExecutionStatus.Failed)
        {
            return new RecoveryResult
            {
                Success = false,
                ShardExecutionId = shardExecutionId,
                ErrorMessage = "仅失败的分片可以恢复"
            };
        }

        var checkpoint = await _checkpointRepository.GetLatestByShardIdAsync(shardExecutionId, cancellationToken);

        shard.MarkRetrying();
        await _jobRepository.UpdateShardAsync(shard, cancellationToken);

        _logger.LogInformation(
            "Shard {ShardId} recovery initiated from checkpoint {CheckpointId}",
            shardExecutionId,
            checkpoint?.Id);

        return new RecoveryResult
        {
            Success = true,
            ShardExecutionId = shardExecutionId,
            RestoredCheckpoint = checkpoint is null ? null : _mapper.Map<CheckpointInfo>(checkpoint)
        };
    }

    public async Task<IReadOnlyList<RecoveryResult>> RecoverShardsAsync(
        IReadOnlyCollection<long> shardExecutionIds,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var distinctIds = shardExecutionIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return Array.Empty<RecoveryResult>();
        }

        var shards = await _jobRepository.GetShardsByIdsAsync(distinctIds, cancellationToken);
        var shardMap = shards.ToDictionary(x => x.Id);
        var checkpoints = await _checkpointRepository.GetLatestByShardIdsAsync(distinctIds, cancellationToken);
        var checkpointMap = checkpoints.ToDictionary(x => x.ShardExecutionId);

        var results = new List<RecoveryResult>(distinctIds.Length);
        var shardsToUpdate = new List<Domain.BatchProcess.Entities.ShardExecution>(distinctIds.Length);

        foreach (var shardId in distinctIds)
        {
            if (!shardMap.TryGetValue(shardId, out var shard))
            {
                results.Add(new RecoveryResult
                {
                    Success = false,
                    ShardExecutionId = shardId,
                    ErrorMessage = "分片不存在"
                });
                continue;
            }

            if (shard.Status != ShardExecutionStatus.Failed)
            {
                results.Add(new RecoveryResult
                {
                    Success = false,
                    ShardExecutionId = shardId,
                    ErrorMessage = "仅失败的分片可以恢复"
                });
                continue;
            }

            checkpointMap.TryGetValue(shardId, out var checkpoint);
            shard.MarkRetrying();
            shardsToUpdate.Add(shard);
            results.Add(new RecoveryResult
            {
                Success = true,
                ShardExecutionId = shardId,
                RestoredCheckpoint = checkpoint is null ? null : _mapper.Map<CheckpointInfo>(checkpoint)
            });
        }

        if (shardsToUpdate.Count > 0)
        {
            await _jobRepository.UpdateShardsAsync(shardsToUpdate, cancellationToken);
        }

        _logger.LogInformation(
            "Batch shard recovery completed, success: {Success}/{Total}",
            results.Count(x => x.Success),
            results.Count);

        return results;
    }
}
