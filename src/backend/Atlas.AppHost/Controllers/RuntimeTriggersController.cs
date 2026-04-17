using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/runtime/triggers")]
[Authorize]
public sealed class RuntimeTriggersController : ControllerBase
{
    private readonly IRuntimeTriggerService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeTriggersController(IRuntimeTriggerService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TriggerInfoDto>>>> List(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _service.ListAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TriggerInfoDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TriggerInfoDto>>> Create([FromBody] TriggerUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _service.UpsertAsync(tenantId, user.UserId, request with { Id = null }, cancellationToken);
        return Ok(ApiResponse<TriggerInfoDto>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<TriggerInfoDto>>> Update(string id, [FromBody] TriggerUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _service.UpsertAsync(tenantId, user.UserId, request with { Id = id }, cancellationToken);
        return Ok(ApiResponse<TriggerInfoDto>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.DeleteAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}:pause")]
    public async Task<ActionResult<ApiResponse<object>>> Pause(string id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.PauseAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}:resume")]
    public async Task<ActionResult<ApiResponse<object>>> Resume(string id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.ResumeAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

[ApiController]
[Route("api/runtime/webview-domains")]
[Authorize]
public sealed class RuntimeWebviewDomainsController : ControllerBase
{
    private readonly IRuntimeWebviewDomainService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeWebviewDomainsController(IRuntimeWebviewDomainService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WebviewDomainInfoDto>>>> List(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _service.ListAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WebviewDomainInfoDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebviewDomainInfoDto>>> Add([FromBody] AddWebviewDomainRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _service.AddAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<WebviewDomainInfoDto>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}:verify")]
    public async Task<ActionResult<ApiResponse<WebviewDomainInfoDto>>> Verify(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _service.VerifyAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<WebviewDomainInfoDto>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Remove(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.RemoveAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
