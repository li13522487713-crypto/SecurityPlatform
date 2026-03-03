using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/lowcode-apps")]
public sealed class LowCodeAppsController : ControllerBase
{
    private readonly ILowCodeAppQueryService _queryService;
    private readonly ILowCodeAppCommandService _commandService;
    private readonly ILowCodePageCommandService _pageCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<LowCodeAppCreateRequest> _createValidator;
    private readonly IValidator<LowCodeAppUpdateRequest> _updateValidator;
    private readonly IValidator<LowCodePageCreateRequest> _pageCreateValidator;
    private readonly IValidator<LowCodePageUpdateRequest> _pageUpdateValidator;

    public LowCodeAppsController(
        ILowCodeAppQueryService queryService,
        ILowCodeAppCommandService commandService,
        ILowCodePageCommandService pageCommandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<LowCodeAppCreateRequest> createValidator,
        IValidator<LowCodeAppUpdateRequest> updateValidator,
        IValidator<LowCodePageCreateRequest> pageCreateValidator,
        IValidator<LowCodePageUpdateRequest> pageUpdateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _pageCommandService = pageCommandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _pageCreateValidator = pageCreateValidator;
        _pageUpdateValidator = pageUpdateValidator;
    }

    /// <summary>
    /// 查询应用列表
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<LowCodeAppListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(request, tenantId, category, cancellationToken);
        return Ok(ApiResponse<PagedResult<LowCodeAppListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 获取应用详情（含页面列表）
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<LowCodeAppDetail?>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<LowCodeAppDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 按 AppKey 获取应用详情
    /// </summary>
    [HttpGet("by-key/{appKey}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<LowCodeAppDetail?>>> GetByKey(
        string appKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByKeyAsync(tenantId, appKey, cancellationToken);
        return Ok(ApiResponse<LowCodeAppDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 创建应用
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] LowCodeAppCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 更新应用
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 发布应用
    /// </summary>
    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(long id, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.PublishAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 停用应用
    /// </summary>
    [HttpPost("{id:long}/disable")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Disable(long id, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DisableAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 删除应用
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    // ─── 页面管理 ───

    /// <summary>
    /// 获取页面详情
    /// </summary>
    [HttpGet("pages/{pageId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<LowCodePageDetail?>>> GetPageById(
        long pageId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetPageByIdAsync(tenantId, pageId, cancellationToken);
        return Ok(ApiResponse<LowCodePageDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 获取应用页面树
    /// </summary>
    [HttpGet("{appId:long}/pages/tree")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LowCodePageTreeNode>>>> GetPageTree(
        long appId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPageTreeAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LowCodePageTreeNode>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 创建页面
    /// </summary>
    [HttpPost("{appId:long}/pages")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreatePage(
        long appId,
        [FromBody] LowCodePageCreateRequest request,
        CancellationToken cancellationToken)
    {
        _pageCreateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _pageCommandService.CreateAsync(tenantId, currentUser.UserId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 更新页面
    /// </summary>
    [HttpPut("pages/{pageId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePage(
        long pageId,
        [FromBody] LowCodePageUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _pageUpdateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _pageCommandService.UpdateAsync(tenantId, currentUser.UserId, pageId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = pageId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 仅更新页面 Schema
    /// </summary>
    [HttpPatch("pages/{pageId:long}/schema")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePageSchema(
        long pageId,
        [FromBody] LowCodePageSchemaUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _pageCommandService.UpdateSchemaAsync(tenantId, currentUser.UserId, pageId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = pageId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 发布页面
    /// </summary>
    [HttpPost("pages/{pageId:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> PublishPage(long pageId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _pageCommandService.PublishAsync(tenantId, currentUser.UserId, pageId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = pageId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 删除页面
    /// </summary>
    [HttpDelete("pages/{pageId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeletePage(long pageId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _pageCommandService.DeleteAsync(tenantId, currentUser.UserId, pageId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = pageId.ToString() }, HttpContext.TraceIdentifier));
    }
}
