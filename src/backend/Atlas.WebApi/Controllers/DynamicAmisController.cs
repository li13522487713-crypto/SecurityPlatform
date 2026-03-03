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
        var tenantId = _tenantProvider.GetTenantId();
        var fields = await _queryService.GetFieldsAsync(
            tenantId,
            tableKey,
            cancellationToken);
        var schema = BuildCrudSchema(tableKey, fields);
        return Ok(ApiResponse<JsonElement>.Ok(schema, HttpContext.TraceIdentifier));
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

    [HttpGet("{tableKey}/forms/detail")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<JsonElement>>> GetDetailForm(
        string tableKey,
        [FromQuery] long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var fields = await _queryService.GetFieldsAsync(
            tenantId,
            tableKey,
            cancellationToken);
        var schema = BuildDetailSchema(tableKey, fields, id);
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

    private static JsonElement BuildCrudSchema(
        string tableKey,
        IReadOnlyList<DynamicFieldDefinition> fields)
    {
        var columns = new List<object>();
        foreach (var field in fields.OrderBy(x => x.SortOrder))
        {
            columns.Add(BuildCrudColumn(field));
        }
        var filterBody = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["type"] = "input-text",
                ["name"] = "keyword",
                ["label"] = "关键字"
            }
        };

        foreach (var field in fields.OrderBy(x => x.SortOrder))
        {
            var filterItem = BuildCrudFilterItem(field);
            if (filterItem is not null)
            {
                filterBody.Add(filterItem);
            }
        }

        columns.Add(new Dictionary<string, object?>
        {
            ["type"] = "operation",
            ["label"] = "操作",
            ["buttons"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["label"] = "详情",
                    ["actionType"] = "dialog",
                    ["dialog"] = new Dictionary<string, object?>
                    {
                        ["title"] = "记录详情",
                        ["size"] = "lg",
                        ["body"] = new Dictionary<string, object?>
                        {
                            ["type"] = "service",
                            ["schemaApi"] = $"get:/api/v1/amis/dynamic-tables/{tableKey}/forms/detail?id=${{id}}"
                        }
                    }
                },
                new Dictionary<string, object?>
                {
                    ["label"] = "编辑",
                    ["actionType"] = "dialog",
                    ["dialog"] = new Dictionary<string, object?>
                    {
                        ["title"] = "编辑记录",
                        ["size"] = "lg",
                        ["body"] = new Dictionary<string, object?>
                        {
                            ["type"] = "service",
                            ["schemaApi"] = $"get:/api/v1/amis/dynamic-tables/{tableKey}/forms/edit?id=${{id}}"
                        }
                    }
                },
                new Dictionary<string, object?>
                {
                    ["label"] = "删除",
                    ["actionType"] = "ajax",
                    ["confirmText"] = "确认删除该记录？",
                    ["api"] = new Dictionary<string, object?>
                    {
                        ["method"] = "delete",
                        ["url"] = $"/api/v1/dynamic-tables/{tableKey}/records/${{id}}",
                        ["adaptor"] = "return { status: payload.success ? 0 : 1, msg: payload.message };"
                    }
                }
            }
        });

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "page",
            ["title"] = "${tableDisplayName | default: '动态数据管理'}",
            ["body"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "crud",
                    ["name"] = "crudTable",
                    ["api"] = new Dictionary<string, object?>
                    {
                        ["method"] = "post",
                        ["url"] = $"/api/v1/dynamic-tables/{tableKey}/records/query",
                        ["requestAdaptor"] = BuildCrudRequestAdaptor(),
                        ["adaptor"] = BuildCrudAdaptor(),
                        ["data"] = new Dictionary<string, object?>
                        {
                            ["pageIndex"] = "${page}",
                            ["perPage"] = "${perPage}",
                            ["keyword"] = "${keyword}",
                            ["sortBy"] = "${orderBy}",
                            ["sortDesc"] = "${orderDir === 'desc'}"
                        }
                    },
                    ["pageField"] = "page",
                    ["perPageField"] = "perPage",
                    ["perPageAvailable"] = new[] { 10, 20, 50, 100 },
                    ["pageSize"] = 20,
                    ["syncLocation"] = false,
                    ["filter"] = new Dictionary<string, object?>
                    {
                        ["title"] = "搜索",
                        ["submitText"] = "查询",
                        ["body"] = filterBody
                    },
                    ["headerToolbar"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "button",
                            ["label"] = "新增",
                            ["level"] = "primary",
                            ["actionType"] = "dialog",
                            ["dialog"] = new Dictionary<string, object?>
                            {
                                ["title"] = "新增记录",
                                ["size"] = "lg",
                                ["body"] = new Dictionary<string, object?>
                                {
                                    ["type"] = "service",
                                    ["schemaApi"] = $"get:/api/v1/amis/dynamic-tables/{tableKey}/forms/create"
                                }
                            }
                        },
                        "bulkActions",
                        "pagination"
                    },
                    ["bulkActions"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["label"] = "批量删除",
                            ["actionType"] = "ajax",
                            ["confirmText"] = "确认删除选中记录？",
                            ["api"] = new Dictionary<string, object?>
                            {
                                ["method"] = "delete",
                                ["url"] = $"/api/v1/dynamic-tables/{tableKey}/records",
                                ["data"] = new Dictionary<string, object?>
                                {
                                    ["ids"] = "${ids}"
                                },
                                ["adaptor"] = "return { status: payload.success ? 0 : 1, msg: payload.message };"
                            }
                        }
                    },
                    ["columns"] = columns
                }
            }
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement BuildDetailSchema(
        string tableKey,
        IReadOnlyList<DynamicFieldDefinition> fields,
        long id)
    {
        var body = fields
            .OrderBy(x => x.SortOrder)
            .Select(field => new Dictionary<string, object?>
            {
                ["type"] = "static",
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name
            })
            .ToArray();

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "form",
            ["wrapWithPanel"] = false,
            ["mode"] = "horizontal",
            ["initApi"] = new Dictionary<string, object?>
            {
                ["method"] = "get",
                ["url"] = $"/api/v1/dynamic-tables/{tableKey}/records/{id}",
                ["adaptor"] = BuildInitAdaptor()
            },
            ["body"] = body
        };

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

    private static string BuildCrudAdaptor()
    {
        return """
        if (!payload.success) {
          return { status: 1, msg: payload.message };
        }
        var d = payload.data || {};
        var items = (d.items || []).map(function(r) {
          var row = { id: r.id };
          (r.values || []).forEach(function(v) {
            var val = v.stringValue ?? v.intValue ?? v.longValue ?? v.decimalValue ?? v.boolValue ?? v.dateTimeValue ?? v.dateValue;
            row[v.field] = val;
          });
          return row;
        });
        return {
          status: 0,
          data: {
            items: items,
            total: d.total || 0,
            pageIndex: d.pageIndex || 1,
            perPage: d.pageSize || 20
          }
        };
        """;
    }

    private static string BuildCrudRequestAdaptor()
    {
        return """
        var data = api.data || {};
        var filters = [];
        Object.keys(data).forEach(function(key) {
          if (!key.startsWith('f_')) { return; }
          var field = key.substring(2);
          var value = data[key];
          if (value === undefined || value === null || value === '' || (Array.isArray(value) && value.length === 0)) {
            return;
          }

          var op = 'eq';
          var payloadValue = value;
          if (Array.isArray(value) && value.length >= 2) {
            op = 'between';
            payloadValue = [value[0], value[1]];
          } else if (typeof value === 'string') {
            op = 'like';
            payloadValue = value;
          }

          filters.push({
            field: field,
            operator: op,
            value: payloadValue
          });
        });

        api.data = {
          pageIndex: data.page || 1,
          pageSize: data.perPage || 20,
          keyword: data.keyword || null,
          sortBy: data.sortBy || null,
          sortDesc: data.sortDesc === true || data.sortDesc === 'true',
          filters: filters
        };

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

    private static object BuildCrudColumn(DynamicFieldDefinition field)
    {
        return field.FieldType switch
        {
            "Bool" => new Dictionary<string, object?>
            {
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name,
                ["type"] = "status",
                ["map"] = new Dictionary<string, object?>
                {
                    ["0"] = "danger",
                    ["1"] = "success"
                }
            },
            "DateTime" => new Dictionary<string, object?>
            {
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name,
                ["type"] = "datetime",
                ["format"] = "YYYY-MM-DD HH:mm:ss",
                ["sortable"] = true
            },
            "Date" => new Dictionary<string, object?>
            {
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name,
                ["type"] = "date",
                ["format"] = "YYYY-MM-DD",
                ["sortable"] = true
            },
            _ => new Dictionary<string, object?>
            {
                ["name"] = field.Name,
                ["label"] = field.DisplayName ?? field.Name,
                ["type"] = "text",
                ["sortable"] = true
            }
        };
    }

    private static object? BuildCrudFilterItem(DynamicFieldDefinition field)
    {
        return field.FieldType switch
        {
            "String" or "Text" => new Dictionary<string, object?>
            {
                ["type"] = "input-text",
                ["name"] = $"f_{field.Name}",
                ["label"] = field.DisplayName ?? field.Name
            },
            "Int" or "Long" or "Decimal" => new Dictionary<string, object?>
            {
                ["type"] = "input-number",
                ["name"] = $"f_{field.Name}",
                ["label"] = field.DisplayName ?? field.Name
            },
            "Bool" => new Dictionary<string, object?>
            {
                ["type"] = "select",
                ["name"] = $"f_{field.Name}",
                ["label"] = field.DisplayName ?? field.Name,
                ["clearable"] = true,
                ["options"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["label"] = "是",
                        ["value"] = true
                    },
                    new Dictionary<string, object?>
                    {
                        ["label"] = "否",
                        ["value"] = false
                    }
                }
            },
            "Date" => new Dictionary<string, object?>
            {
                ["type"] = "input-date-range",
                ["name"] = $"f_{field.Name}",
                ["label"] = field.DisplayName ?? field.Name,
                ["format"] = "YYYY-MM-DD"
            },
            "DateTime" => new Dictionary<string, object?>
            {
                ["type"] = "input-datetime-range",
                ["name"] = $"f_{field.Name}",
                ["label"] = field.DisplayName ?? field.Name,
                ["format"] = "YYYY-MM-DD HH:mm:ss"
            },
            _ => null
        };
    }
}
