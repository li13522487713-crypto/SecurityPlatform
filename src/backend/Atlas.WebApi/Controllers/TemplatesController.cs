using Atlas.Application.Templates;
using Atlas.Core.Models;
using Atlas.Domain.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 组件模板库 API
/// </summary>
[ApiController]
[Route("api/v1/templates")]
[Authorize]
public sealed class TemplatesController : ControllerBase
{
    private readonly IComponentTemplateQueryService _queryService;
    private readonly IComponentTemplateCommandService _commandService;

    public TemplatesController(
        IComponentTemplateQueryService queryService,
        IComponentTemplateCommandService commandService)
    {
        _queryService = queryService;
        _commandService = commandService;
    }

    /// <summary>分页查询模板列表（支持 keyword/category 筛选）</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Search(
        [FromQuery] string? keyword = null,
        [FromQuery] TemplateCategory? category = null,
        [FromQuery] string? tags = null,
        [FromQuery] string? version = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _queryService.SearchAsync(keyword, category, tags, version, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Total = total,
            Items = items
        }, HttpContext.TraceIdentifier));
    }

    /// <summary>获取模板详情</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<ComponentTemplate>>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var template = await _queryService.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return NotFound(ApiResponse<ComponentTemplate>.Fail("NOT_FOUND", "模板不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ComponentTemplate>.Ok(template, HttpContext.TraceIdentifier));
    }

    /// <summary>创建模板</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _commandService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    /// <summary>更新模板</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commandService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>删除模板</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken = default)
    {
        await _commandService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>从模板实例化（返回 Schema JSON）</summary>
    [HttpPost("{id:long}/instantiate")]
    public async Task<ActionResult<ApiResponse<object>>> Instantiate(
        long id,
        CancellationToken cancellationToken = default)
    {
        var schemaJson = await _commandService.InstantiateAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { SchemaJson = schemaJson }, HttpContext.TraceIdentifier));
    }
}
