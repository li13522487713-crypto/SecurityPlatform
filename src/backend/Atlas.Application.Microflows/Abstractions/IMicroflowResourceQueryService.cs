using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowResourceQueryService
{
    Task<MicroflowApiPageResult<MicroflowResourceDto>> GetPagedAsync(
        MicroflowResourceQueryDto query,
        CancellationToken cancellationToken);

    Task<MicroflowResourceDto?> GetByIdAsync(string id, CancellationToken cancellationToken);
}

public sealed record MicroflowResourceQueryDto
{
    public string? WorkspaceId { get; init; }

    public string? Keyword { get; init; }

    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
