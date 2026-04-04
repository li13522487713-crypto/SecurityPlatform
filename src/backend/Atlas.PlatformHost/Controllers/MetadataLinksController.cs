using Atlas.Application.System.Abstractions;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Provides cross-entity metadata reference queries for dynamic tables.
/// </summary>
[ApiController]
[Route("api/v1/metadata")]
[Authorize]
public sealed class MetadataLinksController : ControllerBase
{
    private readonly IMetadataLinkQueryService _metadataLinkQueryService;

    public MetadataLinksController(IMetadataLinkQueryService metadataLinkQueryService)
    {
        _metadataLinkQueryService = metadataLinkQueryService;
    }

    /// <summary>
    /// Returns all metadata artifacts (forms, pages, approval flow) referencing a dynamic table.
    /// </summary>
    [HttpGet("entities/{tableKey}/references")]
    public async Task<ActionResult<ApiResponse<EntityReferenceResult>>> GetEntityReferences(
        [FromRoute] string tableKey,
        CancellationToken cancellationToken)
    {
        var result = await _metadataLinkQueryService.GetEntityReferencesAsync(tableKey, cancellationToken);
        return Ok(ApiResponse<EntityReferenceResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
