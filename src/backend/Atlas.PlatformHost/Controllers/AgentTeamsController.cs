using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Application.AgentTeam.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AgentTeam.Entities;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/agent-teams")]
public sealed class AgentTeamsController : ControllerBase
{
    private readonly IAgentTeamQueryService _queryService;
    private readonly IAgentTeamCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AgentTeamCreateRequest> _createValidator;
    private readonly IValidator<AgentTeamUpdateRequest> _updateValidator;
    private readonly IValidator<TeamPublishRequest> _publishValidator;

    public AgentTeamsController(
        IAgentTeamQueryService queryService,
        IAgentTeamCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AgentTeamCreateRequest> createValidator,
        IValidator<AgentTeamUpdateRequest> updateValidator,
        IValidator<TeamPublishRequest> publishValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _publishValidator = publishValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AgentTeamListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] TeamStatus? status = null,
        [FromQuery] TeamPublishStatus? publishStatus = null,
        [FromQuery] TeamRiskLevel? riskLevel = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPagedAsync(
            tenantId,
            keyword,
            status,
            publishStatus,
            riskLevel,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AgentTeamListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AgentTeamDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AgentTeamDetail>.Fail(ErrorCodes.NotFound, "未找到 Agent 团队。", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AgentTeamDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] AgentTeamCreateRequest request, CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, [FromBody] AgentTeamUpdateRequest request, CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/duplicate")]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Duplicate(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var duplicatedId = await _commandService.DuplicateAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = duplicatedId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/disable")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Disable(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DisableAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/enable")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Enable(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.EnableAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(long id, [FromBody] TeamPublishRequest request, CancellationToken cancellationToken)
    {
        _publishValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var publisher = _currentUserAccessor.GetCurrentUserOrThrow().UserId.ToString();
        var versionId = await _commandService.PublishAsync(tenantId, id, publisher, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = versionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamVersionItem>>>> Versions(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetVersionsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeamVersionItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/rollback/{versionId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Rollback(long id, long versionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var publisher = _currentUserAccessor.GetCurrentUserOrThrow().UserId.ToString();
        await _commandService.RollbackAsync(tenantId, id, versionId, publisher, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), VersionId = versionId.ToString() }, HttpContext.TraceIdentifier));
    }
}
