using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/database-center")]
[Authorize]
public sealed class DatabaseManagementController : ControllerBase
{
    private readonly IDatabaseManagementService _service;
    private readonly ISqlSafetyValidator _sqlSafetyValidator;
    private readonly AiDatabasePhysicalInstanceRepository _instanceRepository;
    private readonly IAiDatabaseSecretProtector _secretProtector;
    private readonly IDatabaseDialectRegistry _dialects;
    private readonly ITenantProvider _tenantProvider;

    public DatabaseManagementController(
        IDatabaseManagementService service,
        ISqlSafetyValidator sqlSafetyValidator,
        AiDatabasePhysicalInstanceRepository instanceRepository,
        IAiDatabaseSecretProtector secretProtector,
        IDatabaseDialectRegistry dialects,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _sqlSafetyValidator = sqlSafetyValidator;
        _instanceRepository = instanceRepository;
        _secretProtector = secretProtector;
        _dialects = dialects;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("sources")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DatabaseCenterSourceDto>>>> ListSources(
        [FromQuery] string? keyword = null,
        [FromQuery] string? workspaceId = null,
        [FromQuery] string? driver = null,
        [FromQuery] string? driverCode = null,
        [FromQuery] string? status = null,
        [FromQuery] AiDatabaseRecordEnvironment? environment = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.ListSourcesAsync(_tenantProvider.GetTenantId(), keyword, workspaceId, driver ?? driverCode, status, environment, cancellationToken);
        return Ok(ApiResponse<PagedResult<DatabaseCenterSourceDto>>.Ok(new PagedResult<DatabaseCenterSourceDto>(result, result.Count, 1, result.Count == 0 ? 20 : result.Count), HttpContext.TraceIdentifier));
    }

    [HttpGet("sources/{sourceId}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<DatabaseCenterInstanceSummaryDto>>> GetSource(
        string sourceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetInstanceSummaryAsync(_tenantProvider.GetTenantId(), sourceId, cancellationToken);
        return Ok(ApiResponse<DatabaseCenterInstanceSummaryDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("sources/{sourceId}/schemas")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseCenterSchemaDto>>>> ListSchemas(
        string sourceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.ListSchemasAsync(_tenantProvider.GetTenantId(), sourceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DatabaseCenterSchemaDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("sources/{sourceId}/instance-summary")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<DatabaseCenterInstanceSummaryDto>>> GetInstanceSummary(
        string sourceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetInstanceSummaryAsync(_tenantProvider.GetTenantId(), sourceId, cancellationToken);
        return Ok(ApiResponse<DatabaseCenterInstanceSummaryDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("sources/{sourceId}/test")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<AiDatabaseConnectionTestResult>>> TestSource(
        string sourceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.TestSourceAsync(_tenantProvider.GetTenantId(), sourceId, cancellationToken);
        return Ok(ApiResponse<AiDatabaseConnectionTestResult>.Ok(result with { TraceId = HttpContext.TraceIdentifier }, HttpContext.TraceIdentifier));
    }

    [HttpGet("sources/{sourceId}/connection-logs")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatabaseCenterConnectionLogDto>>>> ListConnectionLogs(
        string sourceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.ListConnectionLogsAsync(_tenantProvider.GetTenantId(), sourceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DatabaseCenterConnectionLogDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("sql/execute")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<object>>> ExecuteSql(
        [FromBody] DatabaseCenterSqlRequest request,
        CancellationToken cancellationToken)
    {
        _sqlSafetyValidator.ValidateSqlEditorExecute(request.Sql);
        var result = IsSelectSql(request.Sql)
            ? await QuerySqlAsync(request, cancellationToken)
            : await ExecuteMutationSqlAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("sql/preview")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<object>>> PreviewSql(
        [FromBody] DatabaseCenterSqlRequest request,
        CancellationToken cancellationToken)
    {
        _sqlSafetyValidator.ValidateSelectOnly(request.Sql);
        var result = await QuerySqlAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    private async Task<object> QuerySqlAsync(DatabaseCenterSqlRequest request, CancellationToken cancellationToken)
    {
        var instance = await ResolveInstanceAsync(request.SourceId, cancellationToken);
        var connection = _secretProtector.Decrypt(instance.EncryptedConnection);
        using var client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connection,
            DbType = DataSourceDriverRegistry.ResolveDbType(instance.DriverCode),
            IsAutoCloseConnection = true
        });
        client.Ado.CommandTimeOut = 30;
        var sql = request.Sql.Trim().TrimEnd(';');
        var limit = Math.Clamp(request.Limit ?? 100, 1, 200);
        sql = _dialects.Resolve(instance.DriverCode).BuildSelectPreviewSql(sql, limit);

        var started = DateTime.UtcNow;
        var table = await client.Ado.GetDataTableAsync(sql);
        var elapsed = (long)(DateTime.UtcNow - started).TotalMilliseconds;
        var columns = table.Columns.Cast<System.Data.DataColumn>().Select(x => new { name = x.ColumnName, dataType = x.DataType.Name }).ToList();
        var rows = table.Rows.Cast<System.Data.DataRow>().Select(row =>
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Data.DataColumn column in table.Columns)
            {
                item[column.ColumnName] = NormalizePreviewValue(row[column]);
            }

            return item;
        }).ToList();
        return new { columns, rows, affectedRows = (int?)null, elapsedMs = elapsed, truncated = table.Rows.Count >= limit };
    }

    private async Task<object> ExecuteMutationSqlAsync(DatabaseCenterSqlRequest request, CancellationToken cancellationToken)
    {
        var instance = await ResolveInstanceAsync(request.SourceId, cancellationToken);
        if (instance.Environment == AiDatabaseRecordEnvironment.Online)
        {
            throw new BusinessException("Online 实例只读，禁止执行写入 SQL。", ErrorCodes.ValidationError);
        }

        var connection = _secretProtector.Decrypt(instance.EncryptedConnection);
        using var client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connection,
            DbType = DataSourceDriverRegistry.ResolveDbType(instance.DriverCode),
            IsAutoCloseConnection = true
        });
        client.Ado.CommandTimeOut = 30;
        var sql = request.Sql.Trim().TrimEnd(';');
        var started = DateTime.UtcNow;
        var affectedRows = await client.Ado.ExecuteCommandAsync(sql);
        var elapsed = (long)(DateTime.UtcNow - started).TotalMilliseconds;
        return new
        {
            columns = Array.Empty<object>(),
            rows = Array.Empty<object>(),
            affectedRows,
            elapsedMs = elapsed,
            truncated = false
        };
    }

    private static bool IsSelectSql(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Atlas.Domain.AiPlatform.Entities.AiDatabasePhysicalInstance> ResolveInstanceAsync(string sourceId, CancellationToken cancellationToken)
    {
        var raw = sourceId.StartsWith("ai:", StringComparison.OrdinalIgnoreCase) ? sourceId[3..] : sourceId;
        if (!long.TryParse(raw, out var id) || id <= 0)
        {
            throw new Atlas.Core.Exceptions.BusinessException("sourceId 必须是有效字符串 ID。", ErrorCodes.ValidationError);
        }

        return await _instanceRepository.FindByIdAsync(_tenantProvider.GetTenantId(), id, cancellationToken)
            ?? throw new Atlas.Core.Exceptions.BusinessException("数据源不存在。", ErrorCodes.NotFound);
    }

    private static object? NormalizePreviewValue(object value)
    {
        if (value == DBNull.Value)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Convert.ToBase64String(bytes.Length > 512 ? bytes[..512] : bytes),
            string text when text.Length > 2000 => text[..2000],
            DateTime dateTime => dateTime.ToString("O"),
            _ => value
        };
    }
}

public sealed record DatabaseCenterSqlRequest(
    string SourceId,
    string Sql,
    string? Schema = null,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft,
    int? Limit = 100);
