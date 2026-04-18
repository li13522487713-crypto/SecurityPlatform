using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Core.Models;
using Atlas.Domain.ExternalConnectors.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Connectors;

[ApiController]
[Authorize]
[Route("api/v1/connectors/identity-bindings")]
public sealed class ConnectorIdentityBindingsController : ControllerBase
{
    private readonly IExternalIdentityBindingService _service;
    private readonly IValidator<ManualBindingRequest> _manualValidator;
    private readonly IValidator<BindingConflictResolutionRequest> _resolutionValidator;

    public ConnectorIdentityBindingsController(
        IExternalIdentityBindingService service,
        IValidator<ManualBindingRequest> manualValidator,
        IValidator<BindingConflictResolutionRequest> resolutionValidator)
    {
        _service = service;
        _manualValidator = manualValidator;
        _resolutionValidator = resolutionValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ExternalIdentityBindingListItem>>> ListAsync(
        [FromQuery] long providerId,
        [FromQuery] IdentityBindingStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize is < 1 or > 200) pageSize = 20;
        var skip = (pageIndex - 1) * pageSize;
        var items = await _service.ListByProviderAsync(providerId, status, skip, pageSize, cancellationToken).ConfigureAwait(false);
        var total = await _service.CountByProviderAsync(providerId, status, cancellationToken).ConfigureAwait(false);
        return Ok(new PagedResult<ExternalIdentityBindingListItem>(items.ToList(), total, pageIndex, pageSize));
    }

    [HttpPost("manual")]
    public async Task<ActionResult<ExternalIdentityBindingResponse>> CreateManualAsync([FromBody] ManualBindingRequest request, CancellationToken cancellationToken)
    {
        await _manualValidator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        var binding = await _service.CreateManualAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(binding);
    }

    [HttpPost("conflicts:resolve")]
    public async Task<ActionResult<ExternalIdentityBindingResponse>> ResolveConflictAsync([FromBody] BindingConflictResolutionRequest request, CancellationToken cancellationToken)
    {
        await _resolutionValidator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        var binding = await _service.ResolveConflictAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(binding);
    }

    [HttpDelete("{bindingId:long}")]
    public async Task<IActionResult> RevokeAsync(long bindingId, CancellationToken cancellationToken)
    {
        await _service.RevokeAsync(bindingId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
