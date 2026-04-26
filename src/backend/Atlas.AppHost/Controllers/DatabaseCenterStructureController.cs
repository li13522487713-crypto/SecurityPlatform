using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/database-center/sources/{sourceId}/schemas/{schemaName}/structure")]
[Authorize]
public sealed class DatabaseCenterStructureController : ControllerBase
{
    private readonly IDatabaseStructureService _structureService;
    private readonly IDatabaseManagementService _managementService;
    private readonly AiDatabasePhysicalInstanceRepository _instanceRepository;
    private readonly ITenantProvider _tenantProvider;

    public DatabaseCenterStructureController(
        IDatabaseStructureService structureService,
        IDatabaseManagementService managementService,
        AiDatabasePhysicalInstanceRepository instanceRepository,
        ITenantProvider tenantProvider)
    {
        _structureService = structureService;
        _managementService = managementService;
        _instanceRepository = instanceRepository;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<object>>> GetStructure(
        string sourceId,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        var instance = await ResolveInstanceAsync(sourceId, cancellationToken);
        var schemas = await _managementService.ListSchemasAsync(_tenantProvider.GetTenantId(), sourceId, cancellationToken);
        var schema = schemas.FirstOrDefault(x => string.Equals(x.Name, schemaName, StringComparison.OrdinalIgnoreCase));
        var objects = schema?.Groups.SelectMany(x => x.Objects).Select(x => new
        {
            id = $"{x.Schema ?? schemaName}:{x.ObjectType}:{x.Name}",
            name = x.Name,
            objectType = x.ObjectType,
            schema = x.Schema ?? schemaName,
            canPreview = x.CanPreview,
            canDrop = x.CanDrop
        }).ToList() ?? [];
        var columnsByObject = new Dictionary<string, IReadOnlyList<DatabaseColumnDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in objects.Where(x => string.Equals(x.objectType, "table", StringComparison.OrdinalIgnoreCase) || string.Equals(x.objectType, "view", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                columnsByObject[item.id] = string.Equals(item.objectType, "view", StringComparison.OrdinalIgnoreCase)
                    ? await _structureService.GetViewColumnsAsync(_tenantProvider.GetTenantId(), instance.AiDatabaseId, instance.Environment, item.name, schemaName, cancellationToken)
                    : await _structureService.GetTableColumnsAsync(_tenantProvider.GetTenantId(), instance.AiDatabaseId, instance.Environment, item.name, schemaName, cancellationToken);
            }
            catch
            {
                columnsByObject[item.id] = [];
            }
        }

        var result = new
        {
            sourceId,
            schemaName,
            environment = instance.Environment.ToString(),
            objects,
            columnsByObject,
            relations = Array.Empty<object>()
        };
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("objects")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseCenterSchemaObjectDto>>>> GetObjects(
        string sourceId,
        string schemaName,
        [FromQuery] string type = "table",
        CancellationToken cancellationToken = default)
    {
        var schemas = await _managementService.ListSchemasAsync(_tenantProvider.GetTenantId(), sourceId, cancellationToken);
        var schema = schemas.FirstOrDefault(x => string.Equals(x.Name, schemaName, StringComparison.OrdinalIgnoreCase));
        var group = schema?.Groups.FirstOrDefault(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
        return Ok(ApiResponse<IReadOnlyList<DatabaseCenterSchemaObjectDto>>.Ok(group?.Objects ?? [], HttpContext.TraceIdentifier));
    }

    [HttpGet("tables/{tableName}/columns")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseColumnDto>>>> GetTableColumns(
        string sourceId,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
        => await Execute(sourceId, (databaseId, environment) => _structureService.GetTableColumnsAsync(_tenantProvider.GetTenantId(), databaseId, environment, tableName, schemaName, cancellationToken));

    [HttpGet("views/{viewName}/columns")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseColumnDto>>>> GetViewColumns(
        string sourceId,
        string schemaName,
        string viewName,
        CancellationToken cancellationToken)
        => await Execute(sourceId, (databaseId, environment) => _structureService.GetViewColumnsAsync(_tenantProvider.GetTenantId(), databaseId, environment, viewName, schemaName, cancellationToken));

    [HttpGet("tables/{tableName}/ddl")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<DdlResponse>>> GetTableDdl(
        string sourceId,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
        => await Execute(sourceId, (databaseId, environment) => _structureService.GetTableDdlAsync(_tenantProvider.GetTenantId(), databaseId, environment, tableName, schemaName, cancellationToken));

    [HttpGet("views/{viewName}/ddl")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<DdlResponse>>> GetViewDdl(
        string sourceId,
        string schemaName,
        string viewName,
        CancellationToken cancellationToken)
        => await Execute(sourceId, (databaseId, environment) => _structureService.GetViewDdlAsync(_tenantProvider.GetTenantId(), databaseId, environment, viewName, schemaName, cancellationToken));

    [HttpPost("tables/{tableName}/preview")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<PreviewDataResponse>>> PreviewTable(
        string sourceId,
        string schemaName,
        string tableName,
        [FromBody] PreviewDataRequest request,
        CancellationToken cancellationToken)
        => await Execute(sourceId, (databaseId, environment) => _structureService.PreviewTableDataAsync(_tenantProvider.GetTenantId(), databaseId, tableName, request with { Schema = schemaName, Environment = environment }, cancellationToken));

    [HttpPost("views/{viewName}/preview")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<PreviewDataResponse>>> PreviewView(
        string sourceId,
        string schemaName,
        string viewName,
        [FromBody] PreviewDataRequest request,
        CancellationToken cancellationToken)
        => await Execute(sourceId, (databaseId, environment) => _structureService.PreviewViewDataAsync(_tenantProvider.GetTenantId(), databaseId, viewName, request with { Schema = schemaName, Environment = environment }, cancellationToken));

    [HttpPost("tables/preview-ddl")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<DdlResponse>>> PreviewCreateTableDdl(
        string sourceId,
        string schemaName,
        [FromBody] PreviewCreateTableDdlRequest request,
        CancellationToken cancellationToken)
        => await ExecuteDraft(sourceId, databaseId => _structureService.BuildCreateTableDdlAsync(_tenantProvider.GetTenantId(), databaseId, request with { Schema = schemaName }, cancellationToken));

    [HttpPost("tables")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> CreateTable(
        string sourceId,
        string schemaName,
        [FromBody] CreateTableRequest request,
        CancellationToken cancellationToken)
        => await ExecuteDraftObject(sourceId, async databaseId =>
        {
            await _structureService.CreateTableAsync(_tenantProvider.GetTenantId(), databaseId, request with { Schema = schemaName }, cancellationToken);
            return new { request.TableName };
        });

    [HttpPost("tables/sql")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> CreateTableBySql(
        string sourceId,
        [FromBody] CreateTableSqlRequest request,
        CancellationToken cancellationToken)
        => await ExecuteDraftObject(sourceId, async databaseId =>
        {
            await _structureService.CreateTableBySqlAsync(_tenantProvider.GetTenantId(), databaseId, request, cancellationToken);
            return new { Success = true };
        });

    [HttpPost("views/preview")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<PreviewDataResponse>>> PreviewViewSql(
        string sourceId,
        string schemaName,
        [FromBody] PreviewViewSqlRequest request,
        CancellationToken cancellationToken)
        => await Execute(sourceId, (databaseId, environment) => _structureService.PreviewViewSqlAsync(
            _tenantProvider.GetTenantId(),
            databaseId,
            request with { Schema = schemaName, Environment = environment },
            cancellationToken));

    [HttpPost("views")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> CreateView(
        string sourceId,
        string schemaName,
        [FromBody] CreateViewRequest request,
        CancellationToken cancellationToken)
        => await ExecuteDraftObject(sourceId, async databaseId =>
        {
            await _structureService.CreateViewAsync(_tenantProvider.GetTenantId(), databaseId, request with { Schema = schemaName }, cancellationToken);
            return new { request.ViewName };
        });

    [HttpDelete("tables/{tableName}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> DropTable(
        string sourceId,
        string schemaName,
        string tableName,
        [FromBody] DropDatabaseObjectRequest request,
        CancellationToken cancellationToken)
        => await ExecuteDraftObject(sourceId, async databaseId =>
        {
            await _structureService.DropTableAsync(_tenantProvider.GetTenantId(), databaseId, tableName, request with { Schema = schemaName }, cancellationToken);
            return new { Name = tableName };
        });

    [HttpDelete("views/{viewName}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> DropView(
        string sourceId,
        string schemaName,
        string viewName,
        [FromBody] DropDatabaseObjectRequest request,
        CancellationToken cancellationToken)
        => await ExecuteDraftObject(sourceId, async databaseId =>
        {
            await _structureService.DropViewAsync(_tenantProvider.GetTenantId(), databaseId, viewName, request with { Schema = schemaName }, cancellationToken);
            return new { Name = viewName };
        });

    private async Task<ActionResult<ApiResponse<T>>> Execute<T>(
        string sourceId,
        Func<long, AiDatabaseRecordEnvironment, Task<T>> action)
    {
        try
        {
            var instance = await ResolveInstanceAsync(sourceId, CancellationToken.None);
            var result = await action(instance.AiDatabaseId, instance.Environment);
            return Ok(ApiResponse<T>.Ok(result, HttpContext.TraceIdentifier));
        }
        catch (SqlSafetyException ex)
        {
            return BadRequest(ApiResponse<T>.Fail(ErrorCodes.ValidationError, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    private async Task<ActionResult<ApiResponse<T>>> ExecuteDraft<T>(string sourceId, Func<long, Task<T>> action)
    {
        var instance = await ResolveInstanceAsync(sourceId, CancellationToken.None);
        if (instance.Environment == AiDatabaseRecordEnvironment.Online)
        {
            return BadRequest(ApiResponse<T>.Fail(ErrorCodes.ValidationError, "Online 实例只读，禁止结构编辑。", HttpContext.TraceIdentifier));
        }

        var result = await action(instance.AiDatabaseId);
        return Ok(ApiResponse<T>.Ok(result, HttpContext.TraceIdentifier));
    }

    private async Task<ActionResult<ApiResponse<object>>> ExecuteDraftObject(string sourceId, Func<long, Task<object>> action)
    {
        var result = await ExecuteDraft(sourceId, action);
        return result;
    }

    private async Task<AiDatabasePhysicalInstance> ResolveInstanceAsync(string sourceId, CancellationToken cancellationToken)
    {
        var raw = sourceId.StartsWith("ai:", StringComparison.OrdinalIgnoreCase) ? sourceId[3..] : sourceId;
        if (!long.TryParse(raw, out var id) || id <= 0)
        {
            throw new BusinessException("sourceId 必须是有效字符串 ID。", ErrorCodes.ValidationError);
        }

        return await _instanceRepository.FindByIdAsync(_tenantProvider.GetTenantId(), id, cancellationToken)
            ?? throw new BusinessException("数据源不存在。", ErrorCodes.NotFound);
    }
}
