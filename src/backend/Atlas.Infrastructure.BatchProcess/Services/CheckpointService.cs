using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Entities;
using AutoMapper;

namespace Atlas.Infrastructure.BatchProcess.Services;

public sealed class CheckpointService : ICheckpointService
{
    private readonly IBatchCheckpointRepository _repository;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IMapper _mapper;

    public CheckpointService(IBatchCheckpointRepository repository, IIdGeneratorAccessor idGen, IMapper mapper)
    {
        _repository = repository;
        _idGen = idGen;
        _mapper = mapper;
    }

    public async Task<long> SaveAsync(
        long shardExecutionId,
        string checkpointKey,
        string processedUpTo,
        long processedCount,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var checkpoint = new BatchCheckpoint(tenantId, _idGen.NextId(), shardExecutionId, checkpointKey, processedUpTo, processedCount);
        return await _repository.AddAsync(checkpoint, cancellationToken);
    }

    public async Task<CheckpointInfo?> GetLatestAsync(long shardExecutionId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetLatestByShardIdAsync(shardExecutionId, cancellationToken);
        return entity is null ? null : _mapper.Map<CheckpointInfo>(entity);
    }
}
