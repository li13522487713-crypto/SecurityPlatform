using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowResourceService
{
    Task<MicroflowApiPageResult<MicroflowResourceDto>> ListAsync(ListMicroflowsRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> CreateAsync(CreateMicroflowRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> UpdateAsync(string id, UpdateMicroflowResourceRequestDto request, CancellationToken cancellationToken);

    Task<GetMicroflowSchemaResponseDto> GetSchemaAsync(string id, CancellationToken cancellationToken);

    Task<SaveMicroflowSchemaResponseDto> SaveSchemaAsync(string id, SaveMicroflowSchemaRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> DuplicateAsync(string id, DuplicateMicroflowRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> RenameAsync(string id, RenameMicroflowRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> ToggleFavoriteAsync(string id, ToggleFavoriteMicroflowRequestDto request, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> ArchiveAsync(string id, CancellationToken cancellationToken);

    Task<MicroflowResourceDto> RestoreAsync(string id, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
