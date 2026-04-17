using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/lowcode/apps/{appId:long}/variables")]
public sealed class LowCodeAppVariablesController : ControllerBase
{
    private readonly IAppVariableQueryService _query;
    private readonly IAppVariableCommandService _command;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAppVariablesController(IAppVariableQueryService query, IAppVariableCommandService command, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _query = query;
        _command = command;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppVariableDto>>>> List(long appId, [FromQuery] string? scope, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _query.ListAsync(tenantId, appId, scope, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppVariableDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(long appId, [FromBody] AppVariableCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _command.CreateAsync(tenantId, user.UserId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long appId, long id, [FromBody] AppVariableUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.UpdateAsync(tenantId, user.UserId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.DeleteAsync(tenantId, user.UserId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
