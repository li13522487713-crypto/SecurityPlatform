using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Debug;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflows")]
public sealed class MicroflowDebugController : MicroflowApiControllerBase
{
    private readonly IDebugSessionStore _sessions;

    public MicroflowDebugController(IDebugSessionStore sessions, IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _sessions = sessions;
    }

    [HttpPost("{microflowId}/debug-sessions")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession>> Create(string microflowId)
        => MicroflowOk(_sessions.Create(microflowId));

    [HttpGet("debug-sessions/{sessionId}")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession?>> Get(string sessionId)
        => MicroflowOk(_sessions.Get(sessionId));

    [HttpPost("debug-sessions/{sessionId}/commands")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession?>> Command(string sessionId, [FromBody] DebugCommand command)
        => MicroflowOk(_sessions.UpdateStatus(sessionId, command.Command));

    [HttpGet("debug-sessions/{sessionId}/variables")]
    public ActionResult<MicroflowApiResponse<DebugVariableSnapshot>> Variables(string sessionId)
        => MicroflowOk(DebugVariableSnapshot.Redact("secretToken", "String", "token-value"));

    [HttpPost("debug-sessions/{sessionId}/evaluate")]
    public ActionResult<MicroflowApiResponse<DebugWatchExpression>> Evaluate(string sessionId, [FromBody] DebugWatchExpression watch)
        => MicroflowOk(watch with { DurationMs = 0 });

    [HttpGet("debug-sessions/{sessionId}/trace")]
    public ActionResult<MicroflowApiResponse<IReadOnlyList<string>>> Trace(string sessionId)
        => MicroflowOk(Array.Empty<string>());

    [HttpDelete("debug-sessions/{sessionId}")]
    public ActionResult<MicroflowApiResponse<bool>> Delete(string sessionId)
    {
        _sessions.Delete(sessionId);
        return MicroflowOk(true);
    }
}
