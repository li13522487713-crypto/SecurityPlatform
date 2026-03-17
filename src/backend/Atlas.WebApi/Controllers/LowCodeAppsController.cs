using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Attributes;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/lowcode-apps")]
[DeprecatedApi("lowcode-apps v1 is in compatibility window", "/api/v2/tenant-app-instances")]
[Obsolete("Deprecated since Sprint 1. Please migrate to api/v1/app-manifests and api/v1/packages endpoints.")]
public sealed class LowCodeAppsController : ControllerBase
{
    private readonly ILowCodeAppQueryService _queryService;
    private readonly ILowCodeAppCommandService _commandService;
    private readonly ILowCodePageCommandService _pageCommandService;
    private readonly ILowCodeEnvironmentService _environmentService;
    private readonly ITenantDataSourceService _tenantDataSourceService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<LowCodeAppCreateRequest> _createValidator;
    private readonly IValidator<LowCodeAppUpdateRequest> _updateValidator;
    private readonly IValidator<LowCodeAppImportRequest> _importValidator;
    private readonly IValidator<LowCodeAppSharingPolicyUpdateRequest> _sharingPolicyValidator;
    private readonly IValidator<LowCodeAppEntityAliasesUpdateRequest> _entityAliasesValidator;
    private readonly IValidator<LowCodePageCreateRequest> _pageCreateValidator;
    private readonly IValidator<LowCodePageUpdateRequest> _pageUpdateValidator;
    private readonly IValidator<LowCodeEnvironmentCreateRequest> _environmentCreateValidator;
    private readonly IValidator<LowCodeEnvironmentUpdateRequest> _environmentUpdateValidator;

    public LowCodeAppsController(
        ILowCodeAppQueryService queryService,
        ILowCodeAppCommandService commandService,
        ILowCodePageCommandService pageCommandService,
        ILowCodeEnvironmentService environmentService,
        ITenantDataSourceService tenantDataSourceService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<LowCodeAppCreateRequest> createValidator,
        IValidator<LowCodeAppUpdateRequest> updateValidator,
        IValidator<LowCodeAppImportRequest> importValidator,
        IValidator<LowCodeAppSharingPolicyUpdateRequest> sharingPolicyValidator,
        IValidator<LowCodeAppEntityAliasesUpdateRequest> entityAliasesValidator,
        IValidator<LowCodePageCreateRequest> pageCreateValidator,
        IValidator<LowCodePageUpdateRequest> pageUpdateValidator,
        IValidator<LowCodeEnvironmentCreateRequest> environmentCreateValidator,
        IValidator<LowCodeEnvironmentUpdateRequest> environmentUpdateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _pageCommandService = pageCommandService;
        _environmentService = environmentService;
        _tenantDataSourceService = tenantDataSourceService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _importValidator = importValidator;
        _sharingPolicyValidator = sharingPolicyValidator;
        _entityAliasesValidator = entityAliasesValidator;
        _pageCreateValidator = pageCreateValidator;
        _pageUpdateValidator = pageUpdateValidator;
        _environmentCreateValidator = environmentCreateValidator;
        _environmentUpdateValidator = environmentUpdateValidator;
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
    /// 查询应用共享策略
    /// </summary>
    [HttpGet("{id:long}/sharing-policy")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<LowCodeAppSharingPolicy?>>> GetSharingPolicy(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetSharingPolicyAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<LowCodeAppSharingPolicy?>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 更新应用共享策略
    /// </summary>
    [HttpPut("{id:long}/sharing-policy")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSharingPolicy(
        long id,
        [FromBody] LowCodeAppSharingPolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _sharingPolicyValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateSharingPolicyAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 查询应用实体别名
    /// </summary>
    [HttpGet("{id:long}/entity-aliases")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LowCodeAppEntityAliasItem>>>> GetEntityAliases(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetEntityAliasesAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LowCodeAppEntityAliasItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 更新应用实体别名
    /// </summary>
    [HttpPut("{id:long}/entity-aliases")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateEntityAliases(
        long id,
        [FromBody] LowCodeAppEntityAliasesUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _entityAliasesValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateEntityAliasesAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 查询应用绑定的数据源
    /// </summary>
    [HttpGet("{id:long}/datasource")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<LowCodeAppDataSourceInfo?>>> GetDataSource(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetDataSourceInfoAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<LowCodeAppDataSourceInfo?>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 测试应用绑定数据源连接
    /// </summary>
    [HttpPost("{id:long}/datasource/test")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<TestConnectionResult>>> TestDataSource(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var dataSource = await _queryService.GetDataSourceInfoAsync(tenantId, id, cancellationToken);
        if (dataSource is null)
        {
            return NotFound(ApiResponse<TestConnectionResult>.Fail(ErrorCodes.NotFound, "应用不存在", HttpContext.TraceIdentifier));
        }

        if (!long.TryParse(dataSource.DataSourceId, out var dataSourceId))
        {
            return Ok(ApiResponse<TestConnectionResult>.Ok(
                new TestConnectionResult(false, "应用未绑定数据源"),
                HttpContext.TraceIdentifier));
        }

        var result = await _tenantDataSourceService.TestConnectionByDataSourceIdAsync(tenantId.Value.ToString(), dataSourceId, cancellationToken);
        return Ok(ApiResponse<TestConnectionResult>.Ok(result, HttpContext.TraceIdentifier));
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
    /// 查询应用版本历史
    /// </summary>
    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<LowCodeAppVersionListItem>>>> GetVersions(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetVersionsAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<LowCodeAppVersionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 回滚应用到指定版本
    /// </summary>
    [HttpPost("{id:long}/versions/{versionId:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Rollback(
        long id,
        long versionId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var rollbackVersion = await _commandService.RollbackAsync(
            tenantId,
            currentUser.UserId,
            id,
            versionId,
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), Version = rollbackVersion }, HttpContext.TraceIdentifier));
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

    /// <summary>
    /// 导出应用（JSON）
    /// </summary>
    [HttpGet("{id:long}/export")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<IActionResult> Export(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var exportPackage = await _queryService.ExportAsync(tenantId, id, cancellationToken);
        if (exportPackage is null)
        {
            return NotFound(ApiResponse<object>.Fail(ErrorCodes.NotFound, "应用不存在", HttpContext.TraceIdentifier));
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(exportPackage, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        var fileName = $"{exportPackage.AppKey}-export.json";
        return File(bytes, "application/json; charset=utf-8", fileName);
    }

    /// <summary>
    /// 导入应用（JSON包）
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<LowCodeAppImportResult>>> Import(
        [FromBody] LowCodeAppImportRequest request,
        CancellationToken cancellationToken)
    {
        _importValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<LowCodeAppImportResult>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.ImportAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<LowCodeAppImportResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 查询应用环境配置
    /// </summary>
    [HttpGet("{appId:long}/environments")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LowCodeEnvironmentListItem>>>> GetEnvironments(
        long appId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _environmentService.GetByAppIdAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LowCodeEnvironmentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("environments/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<LowCodeEnvironmentDetail?>>> GetEnvironmentById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _environmentService.GetByIdAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<LowCodeEnvironmentDetail?>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 创建应用环境配置
    /// </summary>
    [HttpPost("{appId:long}/environments")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateEnvironment(
        long appId,
        [FromBody] LowCodeEnvironmentCreateRequest request,
        CancellationToken cancellationToken)
    {
        _environmentCreateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _environmentService.CreateAsync(tenantId, currentUser.UserId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 更新应用环境配置
    /// </summary>
    [HttpPut("environments/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateEnvironment(
        long id,
        [FromBody] LowCodeEnvironmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _environmentUpdateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _environmentService.UpdateAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 删除应用环境配置
    /// </summary>
    [HttpDelete("environments/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteEnvironment(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _environmentService.DeleteAsync(tenantId, currentUser.UserId, id, cancellationToken);
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
    /// 获取页面运行态 Schema（draft/published）
    /// </summary>
    [HttpGet("pages/{pageId:long}/runtime")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<LowCodePageRuntimeSchema?>>> GetRuntimePageSchema(
        long pageId,
        [FromQuery] string mode = "draft",
        [FromQuery] string? environmentCode = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetRuntimePageSchemaAsync(tenantId, pageId, mode, environmentCode, cancellationToken);
        return Ok(ApiResponse<LowCodePageRuntimeSchema?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 获取页面版本历史
    /// </summary>
    [HttpGet("pages/{pageId:long}/versions")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LowCodePageVersionListItem>>>> GetPageVersions(
        long pageId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPageVersionsAsync(tenantId, pageId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LowCodePageVersionListItem>>.Ok(result, HttpContext.TraceIdentifier));
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

    /// <summary>
    /// 回滚页面到历史版本（并发布为新版本）
    /// </summary>
    [HttpPost("pages/{pageId:long}/rollback/{versionId:long}")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RollbackPage(
        long pageId,
        long versionId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _pageCommandService.RollbackAsync(tenantId, currentUser.UserId, pageId, versionId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = pageId.ToString(), VersionId = versionId.ToString() }, HttpContext.TraceIdentifier));
    }
}
