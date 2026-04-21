using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/lowcode/apps/{appId:long}/pages")]
public sealed class LowCodeAppPagesController : ControllerBase
{
    private readonly IPageDefinitionQueryService _query;
    private readonly IPageDefinitionCommandService _command;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAppPagesController(IPageDefinitionQueryService query, IPageDefinitionCommandService command, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _query = query;
        _command = command;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PageDefinitionListItem>>>> List(long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _query.ListAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PageDefinitionListItem>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<PageDefinitionDetail>>> Get(long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var page = await _query.GetAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"页面不存在：{id}");
        return Ok(ApiResponse<PageDefinitionDetail>.Ok(page, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(long appId, [FromBody] PageDefinitionCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _command.CreateAsync(tenantId, user.UserId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long appId, long id, [FromBody] PageDefinitionUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.UpdateAsync(tenantId, user.UserId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/schema")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ReplaceSchema(long appId, long id, [FromBody] PageSchemaReplaceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.ReplaceSchemaAsync(tenantId, user.UserId, appId, id, request, cancellationToken);
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

    [HttpPost("reorder")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Reorder(long appId, [FromBody] PagesReorderRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _command.ReorderAsync(tenantId, user.UserId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
