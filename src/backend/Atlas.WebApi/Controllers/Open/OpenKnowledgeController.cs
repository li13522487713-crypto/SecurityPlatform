using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers.Open;

[ApiController]
[Route("api/v1/open/knowledge")]
[Authorize(AuthenticationSchemes = $"{PatAuthenticationHandler.SchemeName},{OpenProjectAuthenticationHandler.SchemeName}")]
[AppRuntimeOnly]
public sealed class OpenKnowledgeController : ControllerBase
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IDocumentService _documentService;
    private readonly ITenantProvider _tenantProvider;

    public OpenKnowledgeController(
        IKnowledgeBaseService knowledgeBaseService,
        IDocumentService documentService,
        ITenantProvider tenantProvider)
    {
        _knowledgeBaseService = knowledgeBaseService;
        _documentService = documentService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("bases")]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeBaseDto>>>> GetBases(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        if (!OpenScopeHelper.HasScope(User, "open:knowledge:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<PagedResult<KnowledgeBaseDto>>.Fail(
                ErrorCodes.Forbidden,
                ApiResponseLocalizer.T(HttpContext, "PatMissingKnowledgeReadScope"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _knowledgeBaseService.GetPagedAsync(
            tenantId,
            keyword,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeBaseDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("bases/{id:long}")]
    public async Task<ActionResult<ApiResponse<KnowledgeBaseDto>>> GetBaseById(long id, CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:knowledge:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<KnowledgeBaseDto>.Fail(
                ErrorCodes.Forbidden,
                ApiResponseLocalizer.T(HttpContext, "PatMissingKnowledgeReadScope"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _knowledgeBaseService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<KnowledgeBaseDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "KnowledgeBaseNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<KnowledgeBaseDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("bases/{id:long}/documents")]
    public async Task<ActionResult<ApiResponse<PagedResult<KnowledgeDocumentDto>>>> GetDocuments(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:knowledge:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<PagedResult<KnowledgeDocumentDto>>.Fail(
                ErrorCodes.Forbidden,
                ApiResponseLocalizer.T(HttpContext, "PatMissingKnowledgeReadScope"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _documentService.ListByKnowledgeBaseAsync(
            tenantId,
            id,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<KnowledgeDocumentDto>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
