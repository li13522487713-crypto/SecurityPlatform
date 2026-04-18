using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// Chatflow 运行时控制器（M11 S11-1，**runtime 前缀** /api/runtime/chatflows）。
/// </summary>
[ApiController]
[Route("api/runtime/chatflows")]
[Authorize]
public sealed class RuntimeChatflowsController : ControllerBase
{
    private readonly IRuntimeChatflowService _chatflow;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeChatflowsController(IRuntimeChatflowService chatflow, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _chatflow = chatflow;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("{id}:invoke")]
    [Produces("text/event-stream")]
    public async Task Invoke(string id, [FromBody] RuntimeChatflowInvokeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var frame in _chatflow.StreamSseAsync(tenantId, user.UserId, request with { ChatflowId = id }, cancellationToken))
        {
            await Response.WriteAsync(frame, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("sessions/{sessionId}:pause")]
    public async Task<ActionResult<ApiResponse<object>>> Pause(string sessionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _chatflow.PauseAsync(tenantId, user.UserId, sessionId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("sessions/{sessionId}:resume")]
    [Produces("text/event-stream")]
    public async Task Resume(string sessionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        await foreach (var frame in _chatflow.ResumeSseAsync(tenantId, user.UserId, sessionId, cancellationToken))
        {
            await Response.WriteAsync(frame, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("sessions/{sessionId}:inject")]
    public async Task<ActionResult<ApiResponse<object>>> Inject(string sessionId, [FromBody] RuntimeChatflowInjectRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _chatflow.InjectAsync(tenantId, user.UserId, sessionId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

[ApiController]
[Route("api/runtime/sessions")]
[Authorize]
public sealed class RuntimeSessionsController : ControllerBase
{
    private readonly IRuntimeSessionService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeSessionsController(IRuntimeSessionService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RuntimeSessionInfo>>>> List(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var list = await _service.ListAsync(tenantId, user.UserId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RuntimeSessionInfo>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] RuntimeSessionCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _service.CreateAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/clear")]
    public async Task<ActionResult<ApiResponse<object>>> Clear(string id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.ClearAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/pin")]
    public async Task<ActionResult<ApiResponse<object>>> Pin(string id, [FromBody] RuntimeSessionPinRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.PinAsync(tenantId, user.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/archive")]
    public async Task<ActionResult<ApiResponse<object>>> Archive(string id, [FromBody] RuntimeSessionArchiveRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.ArchiveAsync(tenantId, user.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

[ApiController]
[Route("api/runtime/message-log")]
[Authorize]
public sealed class RuntimeMessageLogController : ControllerBase
{
    private readonly IRuntimeMessageLogService _service;
    private readonly ITenantProvider _tenantProvider;

    public RuntimeMessageLogController(IRuntimeMessageLogService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RuntimeMessageLogEntryDto>>>> Query([FromQuery] string? sessionId, [FromQuery] string? workflowId, [FromQuery] string? agentId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int? pageIndex, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var query = new RuntimeMessageLogQuery(sessionId, workflowId, agentId, from, to, pageIndex, pageSize);
        var list = await _service.QueryAsync(tenantId, query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RuntimeMessageLogEntryDto>>.Ok(list, HttpContext.TraceIdentifier));
    }
}
