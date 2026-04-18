using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Connectors;

[ApiController]
[Authorize]
[Route("api/v1/connectors/providers/{providerId:long}/approvals")]
public sealed class ConnectorApprovalTemplatesController : ControllerBase
{
    private readonly IExternalApprovalTemplateService _service;

    public ConnectorApprovalTemplatesController(IExternalApprovalTemplateService service)
    {
        _service = service;
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<ExternalApprovalTemplateResponse>>> ListAsync(long providerId, CancellationToken cancellationToken)
        => Ok(await _service.ListCachedAsync(providerId, cancellationToken).ConfigureAwait(false));

    [HttpPost("templates/{externalTemplateId}:refresh")]
    public async Task<ActionResult<ExternalApprovalTemplateResponse>> RefreshAsync(long providerId, string externalTemplateId, CancellationToken cancellationToken)
        => Ok(await _service.RefreshAsync(providerId, externalTemplateId, cancellationToken).ConfigureAwait(false));

    [HttpGet("template-mappings")]
    public async Task<ActionResult<IReadOnlyList<ExternalApprovalTemplateMappingResponse>>> ListMappingsAsync(long providerId, CancellationToken cancellationToken)
        => Ok(await _service.ListMappingsAsync(providerId, cancellationToken).ConfigureAwait(false));

    [HttpGet("template-mappings/{flowDefinitionId:long}")]
    public async Task<ActionResult<ExternalApprovalTemplateMappingResponse>> GetMappingAsync(long providerId, long flowDefinitionId, CancellationToken cancellationToken)
    {
        var mapping = await _service.GetMappingAsync(providerId, flowDefinitionId, cancellationToken).ConfigureAwait(false);
        return mapping is null ? NotFound() : Ok(mapping);
    }

    [HttpPut("template-mappings/{flowDefinitionId:long}")]
    public async Task<ActionResult<ExternalApprovalTemplateMappingResponse>> UpsertMappingAsync(long providerId, long flowDefinitionId, [FromBody] ExternalApprovalTemplateMappingRequest request, CancellationToken cancellationToken)
    {
        request.ProviderId = providerId;
        request.FlowDefinitionId = flowDefinitionId;
        var mapping = await _service.UpsertMappingAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(mapping);
    }

    [HttpDelete("template-mappings/{mappingId:long}")]
    public async Task<IActionResult> DeleteMappingAsync(long providerId, long mappingId, CancellationToken cancellationToken)
    {
        _ = providerId;
        await _service.DeleteMappingAsync(mappingId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
