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

    public string? TenantId { get; init; }

    public string? Keyword { get; init; }

    public IReadOnlyList<string> Status { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PublishStatus { get; init; } = Array.Empty<string>();

    public bool FavoriteOnly { get; init; }

    public string? OwnerId { get; init; }

    public string? ModuleId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public DateTimeOffset? UpdatedFrom { get; init; }

    public DateTimeOffset? UpdatedTo { get; init; }

    public string? SortBy { get; init; }

    public string? SortOrder { get; init; }

    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
