using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Nodes;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/node-types")]
[Authorize]
[PlatformOnly]
public sealed class NodeTypesController : ControllerBase
{
    private readonly INodeTypeQueryService _queryService;
    private readonly INodeTypeCommandService _commandService;
    private readonly INodeTypeRegistry _registry;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<NodeTypeCreateRequest> _createValidator;

    public NodeTypesController(
        INodeTypeQueryService queryService,
        INodeTypeCommandService commandService,
        INodeTypeRegistry registry,
        ITenantProvider tenantProvider,
        IValidator<NodeTypeCreateRequest> createValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _registry = registry;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NodeTypeListItem>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] NodeCategory? category = null,
        [FromQuery] bool? isBuiltIn = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var request = new NodeTypeQueryRequest
        {
            PageIndex = pageIndex, PageSize = pageSize,
            Keyword = keyword, Category = category, IsBuiltIn = isBuiltIn,
        };
        var result = await _queryService.QueryAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<NodeTypeListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<NodeTypeDetailResponse>>> GetById(
        long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(id, tenantId, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<NodeTypeDetailResponse>.Fail("NOT_FOUND",
                ApiResponseLocalizer.T(HttpContext, "ResourceNotFound"), HttpContext.TraceIdentifier));
        return Ok(ApiResponse<NodeTypeDetailResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("by-key/{typeKey}")]
    public async Task<ActionResult<ApiResponse<NodeTypeDetailResponse>>> GetByTypeKey(
        string typeKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByTypeKeyAsync(typeKey, tenantId, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<NodeTypeDetailResponse>.Fail("NOT_FOUND",
                ApiResponseLocalizer.T(HttpContext, "ResourceNotFound"), HttpContext.TraceIdentifier));
        return Ok(ApiResponse<NodeTypeDetailResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NodeCategoryInfo>>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var result = await _queryService.GetCategoriesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NodeCategoryInfo>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 获取内存注册表中的所有内置节点声明（含端口、能力、UI 元数据）。
    /// </summary>
    [HttpGet("registry")]
    public ActionResult<ApiResponse<IReadOnlyList<NodeRegistryItem>>> GetRegistryAll(
        [FromQuery] NodeCategory? category = null)
    {
        var declarations = category.HasValue
            ? _registry.GetByCategory(category.Value)
            : _registry.GetAll();

        var items = declarations.Select(d => new NodeRegistryItem(
            d.TypeKey, d.Category, d.DisplayName, d.Description,
            d.GetPortDefinitions().ToList(),
            d.GetCapabilities(),
            d.GetUiMetadata())).ToList();

        return Ok(ApiResponse<IReadOnlyList<NodeRegistryItem>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] NodeTypeCreateRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR",
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), HttpContext.TraceIdentifier));

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] NodeTypeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await _commandService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id, CancellationToken cancellationToken)
    {
        await _commandService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

public sealed record NodeRegistryItem(
    string TypeKey,
    NodeCategory Category,
    string DisplayName,
    string? Description,
    List<PortDefinition> Ports,
    NodeCapability Capabilities,
    NodeUiMetadata UiMetadata);
