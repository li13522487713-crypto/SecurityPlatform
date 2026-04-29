using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Debug;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflows")]
public sealed class MicroflowDebugController : MicroflowApiControllerBase
{
    /// <summary>单租户进程内并发调试会话上限（防护恶意耗尽内存）。</summary>
    private const int MaxConcurrentDebugSessions = 128;

    private readonly IDebugSessionStore _sessions;
    private readonly IMicroflowDebugCoordinator _debugCoordinator;

    private static readonly HashSet<string> ResumeCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "continue",
        "stepOver",
        "stepInto",
        "stepOut",
        "runToNode",
        "cancel",
        "stop"
    };

    public MicroflowDebugController(
        IDebugSessionStore sessions,
        IMicroflowDebugCoordinator debugCoordinator,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _sessions = sessions;
        _debugCoordinator = debugCoordinator;
    }

    [HttpPost("{microflowId}/debug-sessions")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession>> Create(string microflowId)
    {
        if (_sessions.SessionCount >= MaxConcurrentDebugSessions)
        {
            return MicroflowError<MicroflowDebugSession>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowDebugSessionLimitExceeded,
                    Message = "调试会话数量已达上限，请关闭其它会话后重试。",
                    HttpStatus = 429
                },
                429);
        }

        return MicroflowOk(_sessions.Create(microflowId));
    }

    [HttpGet("debug-sessions/{sessionId}")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession?>> Get(string sessionId)
        => MicroflowOk(_sessions.Get(sessionId));

    [HttpPost("debug-sessions/{sessionId}/commands")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession?>> Command(string sessionId, [FromBody] DebugCommand command)
    {
        var updated = _sessions.UpdateStatus(sessionId, command.Command);
        if (ResumeCommands.Contains(command.Command))
        {
            _debugCoordinator.ReleaseOnePause(sessionId);
        }

        return MicroflowOk(updated);
    }

    [HttpGet("debug-sessions/{sessionId}/variables")]
    public ActionResult<MicroflowApiResponse<DebugVariableSnapshot>> Variables(string sessionId)
        => MicroflowOk(DebugVariableSnapshot.Redact("secretToken", "String", "token-value"));

    [HttpPost("debug-sessions/{sessionId}/evaluate")]
    public ActionResult<MicroflowApiResponse<DebugWatchExpression>> Evaluate(string sessionId, [FromBody] DebugWatchExpression watch)
        => MicroflowOk(watch with { DurationMs = 0 });

    [HttpGet("debug-sessions/{sessionId}/trace")]
    public ActionResult<MicroflowApiResponse<IReadOnlyList<string>>> Trace(string sessionId)
        => MicroflowOk((IReadOnlyList<string>)Array.Empty<string>());

    [HttpDelete("debug-sessions/{sessionId}")]
    public ActionResult<MicroflowApiResponse<bool>> Delete(string sessionId)
    {
        _debugCoordinator.RemoveSession(sessionId);
        _sessions.Delete(sessionId);
        return MicroflowOk(true);
    }
}
