using Atlas.Core.Models;
using Atlas.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SqlSugar;
using Hangfire;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly ISqlSugarClient _db;
    private readonly FileStorageOptions _fileStorageOptions;
    private readonly IHostEnvironment _hostEnvironment;

    public HealthController(
        ISqlSugarClient db,
        IOptions<FileStorageOptions> fileStorageOptions,
        IHostEnvironment hostEnvironment)
    {
        _db = db;
        _fileStorageOptions = fileStorageOptions.Value;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<HealthStatusPayload>>> Get(CancellationToken cancellationToken = default)
    {
        var checks = new List<HealthDependencyStatus>
        {
            await CheckDatabaseAsync(cancellationToken),
            CheckHangfire(),
            CheckFileStorage()
        };

        var overallHealthy = checks.All(item => item.Healthy);
        var payload = new HealthStatusPayload(
            overallHealthy ? "Healthy" : "Degraded",
            DateTimeOffset.UtcNow,
            checks);
        var response = ApiResponse<HealthStatusPayload>.Ok(payload, HttpContext.TraceIdentifier);

        if (overallHealthy)
        {
            return Ok(response);
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private async Task<HealthDependencyStatus> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.Ado.ExecuteCommandAsync("SELECT 1;", cancellationToken);
            return new HealthDependencyStatus("Database", true, "连接正常");
        }
        catch (Exception ex)
        {
            return new HealthDependencyStatus("Database", false, $"连接异常：{ex.Message}");
        }
    }

    private static HealthDependencyStatus CheckHangfire()
    {
        try
        {
            var monitoringApi = JobStorage.Current.GetMonitoringApi();
            _ = monitoringApi.Servers();
            return new HealthDependencyStatus("Hangfire", true, "连接正常");
        }
        catch (Exception ex)
        {
            return new HealthDependencyStatus("Hangfire", false, $"连接异常：{ex.Message}");
        }
    }

    private HealthDependencyStatus CheckFileStorage()
    {
        try
        {
            var path = _fileStorageOptions.BasePath;
            var resolvedPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(_hostEnvironment.ContentRootPath, path);
            var exists = Directory.Exists(resolvedPath);
            return exists
                ? new HealthDependencyStatus("FileStorage", true, $"目录可用：{resolvedPath}")
                : new HealthDependencyStatus("FileStorage", false, $"目录不存在：{resolvedPath}");
        }
        catch (Exception ex)
        {
            return new HealthDependencyStatus("FileStorage", false, $"检测异常：{ex.Message}");
        }
    }
}

public sealed record HealthStatusPayload(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<HealthDependencyStatus> Dependencies);

public sealed record HealthDependencyStatus(
    string Name,
    bool Healthy,
    string Message);
