using Atlas.Core.Models;
using Atlas.Core.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 应用宿主安装状态与初始化端点。
/// 暴露平台和应用两级 setup 状态，并提供应用级最小安装向导。
/// </summary>
[ApiController]
[Route("api/v1/setup")]
[AllowAnonymous]
public sealed class SetupStateController : ControllerBase
{
    private readonly ISetupStateProvider _platformSetupStateProvider;
    private readonly IAppSetupStateProvider _appSetupStateProvider;
    private readonly ILogger<SetupStateController> _logger;

    public SetupStateController(
        ISetupStateProvider platformSetupStateProvider,
        IAppSetupStateProvider appSetupStateProvider,
        ILogger<SetupStateController> logger)
    {
        _platformSetupStateProvider = platformSetupStateProvider;
        _appSetupStateProvider = appSetupStateProvider;
        _logger = logger;
    }

    /// <summary>获取平台和应用两级 setup 状态</summary>
    [HttpGet("state")]
    public ActionResult<ApiResponse<AppHostSetupStateResponse>> GetState()
    {
        var platformState = _platformSetupStateProvider.GetState();
        var appState = _appSetupStateProvider.GetState();
        return Ok(ApiResponse<AppHostSetupStateResponse>.Ok(
            new AppHostSetupStateResponse(
                platformState.Status.ToString(),
                platformState.PlatformSetupCompleted,
                appState.Status.ToString(),
                _appSetupStateProvider.IsReady),
            HttpContext.TraceIdentifier));
    }

    /// <summary>执行应用级最小初始化（确认组织主体 + 管理员）</summary>
    [HttpPost("initialize")]
    public async Task<ActionResult<ApiResponse<AppHostSetupStateResponse>>> Initialize(
        [FromBody] AppSetupInitializeRequest request,
        CancellationToken cancellationToken)
    {
        if (!_platformSetupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<AppHostSetupStateResponse>.Fail(
                "PLATFORM_NOT_READY", "Platform setup must be completed first.", HttpContext.TraceIdentifier));
        }

        if (_appSetupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<AppHostSetupStateResponse>.Fail(
                "ALREADY_CONFIGURED", "Application setup has already been completed.", HttpContext.TraceIdentifier));
        }

        try
        {
            await _appSetupStateProvider.TransitionAsync(AppSetupState.Initializing, cancellationToken: cancellationToken);

            // 应用级初始化最小范围：记录组织名称和管理员
            // 后续可扩展为实际的应用数据库 seed、应用菜单/角色初始化等
            await _appSetupStateProvider.CompleteSetupAsync(
                request.AppName,
                request.AdminUsername,
                cancellationToken);

            _logger.LogInformation("[AppSetup] 应用安装完成，AppName={AppName}", request.AppName);

            var platformState = _platformSetupStateProvider.GetState();
            var appState = _appSetupStateProvider.GetState();
            return Ok(ApiResponse<AppHostSetupStateResponse>.Ok(
                new AppHostSetupStateResponse(
                    platformState.Status.ToString(),
                    platformState.PlatformSetupCompleted,
                    appState.Status.ToString(),
                    true),
                HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AppSetup] 应用初始化失败");
            await _appSetupStateProvider.TransitionAsync(AppSetupState.Failed, ex.Message, cancellationToken);
            return StatusCode(500, ApiResponse<AppHostSetupStateResponse>.Fail(
                "APP_SETUP_FAILED", ex.Message, HttpContext.TraceIdentifier));
        }
    }
}

public sealed record AppHostSetupStateResponse(
    string PlatformStatus,
    bool PlatformSetupCompleted,
    string AppStatus,
    bool AppSetupCompleted);

public sealed record AppSetupInitializeRequest(
    string AppName,
    string AdminUsername);
