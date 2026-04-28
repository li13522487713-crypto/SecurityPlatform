using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/database-resources/{databaseId}/structure")]
[Authorize]
public sealed class DatabaseStructureController : ControllerBase
{
    private readonly IDatabaseStructureService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAuditWriter _auditWriter;
    private readonly IDatabaseDialectRegistry _dialects;

    public DatabaseStructureController(
        IDatabaseStructureService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IAuditWriter auditWriter,
        IDatabaseDialectRegistry dialects)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _auditWriter = auditWriter;
        _dialects = dialects;
    }

    [HttpGet("objects")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseObjectDto>>>> GetObjects(
        string databaseId,
        [FromQuery] string type = "table",
        [FromQuery] AiDatabaseRecordEnvironment environment = AiDatabaseRecordEnvironment.Draft,
        CancellationToken cancellationToken = default)
        => await Execute(databaseId, parsedId => _service.GetObjectsAsync(_tenantProvider.GetTenantId(), parsedId, environment, type, cancellationToken));

    [HttpGet("tables/{tableName}/columns")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseColumnDto>>>> GetTableColumns(
        string databaseId,
        string tableName,
        [FromQuery] string? schema = null,
        [FromQuery] AiDatabaseRecordEnvironment environment = AiDatabaseRecordEnvironment.Draft,
        CancellationToken cancellationToken = default)
        => await Execute(databaseId, parsedId => _service.GetTableColumnsAsync(_tenantProvider.GetTenantId(), parsedId, environment, tableName, schema, cancellationToken));

    [HttpGet("views/{viewName}/columns")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseColumnDto>>>> GetViewColumns(
        string databaseId,
        string viewName,
        [FromQuery] string? schema = null,
        [FromQuery] AiDatabaseRecordEnvironment environment = AiDatabaseRecordEnvironment.Draft,
        CancellationToken cancellationToken = default)
        => await Execute(databaseId, parsedId => _service.GetViewColumnsAsync(_tenantProvider.GetTenantId(), parsedId, environment, viewName, schema, cancellationToken));

    [HttpGet("tables/{tableName}/ddl")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<DdlResponse>>> GetTableDdl(
        string databaseId,
        string tableName,
        [FromQuery] string? schema = null,
        [FromQuery] AiDatabaseRecordEnvironment environment = AiDatabaseRecordEnvironment.Draft,
        CancellationToken cancellationToken = default)
        => await Execute(databaseId, parsedId => _service.GetTableDdlAsync(_tenantProvider.GetTenantId(), parsedId, environment, tableName, schema, cancellationToken));

    [HttpGet("views/{viewName}/ddl")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<DdlResponse>>> GetViewDdl(
        string databaseId,
        string viewName,
        [FromQuery] string? schema = null,
        [FromQuery] AiDatabaseRecordEnvironment environment = AiDatabaseRecordEnvironment.Draft,
        CancellationToken cancellationToken = default)
        => await Execute(databaseId, parsedId => _service.GetViewDdlAsync(_tenantProvider.GetTenantId(), parsedId, environment, viewName, schema, cancellationToken));

    [HttpPost("tables/{tableName}/preview")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<PreviewDataResponse>>> PreviewTable(
        string databaseId,
        string tableName,
        [FromBody] PreviewDataRequest request,
        CancellationToken cancellationToken)
        => await Execute(databaseId, parsedId => _service.PreviewTableDataAsync(_tenantProvider.GetTenantId(), parsedId, tableName, request, cancellationToken));

    [HttpPost("views/{viewName}/preview")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<PreviewDataResponse>>> PreviewView(
        string databaseId,
        string viewName,
        [FromBody] PreviewDataRequest request,
        CancellationToken cancellationToken)
        => await Execute(databaseId, parsedId => _service.PreviewViewDataAsync(_tenantProvider.GetTenantId(), parsedId, viewName, request, cancellationToken));

    [HttpPost("tables/preview-ddl")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<DdlResponse>>> PreviewCreateTableDdl(
        string databaseId,
        [FromBody] PreviewCreateTableDdlRequest request,
        CancellationToken cancellationToken)
        => await Execute(databaseId, parsedId => _service.BuildCreateTableDdlAsync(_tenantProvider.GetTenantId(), parsedId, request, cancellationToken));

    [HttpPost("tables")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> CreateTable(
        string databaseId,
        [FromBody] CreateTableRequest request,
        CancellationToken cancellationToken)
        => await ExecuteObject(databaseId, async parsedId =>
        {
            await _service.CreateTableAsync(_tenantProvider.GetTenantId(), parsedId, request, cancellationToken);
            await WriteAuditAsync("database_structure.table.create", parsedId, request.TableName, cancellationToken);
            return new { request.TableName };
        });

    [HttpPost("tables/sql")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> CreateTableBySql(
        string databaseId,
        [FromBody] CreateTableSqlRequest request,
        CancellationToken cancellationToken)
        => await ExecuteObject(databaseId, async parsedId =>
        {
            await _service.CreateTableBySqlAsync(_tenantProvider.GetTenantId(), parsedId, request, cancellationToken);
            await WriteAuditAsync("database_structure.table.create_sql", parsedId, "sql", cancellationToken);
            return new { Success = true };
        });

    [HttpPost("views/preview")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<PreviewDataResponse>>> PreviewViewSql(
        string databaseId,
        [FromBody] PreviewViewSqlRequest request,
        CancellationToken cancellationToken)
        => await Execute(databaseId, parsedId => _service.PreviewViewSqlAsync(_tenantProvider.GetTenantId(), parsedId, request, cancellationToken));

    [HttpPost("views")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> CreateView(
        string databaseId,
        [FromBody] CreateViewRequest request,
        CancellationToken cancellationToken)
        => await ExecuteObject(databaseId, async parsedId =>
        {
            await _service.CreateViewAsync(_tenantProvider.GetTenantId(), parsedId, request, cancellationToken);
            await WriteAuditAsync("database_structure.view.create", parsedId, request.ViewName, cancellationToken);
            return new { request.ViewName };
        });

    [HttpDelete("tables/{tableName}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> DropTable(
        string databaseId,
        string tableName,
        [FromBody] DropDatabaseObjectRequest request,
        CancellationToken cancellationToken)
        => await ExecuteObject(databaseId, async parsedId =>
        {
            await _service.DropTableAsync(_tenantProvider.GetTenantId(), parsedId, tableName, request, cancellationToken);
            await WriteAuditAsync("database_structure.table.drop", parsedId, tableName, cancellationToken);
            return new { Name = tableName };
        });

    [HttpDelete("views/{viewName}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesSchemaWrite)]
    public async Task<ActionResult<ApiResponse<object>>> DropView(
        string databaseId,
        string viewName,
        [FromBody] DropDatabaseObjectRequest request,
        CancellationToken cancellationToken)
        => await ExecuteObject(databaseId, async parsedId =>
        {
            await _service.DropViewAsync(_tenantProvider.GetTenantId(), parsedId, viewName, request, cancellationToken);
            await WriteAuditAsync("database_structure.view.drop", parsedId, viewName, cancellationToken);
            return new { Name = viewName };
        });

    [HttpGet("/api/v1/database-resources/drivers/{driverCode}/data-types")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public ActionResult<ApiResponse<IReadOnlyList<object>>> GetSupportedDataTypes(string driverCode)
    {
        var dialect = _dialects.Resolve(driverCode);
        var types = dialect.DriverCode switch
        {
            "SQLite" => new[] { "INTEGER", "TEXT", "REAL", "NUMERIC", "BLOB" },
            "MySql" => new[] { "BIGINT", "INT", "VARCHAR", "TEXT", "DATETIME", "TIMESTAMP", "DECIMAL", "TINYINT", "JSON" },
            "PostgreSQL" => new[] { "BIGINT", "INTEGER", "VARCHAR", "TEXT", "TIMESTAMP", "NUMERIC", "BOOLEAN", "JSONB", "UUID" },
            "SqlServer" => new[] { "BIGINT", "INT", "NVARCHAR", "VARCHAR", "TEXT", "DATETIME2", "DECIMAL", "BIT", "UNIQUEIDENTIFIER" },
            "Oracle" or "Dm" => new[] { "NUMBER", "VARCHAR2", "NVARCHAR2", "CLOB", "DATE", "TIMESTAMP" },
            "Kdbndp" => new[] { "BIGINT", "INTEGER", "VARCHAR", "TEXT", "TIMESTAMP", "NUMERIC", "BOOLEAN", "JSONB", "UUID" },
            _ => new[] { "BIGINT", "INTEGER", "VARCHAR", "TEXT", "TIMESTAMP", "NUMERIC", "BOOLEAN" }
        };
        var result = types.Select(type => (object)new { value = type, label = type }).ToList();
        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(result, HttpContext.TraceIdentifier));
    }

    private async Task WriteAuditAsync(string action, long databaseId, string targetName, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        await _auditWriter.WriteAsync(
            new AuditRecord(
                tenantId,
                user.UserId.ToString(),
                action,
                "success",
                $"db:{databaseId}/object:{targetName}",
                null,
                null),
            cancellationToken);
    }

    private async Task<ActionResult<ApiResponse<T>>> Execute<T>(string databaseId, Func<long, Task<T>> action)
    {
        if (!TryParseDatabaseId(databaseId, out var parsedId, out var error))
        {
            return BadRequest(ApiResponse<T>.Fail(ErrorCodes.ValidationError, error, HttpContext.TraceIdentifier));
        }

        try
        {
            var result = await action(parsedId);
            return Ok(ApiResponse<T>.Ok(result, HttpContext.TraceIdentifier));
        }
        catch (SqlSafetyException ex)
        {
            return BadRequest(ApiResponse<T>.Fail(ErrorCodes.ValidationError, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> ExecuteObject(string databaseId, Func<long, Task<object>> action)
    {
        if (!TryParseDatabaseId(databaseId, out var parsedId, out var error))
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, error, HttpContext.TraceIdentifier));
        }

        try
        {
            var result = await action(parsedId);
            return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
        }
        catch (SqlSafetyException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ValidationError, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    private static bool TryParseDatabaseId(string databaseId, out long parsedId, out string error)
    {
        if (long.TryParse(databaseId, out parsedId) && parsedId > 0)
        {
            error = string.Empty;
            return true;
        }

        error = "databaseId must be a positive integer string.";
        return false;
    }
}
