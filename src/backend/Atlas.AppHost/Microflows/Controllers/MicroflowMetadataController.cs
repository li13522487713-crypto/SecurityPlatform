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
    private readonly IMicroflowMetadataService _metadataService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowMetadataController(
        IMicroflowMetadataService metadataService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _metadataService = metadataService;
        _requestContextAccessor = requestContextAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowMetadataCatalogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowMetadataCatalogDto>>> GetCatalog(
        [FromQuery] string? workspaceId,
        [FromQuery] string? moduleId,
        [FromQuery] bool includeSystem = true,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _metadataService.GetCatalogAsync(
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                ModuleId = moduleId,
                IncludeSystem = includeSystem,
                IncludeArchived = includeArchived
            },
            cancellationToken);

        return MicroflowOk(result);
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowMetadataHealthDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowMetadataHealthDto>>> GetHealth(
        [FromQuery] string? workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _metadataService.GetHealthAsync(workspaceId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("entities/{*qualifiedName}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MetadataEntityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MetadataEntityDto>>> GetEntity(
        string qualifiedName,
        [FromQuery] string? workspaceId,
        [FromQuery] string? moduleId,
        [FromQuery] bool includeSystem = true,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _metadataService.GetEntityAsync(
            Uri.UnescapeDataString(qualifiedName),
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                ModuleId = moduleId,
                IncludeSystem = includeSystem,
                IncludeArchived = includeArchived
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("enumerations/{*qualifiedName}")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MetadataEnumerationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MetadataEnumerationDto>>> GetEnumeration(
        string qualifiedName,
        [FromQuery] string? workspaceId,
        [FromQuery] string? moduleId,
        [FromQuery] bool includeSystem = true,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _metadataService.GetEnumerationAsync(
            Uri.UnescapeDataString(qualifiedName),
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                ModuleId = moduleId,
                IncludeSystem = includeSystem,
                IncludeArchived = includeArchived
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("microflows")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MetadataMicroflowRefDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MetadataMicroflowRefDto>>>> GetMicroflowRefs(
        [FromQuery] string? workspaceId,
        [FromQuery] string? moduleId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] string[]? status = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _metadataService.GetMicroflowRefsAsync(
            new GetMicroflowRefsRequestDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                ModuleId = moduleId,
                IncludeArchived = includeArchived,
                Status = status ?? Array.Empty<string>(),
                Keyword = keyword
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("pages")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MetadataPageRefDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MetadataPageRefDto>>>> GetPageRefs(
        [FromQuery] string? workspaceId,
        [FromQuery] string? moduleId,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _metadataService.GetPageRefsAsync(
            new GetPageRefsRequestDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                ModuleId = moduleId,
                Keyword = keyword
            },
            cancellationToken);
        return MicroflowOk(result);
    }

    [HttpGet("workflows")]
    [ProducesResponseType(typeof(MicroflowApiResponse<IReadOnlyList<MetadataWorkflowRefDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<IReadOnlyList<MetadataWorkflowRefDto>>>> GetWorkflowRefs(
        [FromQuery] string? workspaceId,
        [FromQuery] string? moduleId,
        [FromQuery] string? contextEntityQualifiedName = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var current = _requestContextAccessor.Current;
        var result = await _metadataService.GetWorkflowRefsAsync(
            new GetWorkflowRefsRequestDto
            {
                WorkspaceId = workspaceId ?? current.WorkspaceId,
                ModuleId = moduleId,
                ContextEntityQualifiedName = contextEntityQualifiedName,
                Keyword = keyword
            },
            cancellationToken);
        return MicroflowOk(result);
    }
}
