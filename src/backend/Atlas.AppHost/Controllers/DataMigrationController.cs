using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 系统初始化与迁移控制台 - ORM 跨库迁移端点（M6）。
///
/// 数据迁移端点对齐 docs/contracts.md 12.6：
///  POST /test-connection / /jobs / /jobs/{id}/precheck|start|validate|cutover|rollback|retry
///  GET  /jobs/{id}|/jobs/{id}/progress|report|logs
/// </summary>
[ApiController]
[Route("api/v1/setup-console/migration")]
[AllowAnonymous]
public sealed class DataMigrationController : ControllerBase
{
    private readonly IDataMigrationOrmService _service;
    private readonly ISetupRecoveryKeyService _authService;

    public DataMigrationController(IDataMigrationOrmService service, ISetupRecoveryKeyService authService)
    {
        _service = service;
        _authService = authService;
    }

    [HttpPost("test-connection")]
    public async Task<ActionResult<ApiResponse<MigrationTestConnectionResponse>>> TestConnection(
        [FromBody] MigrationTestConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsMigrationAuthorizedAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<MigrationTestConnectionResponse>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var result = await _service.TestConnectionAsync(request.Connection, cancellationToken);
        return Ok(ApiResponse<MigrationTestConnectionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("jobs")]
    public async Task<ActionResult<ApiResponse<DataMigrationJobDto>>> CreateJob(
        [FromBody] DataMigrationJobCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsMigrationAuthorizedAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<DataMigrationJobDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        try
        {
            var job = await _service.CreateJobAsync(request, cancellationToken);
            return Ok(ApiResponse<DataMigrationJobDto>.Ok(job, HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<DataMigrationJobDto>.Fail(
                "MIGRATION_FINGERPRINT_DUPLICATED", ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("jobs/{jobId}/precheck")]
    public Task<ActionResult<ApiResponse<DataMigrationPrecheckResultDto>>> Precheck(string jobId, CancellationToken cancellationToken)
        => RunGuarded<DataMigrationPrecheckResultDto>(() => _service.PrecheckJobAsync(jobId, cancellationToken), cancellationToken);

    [HttpPost("jobs/{jobId}/start")]
    public Task<ActionResult<ApiResponse<DataMigrationJobDto>>> Start(string jobId, CancellationToken cancellationToken)
        => RunGuarded<DataMigrationJobDto>(() => _service.StartJobAsync(jobId, cancellationToken), cancellationToken);

    [HttpPost("jobs/{jobId}/cancel")]
    public Task<ActionResult<ApiResponse<DataMigrationJobDto>>> Cancel(string jobId, CancellationToken cancellationToken)
        => RunGuarded<DataMigrationJobDto>(() => _service.CancelJobAsync(jobId, cancellationToken), cancellationToken);

    [HttpGet("jobs/{jobId}")]
    public Task<ActionResult<ApiResponse<DataMigrationJobDto>>> GetJob(string jobId, CancellationToken cancellationToken)
        => RunGuarded<DataMigrationJobDto>(() => _service.GetJobAsync(jobId, cancellationToken), cancellationToken);

    [HttpGet("jobs/{jobId}/progress")]
    public async Task<ActionResult<ApiResponse<DataMigrationProgressDto>>> GetProgress(string jobId, CancellationToken cancellationToken)
    {
        if (!await IsMigrationAuthorizedAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<DataMigrationProgressDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var progress = await _service.GetProgressAsync(jobId, cancellationToken);
        return Ok(ApiResponse<DataMigrationProgressDto>.Ok(progress, HttpContext.TraceIdentifier));
    }

    [HttpPost("jobs/{jobId}/validate")]
    public Task<ActionResult<ApiResponse<DataMigrationReportDto>>> Validate(string jobId, CancellationToken cancellationToken)
        => RunGuarded<DataMigrationReportDto>(() => _service.ValidateJobAsync(jobId, cancellationToken), cancellationToken);

    [HttpPost("jobs/{jobId}/cutover")]
    public Task<ActionResult<ApiResponse<DataMigrationJobDto>>> Cutover(
        string jobId,
        [FromBody] DataMigrationCutoverRequest request,
        CancellationToken cancellationToken)
        => RunGuarded<DataMigrationJobDto>(() => _service.CutoverJobAsync(jobId, request, cancellationToken), cancellationToken);

    [HttpPost("jobs/{jobId}/rollback")]
    public async Task<ActionResult<ApiResponse<DataMigrationJobDto>>> Rollback(string jobId, CancellationToken cancellationToken)
    {
        if (!await IsMigrationAuthorizedAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<DataMigrationJobDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }

        return StatusCode(
            501,
            ApiResponse<DataMigrationJobDto>.Fail(
                "MIGRATION_ROLLBACK_NOT_IMPLEMENTED",
                "Data rollback is not implemented. Cancel before cutover or retry from checkpoint instead.",
                HttpContext.TraceIdentifier));
    }

    [HttpPost("jobs/{jobId}/retry")]
    public Task<ActionResult<ApiResponse<DataMigrationJobDto>>> Retry(string jobId, CancellationToken cancellationToken)
        => RunGuarded<DataMigrationJobDto>(() => _service.RetryJobAsync(jobId, cancellationToken), cancellationToken);

    [HttpGet("jobs/{jobId}/report")]
    public async Task<ActionResult<ApiResponse<DataMigrationReportDto?>>> GetReport(string jobId, CancellationToken cancellationToken)
    {
        if (!await IsMigrationAuthorizedAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<DataMigrationReportDto?>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var report = await _service.GetReportAsync(jobId, cancellationToken);
        if (report is null)
        {
            return NotFound(ApiResponse<DataMigrationReportDto?>.Fail(
                "NOT_FOUND", "report not generated yet", HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<DataMigrationReportDto?>.Ok(report, HttpContext.TraceIdentifier));
    }

    [HttpGet("jobs/{jobId}/logs")]
    public async Task<ActionResult<ApiResponse<DataMigrationLogPagedResponse>>> GetLogs(
        string jobId,
        [FromQuery] string? level,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMigrationAuthorizedAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<DataMigrationLogPagedResponse>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var logs = await _service.GetLogsAsync(jobId, level, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<DataMigrationLogPagedResponse>.Ok(logs, HttpContext.TraceIdentifier));
    }

    private async Task<ActionResult<ApiResponse<T>>> RunGuarded<T>(Func<Task<T>> executor, CancellationToken cancellationToken)
    {
        if (!await IsMigrationAuthorizedAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<T>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        try
        {
            var result = await executor();
            return Ok(ApiResponse<T>.Ok(result, HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<T>.Fail("VALIDATION_ERROR", ex.Message, HttpContext.TraceIdentifier));
        }
    }

    private async Task<bool> IsMigrationAuthorizedAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (jwtResult.Succeeded && jwtResult.Principal?.Identity?.IsAuthenticated == true)
        {
            HttpContext.User = jwtResult.Principal;
            return true;
        }

        if (!Request.Headers.TryGetValue(SetupConsoleAuthController.ConsoleTokenHeaderName, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }
        return await _authService.ValidateAsync(token.ToString(), cancellationToken);
    }
}
