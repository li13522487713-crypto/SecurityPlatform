using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Domain.ExternalConnectors.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Connectors;

[ApiController]
[Authorize]
[Route("api/v1/connectors/providers")]
public sealed class ConnectorProvidersController : ControllerBase
{
    private readonly IExternalIdentityProviderQueryService _query;
    private readonly IExternalIdentityProviderCommandService _command;
    private readonly IValidator<ExternalIdentityProviderCreateRequest> _createValidator;
    private readonly IValidator<ExternalIdentityProviderUpdateRequest> _updateValidator;
    private readonly IValidator<ExternalIdentityProviderRotateSecretRequest> _rotateValidator;

    public ConnectorProvidersController(
        IExternalIdentityProviderQueryService query,
        IExternalIdentityProviderCommandService command,
        IValidator<ExternalIdentityProviderCreateRequest> createValidator,
        IValidator<ExternalIdentityProviderUpdateRequest> updateValidator,
        IValidator<ExternalIdentityProviderRotateSecretRequest> rotateValidator)
    {
        _query = query;
        _command = command;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _rotateValidator = rotateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExternalIdentityProviderListItem>>> ListAsync(
        [FromQuery] ConnectorProviderType? type,
        [FromQuery] bool includeDisabled,
        CancellationToken cancellationToken)
        => Ok(await _query.ListAsync(type, includeDisabled, cancellationToken).ConfigureAwait(false));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ExternalIdentityProviderResponse>> GetAsync(long id, CancellationToken cancellationToken)
    {
        var item = await _query.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ExternalIdentityProviderResponse>> CreateAsync([FromBody] ExternalIdentityProviderCreateRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        var created = await _command.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetAsync), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ExternalIdentityProviderResponse>> UpdateAsync(long id, [FromBody] ExternalIdentityProviderUpdateRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        var updated = await _command.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }

    [HttpPost("{id:long}/secret:rotate")]
    public async Task<ActionResult<ExternalIdentityProviderResponse>> RotateSecretAsync(long id, [FromBody] ExternalIdentityProviderRotateSecretRequest request, CancellationToken cancellationToken)
    {
        await _rotateValidator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        var updated = await _command.RotateSecretAsync(id, request, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }

    [HttpPost("{id:long}:enable")]
    public async Task<ActionResult<ExternalIdentityProviderResponse>> EnableAsync(long id, CancellationToken cancellationToken)
        => Ok(await _command.SetEnabledAsync(id, true, cancellationToken).ConfigureAwait(false));

    [HttpPost("{id:long}:disable")]
    public async Task<ActionResult<ExternalIdentityProviderResponse>> DisableAsync(long id, CancellationToken cancellationToken)
        => Ok(await _command.SetEnabledAsync(id, false, cancellationToken).ConfigureAwait(false));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        await _command.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
