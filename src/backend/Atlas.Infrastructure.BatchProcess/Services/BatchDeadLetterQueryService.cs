using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;
using AutoMapper;

namespace Atlas.Infrastructure.BatchProcess.Services;

public sealed class BatchDeadLetterQueryService : IBatchDeadLetterQueryService
{
    private readonly IBatchDeadLetterRepository _repository;
    private readonly IMapper _mapper;

    public BatchDeadLetterQueryService(IBatchDeadLetterRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<BatchDeadLetterListItem>> QueryAsync(
        long? jobExecutionId,
        DeadLetterStatus? status,
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _repository.QueryPageAsync(jobExecutionId, status, pageIndex, pageSize, cancellationToken);
        var resultItems = items.Select(x => _mapper.Map<BatchDeadLetterListItem>(x)).ToArray();
        return new PagedResult<BatchDeadLetterListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<BatchDeadLetterResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : _mapper.Map<BatchDeadLetterResponse>(entity);
    }
}
