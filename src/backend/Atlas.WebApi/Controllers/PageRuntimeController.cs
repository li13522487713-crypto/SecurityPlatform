using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 低代码页面运行态控制器：面向应用普通用户开放，支持读取发布态 Schema 及提交表单数据到绑定的动态表。
/// </summary>
[ApiController]
[Route("api/v1/runtime")]
[Authorize]
public sealed class PageRuntimeController : ControllerBase
{
    private readonly ILowCodeAppQueryService _queryService;
    private readonly IDynamicRecordCommandService _recordCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PageRuntimeController(
        ILowCodeAppQueryService queryService,
        IDynamicRecordCommandService recordCommandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _recordCommandService = recordCommandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>
    /// 通过应用 Key + 页面 Key 获取已发布的运行态 Schema（对所有已认证用户开放）。
    /// </summary>
    [HttpGet("apps/{appKey}/pages/{pageKey}/schema")]
    public async Task<ActionResult<ApiResponse<LowCodePageRuntimeSchema?>>> GetSchema(
        string appKey,
        string pageKey,
        [FromQuery] string? environmentCode = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var app = await _queryService.GetByKeyAsync(tenantId, appKey, cancellationToken);
        if (app is null)
        {
            return NotFound(ApiResponse<LowCodePageRuntimeSchema?>.Fail(
                ErrorCodes.NotFound, $"应用 {appKey} 不存在", HttpContext.TraceIdentifier));
        }

        var page = app.Pages.FirstOrDefault(p =>
            string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase));
        if (page is null)
        {
            return NotFound(ApiResponse<LowCodePageRuntimeSchema?>.Fail(
                ErrorCodes.NotFound, $"页面 {pageKey} 不存在", HttpContext.TraceIdentifier));
        }

        if (!long.TryParse(page.Id, out var pageId))
        {
            return BadRequest(ApiResponse<LowCodePageRuntimeSchema?>.Fail(
                ErrorCodes.ValidationError, "页面 ID 格式无效", HttpContext.TraceIdentifier));
        }

        var schema = await _queryService.GetRuntimePageSchemaAsync(
            tenantId, pageId, "published", environmentCode, cancellationToken);

        return Ok(ApiResponse<LowCodePageRuntimeSchema?>.Ok(schema, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 向页面绑定的动态表提交记录（对所有已认证用户开放）。
    /// 页面必须绑定了 DataTableKey，否则返回 400。
    /// </summary>
    [HttpPost("apps/{appKey}/pages/{pageKey}/records")]
    public async Task<ActionResult<ApiResponse<object>>> CreateRecord(
        string appKey,
        string pageKey,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var app = await _queryService.GetByKeyAsync(tenantId, appKey, cancellationToken);
        if (app is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound, $"应用 {appKey} 不存在", HttpContext.TraceIdentifier));
        }

        var page = app.Pages.FirstOrDefault(p =>
            string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase));
        if (page is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound, $"页面 {pageKey} 不存在", HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(page.DataTableKey))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                $"页面 {pageKey} 未绑定动态表，无法提交记录",
                HttpContext.TraceIdentifier));
        }

        var request = ParseUpsertRequest(payload);
        var id = await _recordCommandService.CreateAsync(
            tenantId, currentUser.UserId, page.DataTableKey, request, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 更新页面绑定的动态表记录（对所有已认证用户开放）。
    /// </summary>
    [HttpPut("apps/{appKey}/pages/{pageKey}/records/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRecord(
        string appKey,
        string pageKey,
        long id,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var app = await _queryService.GetByKeyAsync(tenantId, appKey, cancellationToken);
        if (app is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound, $"应用 {appKey} 不存在", HttpContext.TraceIdentifier));
        }

        var page = app.Pages.FirstOrDefault(p =>
            string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase));
        if (page is null || string.IsNullOrWhiteSpace(page.DataTableKey))
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound, $"页面 {pageKey} 未绑定动态表", HttpContext.TraceIdentifier));
        }

        var request = ParseUpsertRequest(payload);
        await _recordCommandService.UpdateAsync(
            tenantId, currentUser.UserId, page.DataTableKey, id, request, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    private static DynamicRecordUpsertRequest ParseUpsertRequest(JsonElement payload)
    {
        // 兼容两类载荷：
        // 1) 标准 DynamicRecordUpsertRequest: { values: [...] }
        // 2) 运行态表单直传: { fieldA: xxx, fieldB: yyy }
        if (payload.ValueKind is JsonValueKind.Object && payload.TryGetProperty("values", out _))
        {
            var request = JsonSerializer.Deserialize<DynamicRecordUpsertRequest>(payload.GetRawText());
            if (request is not null)
            {
                return request;
            }
        }

        if (payload.ValueKind is not JsonValueKind.Object)
        {
            return new DynamicRecordUpsertRequest(Array.Empty<DynamicFieldValueDto>());
        }

        var values = new List<DynamicFieldValueDto>();
        foreach (var property in payload.EnumerateObject())
        {
            values.Add(ConvertField(property.Name, property.Value));
        }

        return new DynamicRecordUpsertRequest(values);
    }

    private static DynamicFieldValueDto ConvertField(string fieldName, JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "Bool",
                BoolValue = value.GetBoolean()
            },
            JsonValueKind.Number when value.TryGetInt32(out var intValue) => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "Int",
                IntValue = intValue
            },
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "Long",
                LongValue = longValue
            },
            JsonValueKind.Number => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "Decimal",
                DecimalValue = value.GetDecimal()
            },
            JsonValueKind.String when value.TryGetDateTimeOffset(out var dateTimeValue) => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "DateTime",
                DateTimeValue = dateTimeValue
            },
            JsonValueKind.String => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "String",
                StringValue = value.GetString()
            },
            JsonValueKind.Null => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "String",
                StringValue = null
            },
            _ => new DynamicFieldValueDto
            {
                Field = fieldName,
                ValueType = "String",
                StringValue = value.GetRawText()
            }
        };
    }
}
