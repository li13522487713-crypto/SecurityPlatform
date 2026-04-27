using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/microflow-metadata")]
[AllowAnonymous]
public sealed class MicroflowMetadataController : MicroflowApiControllerBase
{
    private readonly IMicroflowMetadataQueryService _metadataQueryService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowMetadataController(
        IMicroflowMetadataQueryService metadataQueryService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _metadataQueryService = metadataQueryService;
        _requestContextAccessor = requestContextAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowMetadataCatalogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowMetadataCatalogDto>>> GetCatalog(
        [FromQuery] string? workspaceId,
        [FromQuery] string? moduleId,
        [FromQuery] bool includeSystem = false,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _metadataQueryService.GetCatalogAsync(
            new MicroflowMetadataQueryDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                ModuleId = moduleId,
                IncludeSystem = includeSystem,
                IncludeArchived = includeArchived
            },
            cancellationToken);

        return MicroflowOk(result);
    }
}
