using Atlas.Application.LogicFlow.Expressions.Abstractions;
using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/decision-tables")]
[Authorize]
[PlatformOnly]
public sealed class DecisionTablesController : ControllerBase
{
    private readonly IDecisionTableQueryService _queryService;
    private readonly IDecisionTableCommandService _commandService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<DecisionTableCreateRequest> _createValidator;
    private readonly IValidator<DecisionTableUpdateRequest> _updateValidator;

    public DecisionTablesController(
        IDecisionTableQueryService queryService,
        IDecisionTableCommandService commandService,
        ICurrentUserAccessor currentUser,
        ITenantProvider tenantProvider,
        IValidator<DecisionTableCreateRequest> createValidator,
        IValidator<DecisionTableUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DecisionTableListItem>>>> GetPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPagedAsync(new PagedRequest { PageIndex = pageIndex, PageSize = pageSize }, tenantId, keyword, cancellationToken);
        return Ok(ApiResponse<PagedResult<DecisionTableListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<DecisionTableResponse>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(id, tenantId, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<DecisionTableResponse>.Fail("NOT_FOUND",
                ApiResponseLocalizer.T(HttpContext, "ResourceNotFound"), HttpContext.TraceIdentifier));
        return Ok(ApiResponse<DecisionTableResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Create([FromBody] DecisionTableCreateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<long>.Fail("VALIDATION_ERROR",
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), HttpContext.TraceIdentifier));

        var user = _currentUser.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(request, tenantId, user.Username, cancellationToken);
        return Ok(ApiResponse<long>.Ok(id, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, [FromBody] DecisionTableUpdateRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR",
                ApiResponseLocalizer.T(HttpContext, "IdMismatch"), HttpContext.TraceIdentifier));

        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR",
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), HttpContext.TraceIdentifier));

        var user = _currentUser.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(request, tenantId, user.Username, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("execute")]
    public async Task<ActionResult<ApiResponse<DecisionTableExecuteResponse>>> Execute([FromBody] DecisionTableExecuteRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ExecuteAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<DecisionTableExecuteResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
