using Atlas.Core.Models;
using Atlas.Core.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 应用宿主安装状态查询端点。
/// AppHost 自身不提供安装向导，仅暴露平台的 setup 状态供前端判断。
/// </summary>
[ApiController]
[Route("api/v1/setup")]
[AllowAnonymous]
public sealed class SetupStateController : ControllerBase
{
    private readonly ISetupStateProvider _setupStateProvider;

    public SetupStateController(ISetupStateProvider setupStateProvider)
    {
        _setupStateProvider = setupStateProvider;
    }

    [HttpGet("state")]
    public ActionResult<ApiResponse<AppSetupStateResponse>> GetState()
    {
        var state = _setupStateProvider.GetState();
        return Ok(ApiResponse<AppSetupStateResponse>.Ok(
            new AppSetupStateResponse(state.Status.ToString(), state.PlatformSetupCompleted),
            HttpContext.TraceIdentifier));
    }
}

public sealed record AppSetupStateResponse(string Status, bool PlatformSetupCompleted);
