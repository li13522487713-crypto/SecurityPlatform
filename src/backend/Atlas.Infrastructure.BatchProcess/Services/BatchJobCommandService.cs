using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Entities;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Infrastructure.BatchProcess.Services;

public sealed class BatchJobCommandService : IBatchJobCommandService
{
    private readonly IBatchJobRepository _repository;
    private readonly IIdGeneratorAccessor _idGen;

    public BatchJobCommandService(IBatchJobRepository repository, IIdGeneratorAccessor idGen)
    {
        _repository = repository;
        _idGen = idGen;
    }

    public async Task<long> CreateAsync(BatchJobCreateRequest request, TenantId tenantId, string createdBy, CancellationToken cancellationToken)
    {
        var entity = new BatchJobDefinition(
            tenantId,
            _idGen.NextId(),
            request.Name,
            request.Description,
            request.DataSourceType,
            request.DataSourceConfig,
            request.ShardStrategyType,
            request.ShardConfig,
            request.BatchSize,
            request.MaxConcurrency,
            request.RetryPolicy,
            request.TimeoutSeconds,
            request.CronExpression,
            createdBy);

        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(long id, BatchJobUpdateRequest request, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "批处理任务不存在");

        if (entity.Status != BatchJobStatus.Draft && entity.Status != BatchJobStatus.Paused)
        {
            throw new BusinessException("INVALID_STATE", "仅草稿或已暂停的任务可以编辑");
        }

        entity.UpdateDefinition(
            request.Name,
            request.Description,
            request.DataSourceType,
            request.DataSourceConfig,
            request.ShardStrategyType,
            request.ShardConfig,
            request.BatchSize,
            request.MaxConcurrency,
            request.RetryPolicy,
            request.TimeoutSeconds,
            request.CronExpression);

        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task ActivateAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "批处理任务不存在");

        entity.Activate();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task PauseAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "批处理任务不存在");

        entity.Pause();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task ArchiveAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "批处理任务不存在");

        entity.Archive();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<long> TriggerAsync(long id, TenantId tenantId, string triggeredBy, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "批处理任务不存在");

        if (entity.Status != BatchJobStatus.Active)
        {
            throw new BusinessException("INVALID_STATE", "仅已激活的任务可以触发执行");
        }

        var execution = new BatchJobExecution(tenantId, _idGen.NextId(), entity.Id, triggeredBy);
        await _repository.AddExecutionAsync(execution, cancellationToken);
        return execution.Id;
    }

    public async Task CancelExecutionAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var execution = await _repository.GetExecutionByIdAsync(executionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "执行实例不存在");

        if (execution.Status != JobExecutionStatus.Pending && execution.Status != JobExecutionStatus.Running)
        {
            throw new BusinessException("INVALID_STATE", "仅待执行或运行中的实例可以取消");
        }

        execution.MarkCancelled();
        await _repository.UpdateExecutionAsync(execution, cancellationToken);
    }
}
