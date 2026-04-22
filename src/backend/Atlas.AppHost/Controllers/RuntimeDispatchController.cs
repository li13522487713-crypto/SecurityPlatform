using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/runtime/dispatch")]
[Authorize]
public sealed class RuntimeDispatchController : ControllerBase
{
    private readonly IDispatchExecutor _dispatchExecutor;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeDispatchController(
        IDispatchExecutor dispatchExecutor,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUser)
    {
        _dispatchExecutor = dispatchExecutor;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DispatchResponse>>> Dispatch(
        [FromBody] DispatchRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var response = await _dispatchExecutor.DispatchAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<DispatchResponse>.Ok(response, HttpContext.TraceIdentifier));
    }
}
