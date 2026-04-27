using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public sealed class InMemoryMicroflowResourceQueryService : IMicroflowResourceQueryService
{
    public Task<MicroflowApiPageResult<MicroflowResourceDto>> GetPagedAsync(
        MicroflowResourceQueryDto query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var result = new MicroflowApiPageResult<MicroflowResourceDto>
        {
            Items = Array.Empty<MicroflowResourceDto>(),
            Total = 0,
            PageIndex = pageIndex,
            PageSize = pageSize,
            HasMore = false
        };

        return Task.FromResult(result);
    }

    public Task<MicroflowResourceDto?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<MicroflowResourceDto?>(null);
    }
}
