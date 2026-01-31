using Atlas.Application.TableViews.Abstractions;
using Atlas.Application.TableViews.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/table-views")]
public sealed class TableViewsController : ControllerBase
{
    private readonly ITableViewQueryService _queryService;
    private readonly ITableViewCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IValidator<TableViewCreateRequest> _createValidator;
    private readonly IValidator<TableViewUpdateRequest> _updateValidator;
    private readonly IValidator<TableViewConfigUpdateRequest> _configValidator;
    private readonly IValidator<TableViewDuplicateRequest> _duplicateValidator;

    public TableViewsController(
        ITableViewQueryService queryService,
        ITableViewCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        IValidator<TableViewCreateRequest> createValidator,
        IValidator<TableViewUpdateRequest> updateValidator,
        IValidator<TableViewConfigUpdateRequest> configValidator,
        IValidator<TableViewDuplicateRequest> duplicateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _idGeneratorAccessor = idGeneratorAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _configValidator = configValidator;
        _duplicateValidator = duplicateValidator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PagedResult<TableViewListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string tableKey,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<PagedResult<TableViewListItem>>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(tableKey))
        {
            return BadRequest(ApiResponse<PagedResult<TableViewListItem>>.Fail(
                ErrorCodes.ValidationError,
                "TableKey不能为空",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(request, tenantId, currentUser.UserId, tableKey, cancellationToken);
        return Ok(ApiResponse<PagedResult<TableViewListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("default")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<TableViewDetail?>>> GetDefault(
        [FromQuery] string tableKey,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<TableViewDetail?>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(tableKey))
        {
            return BadRequest(ApiResponse<TableViewDetail?>.Fail(
                ErrorCodes.ValidationError,
                "TableKey不能为空",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetDefaultAsync(tenantId, currentUser.UserId, tableKey, cancellationToken);
        return Ok(ApiResponse<TableViewDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("default-config")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<TableViewConfig>>> GetDefaultConfig(
        [FromQuery] string tableKey,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<TableViewConfig>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(tableKey))
        {
            return BadRequest(ApiResponse<TableViewConfig>.Fail(
                ErrorCodes.ValidationError,
                "TableKey不能为空",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var config = await _queryService.GetDefaultConfigAsync(tenantId, tableKey, cancellationToken);
        return Ok(ApiResponse<TableViewConfig>.Ok(config, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<TableViewDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<TableViewDetail>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByIdAsync(tenantId, currentUser.UserId, id, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<TableViewDetail>.Fail(
                ErrorCodes.NotFound,
                "视图不存在",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TableViewDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] TableViewCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = _idGeneratorAccessor.NextId();
        var createdId = await _commandService.CreateAsync(tenantId, currentUser.UserId, request, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] TableViewUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:long}/config")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> UpdateConfig(
        long id,
        [FromBody] TableViewConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _configValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateConfigAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/set-default")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> SetDefault(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.SetDefaultAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/duplicate")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Duplicate(
        long id,
        [FromBody] TableViewDuplicateRequest request,
        CancellationToken cancellationToken)
    {
        _duplicateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var newId = _idGeneratorAccessor.NextId();
        var createdId = await _commandService.DuplicateAsync(tenantId, currentUser.UserId, id, request, newId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
