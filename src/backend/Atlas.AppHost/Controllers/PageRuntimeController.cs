using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.AppHost.Sdk.Hosting;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/app/runtime")]
[Authorize(Policy = PermissionPolicies.AppUser)]
public sealed class PageRuntimeController : ControllerBase
{
    private readonly ILowCodeAppQueryService queryService;
    private readonly IDynamicRecordQueryService recordQueryService;
    private readonly IDynamicRecordCommandService recordCommandService;
    private readonly IRuntimeRouteQueryService runtimeRouteQueryService;
    private readonly ITenantProvider tenantProvider;
    private readonly ICurrentUserAccessor currentUserAccessor;
    private readonly IAppContextAccessor appContextAccessor;
    private readonly IClientContextAccessor clientContextAccessor;
    private readonly AppInstanceConfigurationLoader appInstanceConfigurationLoader;

    public PageRuntimeController(
        ILowCodeAppQueryService queryService,
        IDynamicRecordQueryService recordQueryService,
        IDynamicRecordCommandService recordCommandService,
        IRuntimeRouteQueryService runtimeRouteQueryService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor,
        IClientContextAccessor clientContextAccessor,
        AppInstanceConfigurationLoader appInstanceConfigurationLoader)
    {
        this.queryService = queryService;
        this.recordQueryService = recordQueryService;
        this.recordCommandService = recordCommandService;
        this.runtimeRouteQueryService = runtimeRouteQueryService;
        this.tenantProvider = tenantProvider;
        this.currentUserAccessor = currentUserAccessor;
        this.appContextAccessor = appContextAccessor;
        this.clientContextAccessor = clientContextAccessor;
        this.appInstanceConfigurationLoader = appInstanceConfigurationLoader;
    }

    [HttpGet("pages/{pageKey}/schema")]
    public async Task<ActionResult<ApiResponse<LowCodePageRuntimeSchema?>>> GetSchema(
        string pageKey,
        [FromQuery] string? environmentCode = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenantId();
        var descriptor = await ResolveDescriptorAsync(tenantId, pageKey, cancellationToken);
        if (descriptor is null)
        {
            return NotFound(ApiResponse<LowCodePageRuntimeSchema?>.Fail(
                ErrorCodes.NotFound,
                $"页面 {pageKey} 不存在",
                HttpContext.TraceIdentifier));
        }

        using var _ = BeginAppScope(descriptor);
        var schema = await queryService.GetRuntimePageSchemaAsync(
            tenantId,
            descriptor.PageId,
            "published",
            environmentCode,
            cancellationToken);

        return Ok(ApiResponse<LowCodePageRuntimeSchema?>.Ok(schema, HttpContext.TraceIdentifier));
    }

    [HttpGet("pages/{pageKey}/records")]
    public async Task<ActionResult<ApiResponse<DynamicRecordListResult>>> QueryRecords(
        string pageKey,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenantId();
        var descriptor = await ResolveDescriptorAsync(tenantId, pageKey, cancellationToken);
        if (descriptor is null)
        {
            return NotFound(ApiResponse<DynamicRecordListResult>.Fail(
                ErrorCodes.NotFound,
                $"页面 {pageKey} 不存在",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(descriptor.DataTableKey))
        {
            return BadRequest(ApiResponse<DynamicRecordListResult>.Fail(
                ErrorCodes.ValidationError,
                $"页面 {pageKey} 未绑定动态表，无法查询记录",
                HttpContext.TraceIdentifier));
        }

        using var _ = BeginAppScope(descriptor);
        var result = await recordQueryService.QueryAsync(
            tenantId,
            descriptor.DataTableKey,
            new DynamicRecordQueryRequest(pageIndex, pageSize, keyword, sortBy, sortDesc, null),
            cancellationToken);

        return Ok(ApiResponse<DynamicRecordListResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("pages/{pageKey}/records/{id:long}")]
    public async Task<ActionResult<ApiResponse<DynamicRecordDto?>>> GetRecordById(
        string pageKey,
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenantId();
        var descriptor = await ResolveDescriptorAsync(tenantId, pageKey, cancellationToken);
        if (descriptor is null)
        {
            return NotFound(ApiResponse<DynamicRecordDto?>.Fail(
                ErrorCodes.NotFound,
                $"页面 {pageKey} 不存在",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(descriptor.DataTableKey))
        {
            return BadRequest(ApiResponse<DynamicRecordDto?>.Fail(
                ErrorCodes.ValidationError,
                $"页面 {pageKey} 未绑定动态表，无法查询记录",
                HttpContext.TraceIdentifier));
        }

        using var _ = BeginAppScope(descriptor);
        var result = await recordQueryService.GetByIdAsync(tenantId, descriptor.DataTableKey, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DynamicRecordDto?>.Fail(
                ErrorCodes.NotFound,
                $"记录 {id} 不存在",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DynamicRecordDto?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("pages/{pageKey}/records")]
    public async Task<ActionResult<ApiResponse<object>>> CreateRecord(
        string pageKey,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = tenantProvider.GetTenantId();
        var descriptor = await ResolveDescriptorAsync(tenantId, pageKey, cancellationToken);
        if (descriptor is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound,
                $"页面 {pageKey} 不存在",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(descriptor.DataTableKey))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                $"页面 {pageKey} 未绑定动态表，无法提交记录",
                HttpContext.TraceIdentifier));
        }

        using var _ = BeginAppScope(descriptor);
        var request = ParseUpsertRequest(payload);
        var id = await recordCommandService.CreateAsync(
            tenantId,
            currentUser.UserId,
            descriptor.DataTableKey,
            request,
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("pages/{pageKey}/records/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRecord(
        string pageKey,
        long id,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = tenantProvider.GetTenantId();
        var descriptor = await ResolveDescriptorAsync(tenantId, pageKey, cancellationToken);
        if (descriptor is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound,
                $"页面 {pageKey} 不存在",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(descriptor.DataTableKey))
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound,
                $"页面 {pageKey} 未绑定动态表",
                HttpContext.TraceIdentifier));
        }

        using var _ = BeginAppScope(descriptor);
        var request = ParseUpsertRequest(payload);
        await recordCommandService.UpdateAsync(
            tenantId,
            currentUser.UserId,
            descriptor.DataTableKey,
            id,
            request,
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    private async Task<RuntimePageDescriptor?> ResolveDescriptorAsync(
        TenantId tenantId,
        string pageKey,
        CancellationToken cancellationToken)
    {
        var instanceConfig = appInstanceConfigurationLoader.Load();
        if (string.IsNullOrWhiteSpace(instanceConfig.AppKey))
        {
            return null;
        }

        var descriptor = await queryService.GetRuntimePageDescriptorAsync(
            tenantId,
            instanceConfig.AppKey,
            pageKey,
            cancellationToken);
        if (descriptor is null)
        {
            return null;
        }

        await runtimeRouteQueryService.GetRuntimePageAsync(
            tenantId,
            descriptor.AppId,
            instanceConfig.AppKey,
            pageKey,
            cancellationToken);
        return descriptor;
    }

    private IDisposable BeginAppScope(RuntimePageDescriptor descriptor)
    {
        var snapshot = new AppContextSnapshot(
            tenantProvider.GetTenantId(),
            descriptor.AppId.ToString(),
            currentUserAccessor.GetCurrentUser(),
            clientContextAccessor.GetCurrent(),
            HttpContext.TraceIdentifier);
        return appContextAccessor.BeginScope(snapshot);
    }

    private static DynamicRecordUpsertRequest ParseUpsertRequest(JsonElement payload)
    {
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
