using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Core.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Authorization;
using System.Diagnostics;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/apps")]
public sealed class AppsController : ControllerBase
{
    private readonly IAppConfigQueryService _queryService;
    private readonly IAppConfigCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<AppConfigUpdateRequest> _updateValidator;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ILogger<AppsController> _logger;

    public AppsController(
        IAppConfigQueryService queryService,
        IAppConfigCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<AppConfigUpdateRequest> updateValidator,
        IAppContextAccessor appContextAccessor,
        ILogger<AppsController> logger)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _updateValidator = updateValidator;
        _appContextAccessor = appContextAccessor;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppConfigListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppConfigListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("current")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AppConfigDetail>>> GetCurrent(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var tenantId = _tenantProvider.GetTenantId();
        var appId = _appContextAccessor.GetAppId();
        _logger.LogInformation("[Apps/Current] 开始查询 AppId={AppId}", appId);

        var detail = await _queryService.GetByAppIdAsync(appId, tenantId, cancellationToken);
        _logger.LogInformation("[Apps/Current] DB查询 耗时{Elapsed}ms AppId={AppId} Found={Found}",
            sw.ElapsedMilliseconds, appId, detail is not null);

        if (detail is null)
        {
            // 对未配置应用返回默认配置，避免前端初始化阶段出现 404 干扰。
            var fallback = new AppConfigDetail(
                Id: "0",
                AppId: appId,
                Name: appId,
                IsActive: true,
                EnableProjectScope: false,
                Description: "Default app configuration",
                SortOrder: 0);
            _logger.LogInformation("[Apps/Current] 返回默认配置 总耗时{Elapsed}ms", sw.ElapsedMilliseconds);
            return Ok(ApiResponse<AppConfigDetail>.Ok(fallback, HttpContext.TraceIdentifier));
        }

        _logger.LogInformation("[Apps/Current] 总耗时{Elapsed}ms", sw.ElapsedMilliseconds);
        return Ok(ApiResponse<AppConfigDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<AppConfigDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<AppConfigDetail>.Fail(ErrorCodes.NotFound, "App not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AppConfigDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AppConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
