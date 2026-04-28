using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 系统初始化与迁移控制台 - 主端点（M5）。
///
/// - 全部端点 [AllowAnonymous]，由 SetupConsoleAuthMiddleware（如已就绪）
///   或同级 SetupConsoleAuthController 颁发的 ConsoleToken 在请求头 `X-Setup-Console-Token` 中校验。
/// - M6 后追加数据迁移控制器 DataMigrationController。
/// </summary>
[ApiController]
[Route("api/v1/setup-console")]
[AllowAnonymous]
public sealed class SetupConsoleController : ControllerBase
{
    private readonly ISetupConsoleService _service;
    private readonly ISetupRecoveryKeyService _authService;

    public SetupConsoleController(ISetupConsoleService service, ISetupRecoveryKeyService authService)
    {
        _service = service;
        _authService = authService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<SetupConsoleOverviewDto>>> GetOverview(CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<SetupConsoleOverviewDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var overview = await _service.GetOverviewAsync(cancellationToken);
        return Ok(ApiResponse<SetupConsoleOverviewDto>.Ok(overview, HttpContext.TraceIdentifier));
    }

    [HttpGet("system/state")]
    public async Task<ActionResult<ApiResponse<SystemSetupStateDto>>> GetSystemState(CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<SystemSetupStateDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var state = await _service.GetSystemStateAsync(cancellationToken);
        return Ok(ApiResponse<SystemSetupStateDto>.Ok(state, HttpContext.TraceIdentifier));
    }

    [HttpGet("catalog/entities")]
    public async Task<ActionResult<ApiResponse<SetupConsoleCatalogSummaryDto>>> GetCatalog(
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<SetupConsoleCatalogSummaryDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var catalog = await _service.GetCatalogSummaryAsync(category, cancellationToken);
        return Ok(ApiResponse<SetupConsoleCatalogSummaryDto>.Ok(catalog, HttpContext.TraceIdentifier));
    }

    [HttpGet("catalog/entities/{category}/details")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetCatalogEntityDetails(
        string category,
        CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<string>>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var entities = await _service.GetCatalogEntitiesAsync(category, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(entities, HttpContext.TraceIdentifier));
    }

    [HttpPost("system/precheck")]
    public Task<ActionResult<ApiResponse<SetupStepResultDto>>> Precheck(
        [FromBody] SystemPrecheckRequest request,
        CancellationToken cancellationToken)
        => RunGuardedStep(() => _service.RunPrecheckAsync(request, cancellationToken), cancellationToken);

    [HttpPost("system/schema")]
    public Task<ActionResult<ApiResponse<SetupStepResultDto>>> Schema(
        [FromBody] SystemSchemaRequest request,
        CancellationToken cancellationToken)
        => RunGuardedStep(() => _service.RunSchemaAsync(request, cancellationToken), cancellationToken);

    [HttpPost("system/seed")]
    public Task<ActionResult<ApiResponse<SetupStepResultDto>>> Seed(
        [FromBody] SystemSeedRequest request,
        CancellationToken cancellationToken)
        => RunGuardedStep(() => _service.RunSeedAsync(request, cancellationToken), cancellationToken);

    [HttpPost("system/bootstrap-user")]
    public async Task<ActionResult<ApiResponse<SystemBootstrapUserResponse>>> BootstrapUser(
        [FromBody] SystemBootstrapUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<SystemBootstrapUserResponse>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var result = await _service.RunBootstrapUserAsync(request, cancellationToken);
        return Ok(ApiResponse<SystemBootstrapUserResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("system/default-workspace")]
    public Task<ActionResult<ApiResponse<SetupStepResultDto>>> DefaultWorkspace(
        [FromBody] SystemDefaultWorkspaceRequest request,
        CancellationToken cancellationToken)
        => RunGuardedStep(() => _service.RunDefaultWorkspaceAsync(request, cancellationToken), cancellationToken);

    [HttpPost("system/complete")]
    public Task<ActionResult<ApiResponse<SetupStepResultDto>>> Complete(CancellationToken cancellationToken)
        => RunGuardedStep(() => _service.RunCompleteAsync(cancellationToken), cancellationToken);

    [HttpPost("system/retry/{step}")]
    public Task<ActionResult<ApiResponse<SetupStepResultDto>>> Retry(string step, CancellationToken cancellationToken)
        => RunGuardedStep(() => _service.RetryStepAsync(step, cancellationToken), cancellationToken);

    [HttpPost("system/reopen")]
    public async Task<ActionResult<ApiResponse<SystemSetupStateDto>>> Reopen(CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<SystemSetupStateDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var state = await _service.ReopenAsync(cancellationToken);
        return Ok(ApiResponse<SystemSetupStateDto>.Ok(state, HttpContext.TraceIdentifier));
    }

    [HttpGet("workspaces")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkspaceSetupStateDto>>>> ListWorkspaces(CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<WorkspaceSetupStateDto>>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var workspaces = await _service.ListWorkspacesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkspaceSetupStateDto>>.Ok(workspaces, HttpContext.TraceIdentifier));
    }

    [HttpPost("workspaces/{workspaceId}/init")]
    public async Task<ActionResult<ApiResponse<WorkspaceSetupStateDto>>> InitializeWorkspace(
        string workspaceId,
        [FromBody] WorkspaceInitRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<WorkspaceSetupStateDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var result = await _service.InitializeWorkspaceAsync(workspaceId, request, cancellationToken);
        return Ok(ApiResponse<WorkspaceSetupStateDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("workspaces/{workspaceId}/seed-bundle")]
    public async Task<ActionResult<ApiResponse<WorkspaceSetupStateDto>>> ApplyWorkspaceSeedBundle(
        string workspaceId,
        [FromBody] WorkspaceSeedBundleRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<WorkspaceSetupStateDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var result = await _service.ApplyWorkspaceSeedBundleAsync(workspaceId, request, cancellationToken);
        return Ok(ApiResponse<WorkspaceSetupStateDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("workspaces/{workspaceId}/complete")]
    public async Task<ActionResult<ApiResponse<WorkspaceSetupStateDto>>> CompleteWorkspaceInit(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<WorkspaceSetupStateDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var result = await _service.CompleteWorkspaceInitAsync(workspaceId, cancellationToken);
        return Ok(ApiResponse<WorkspaceSetupStateDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    private async Task<ActionResult<ApiResponse<SetupStepResultDto>>> RunGuardedStep(
        Func<Task<SetupStepResultDto>> executor,
        CancellationToken cancellationToken)
    {
        if (!await IsConsoleTokenValidAsync(cancellationToken))
        {
            return Unauthorized(ApiResponse<SetupStepResultDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED", "console token missing or expired", HttpContext.TraceIdentifier));
        }
        var result = await executor();
        return Ok(ApiResponse<SetupStepResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    private async Task<bool> IsConsoleTokenValidAsync(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue(SetupConsoleAuthController.ConsoleTokenHeaderName, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }
        return await _authService.ValidateAsync(token.ToString(), cancellationToken);
    }
}
