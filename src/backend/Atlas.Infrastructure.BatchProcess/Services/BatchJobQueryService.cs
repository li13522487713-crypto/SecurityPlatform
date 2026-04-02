using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;
using AutoMapper;

namespace Atlas.Infrastructure.BatchProcess.Services;

public sealed class BatchJobQueryService : IBatchJobQueryService
{
    private readonly IBatchJobRepository _repository;
    private readonly IMapper _mapper;

    public BatchJobQueryService(IBatchJobRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<BatchJobDefinitionListItem>> QueryJobsAsync(
        PagedRequest request,
        BatchJobStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _repository.QueryPageAsync(pageIndex, pageSize, request.Keyword, status, cancellationToken);
        var resultItems = items.Select(x => _mapper.Map<BatchJobDefinitionListItem>(x)).ToArray();
        return new PagedResult<BatchJobDefinitionListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<BatchJobDefinitionResponse?> GetJobByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : _mapper.Map<BatchJobDefinitionResponse>(entity);
    }

    public async Task<PagedResult<BatchJobExecutionListItem>> QueryExecutionsAsync(
        long jobDefinitionId,
        PagedRequest request,
        JobExecutionStatus? status,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _repository.QueryExecutionPageAsync(jobDefinitionId, pageIndex, pageSize, status, cancellationToken);
        var resultItems = items.Select(x => _mapper.Map<BatchJobExecutionListItem>(x)).ToArray();
        return new PagedResult<BatchJobExecutionListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<BatchJobExecutionResponse?> GetExecutionByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetExecutionByIdAsync(id, cancellationToken);
        return entity is null ? null : _mapper.Map<BatchJobExecutionResponse>(entity);
    }

    public async Task<IReadOnlyList<ShardExecutionResponse>> GetShardsByExecutionIdAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var shards = await _repository.GetShardsByExecutionIdAsync(executionId, cancellationToken);
        return shards.Select(x => _mapper.Map<ShardExecutionResponse>(x)).ToArray();
    }
}
