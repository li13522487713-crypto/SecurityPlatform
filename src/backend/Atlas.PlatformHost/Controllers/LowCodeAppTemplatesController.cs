using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 应用模板控制器（M07 S07-4，**设计态 v1** /api/v1/lowcode/templates）。
/// kind：page / component-set / pattern-A..D / industry。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/templates")]
public sealed class LowCodeAppTemplatesController : ControllerBase
{
    private readonly IAppTemplateService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAppTemplatesController(IAppTemplateService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppTemplateDto>>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? kind,
        [FromQuery] string? shareScope,
        [FromQuery] string? industryTag,
        [FromQuery] int? pageIndex,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _service.SearchAsync(tenantId, keyword, kind, shareScope, industryTag, pageIndex ?? 1, pageSize ?? 20, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppTemplateDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppTemplateDto>>> Get(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var t = await _service.GetAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"模板不存在：{id}");
        return Ok(ApiResponse<AppTemplateDto>.Ok(t, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Upsert([FromBody] AppTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _service.UpsertAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.DeleteAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/apply")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppTemplateApplyResult>>> Apply(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _service.ApplyAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<AppTemplateApplyResult>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/star")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<object>>> Star(long id, [FromQuery] bool? increment, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var stars = await _service.StarAsync(tenantId, user.UserId, id, increment ?? true, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { stars }, HttpContext.TraceIdentifier));
    }
}
