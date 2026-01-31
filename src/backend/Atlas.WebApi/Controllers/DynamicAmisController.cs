using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Models;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/amis/dynamic-tables")]
public sealed class DynamicAmisController : ControllerBase
{
    private readonly IDynamicTableQueryService _queryService;
    private readonly Atlas.Core.Tenancy.ITenantProvider _tenantProvider;
    private readonly string _schemaDirectory;

    public DynamicAmisController(
        IDynamicTableQueryService queryService,
        Atlas.Core.Tenancy.ITenantProvider tenantProvider,
        IHostEnvironment environment)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
        _schemaDirectory = Path.Combine(environment.ContentRootPath, "AmisSchemas", "dynamic-tables");
    }

    [HttpGet("list")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<JsonElement>>> GetListSchema(CancellationToken cancellationToken)
    {
        var schema = await ReadSchemaAsync("list.json", cancellationToken);
        if (schema is null)
        {
            return NotFound(ApiResponse<JsonElement>.Fail(
                ErrorCodes.NotFound,
                "动态表列表Schema未找到",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<JsonElement>.Ok(schema.Value, HttpContext.TraceIdentifier));
    }

    [HttpGet("designer")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<JsonElement>>> GetDesignerSchema(CancellationToken cancellationToken)
    {
        var schema = await ReadSchemaAsync("designer.json", cancellationToken);
        if (schema is null)
        {
            return NotFound(ApiResponse<JsonElement>.Fail(
                ErrorCodes.NotFound,
                "设计器Schema未找到",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<JsonElement>.Ok(schema.Value, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}/crud")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<JsonElement>>> GetCrudSchema(
        string tableKey,
        CancellationToken cancellationToken)
    {
        var schema = await ReadSchemaAsync("crud.json", cancellationToken);
        if (schema is null)
        {
            return NotFound(ApiResponse<JsonElement>.Fail(
                ErrorCodes.NotFound,
                "CRUD Schema未找到",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<JsonElement>.Ok(schema.Value, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}/forms/create")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<JsonElement>>> GetCreateForm(
        string tableKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var fields = await _queryService.GetFieldsAsync(
            tenantId,
            tableKey,
            cancellationToken);
        var schema = BuildFormSchema(tableKey, fields, isEdit: false);
        return Ok(ApiResponse<JsonElement>.Ok(schema, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}/forms/edit")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<JsonElement>>> GetEditForm(
        string tableKey,
        [FromQuery] long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var fields = await _queryService.GetFieldsAsync(
            tenantId,
            tableKey,
            cancellationToken);
        var schema = BuildFormSchema(tableKey, fields, isEdit: true, id);
        return Ok(ApiResponse<JsonElement>.Ok(schema, HttpContext.TraceIdentifier));
    }

    private async Task<JsonElement?> ReadSchemaAsync(string fileName, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_schemaDirectory, fileName);
        if (!System.IO.File.Exists(filePath))
        {
            return null;
        }

        var text = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    private static JsonElement BuildFormSchema(
        string tableKey,
        IReadOnlyList<DynamicFieldDefinition> fields,
        bool isEdit,
        long id = 0)
    {
        var body = new List<object>();
        var fieldTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            if (field.IsAutoIncrement && field.IsPrimaryKey)
            {
                continue;
            }

            fieldTypeMap[field.Name] = field.FieldType;
            body.Add(BuildFormItem(field));
        }

        var api = new Dictionary<string, object?>
        {
            ["method"] = isEdit ? "put" : "post",
            ["url"] = isEdit
                ? $"/api/v1/dynamic-tables/{tableKey}/records/{id}"
                : $"/api/v1/dynamic-tables/{tableKey}/records",
            ["requestAdaptor"] = BuildRequestAdaptor()
        };

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "form",
            ["api"] = api,
            ["data"] = new Dictionary<string, object?>
            {
                ["__fieldTypes"] = fieldTypeMap
            },
            ["body"] = body
        };

        if (isEdit)
        {
            schema["initApi"] = new Dictionary<string, object?>
            {
                ["method"] = "get",
                ["url"] = $"/api/v1/dynamic-tables/{tableKey}/records/{id}",
                ["adaptor"] = BuildInitAdaptor()
            };
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string BuildRequestAdaptor()
    {
        return """
        var values = [];
        var data = api.data || {};
        var fieldTypes = data.__fieldTypes || {};
        Object.keys(data).forEach(function(key) {
          if (key === '__fieldTypes') { return; }
          var type = fieldTypes[key] || 'String';
          var value = data[key];
          if (value === undefined || value === null) { return; }
          var item = { field: key, valueType: type };
          if (type === 'Int') { item.intValue = value; }
          else if (type === 'Long') { item.longValue = value; }
          else if (type === 'Decimal') { item.decimalValue = value; }
          else if (type === 'Bool') { item.boolValue = value; }
          else if (type === 'DateTime') { item.dateTimeValue = value; }
          else if (type === 'Date') { item.dateValue = value; }
          else { item.stringValue = value; }
          values.push(item);
        });
        api.data = { values: values };
        return api;
        """;
    }

    private static string BuildInitAdaptor()
    {
        return """
        if (!payload.success) {
          return { status: 1, msg: payload.message };
        }
        var data = {};
        var values = (payload.data && payload.data.values) ? payload.data.values : [];
        values.forEach(function(v) {
          var val = v.stringValue ?? v.intValue ?? v.longValue ?? v.decimalValue ?? v.boolValue ?? v.dateTimeValue ?? v.dateValue;
          data[v.field] = val;
        });
        return { status: 0, data: data };
        """;
    }

    private static object BuildFormItem(DynamicFieldDefinition field)
    {
        return field.FieldType switch
        {
            "Int" => new Dictionary<string, object?>
            {
                ["type"] = "input-number",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name
            },
            "Long" => new Dictionary<string, object?>
            {
                ["type"] = "input-number",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name
            },
            "Decimal" => new Dictionary<string, object?>
            {
                ["type"] = "input-number",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name,
                ["precision"] = field.Scale ?? 2
            },
            "Bool" => new Dictionary<string, object?>
            {
                ["type"] = "switch",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name
            },
            "DateTime" => new Dictionary<string, object?>
            {
                ["type"] = "input-datetime",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name,
                ["format"] = "YYYY-MM-DD HH:mm:ss"
            },
            "Date" => new Dictionary<string, object?>
            {
                ["type"] = "input-date",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name
            },
            _ => new Dictionary<string, object?>
            {
                ["type"] = "input-text",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name
            }
        };
    }
}
