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
    private const int MaxWatchExpressionLength = 2048;

    private readonly IDebugSessionStore _sessions;
    private readonly IMicroflowDebugCoordinator _debugCoordinator;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

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
        _requestContextAccessor = requestContextAccessor;
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

        var current = _requestContextAccessor.Current;
        return MicroflowOk(_sessions.Create(microflowId, new MicroflowDebugSessionOwner
        {
            TenantId = current.TenantId,
            WorkspaceId = current.WorkspaceId,
            UserId = current.UserId
        }));
    }

    [HttpGet("debug-sessions/{sessionId}")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession?>> Get(string sessionId)
    {
        var session = ResolveOwnedSession(sessionId, out var error);
        return error is not null ? error : MicroflowOk(session);
    }

    [HttpPost("debug-sessions/{sessionId}/commands")]
    public ActionResult<MicroflowApiResponse<MicroflowDebugSession?>> Command(string sessionId, [FromBody] DebugCommand command)
    {
        if (ResolveOwnedSession(sessionId, out var error) is null)
        {
            return error!;
        }

        var updated = _debugCoordinator.ApplyCommand(sessionId, command);
        if (ResumeCommands.Contains(DebugCommandKind.Normalize(command.Command)))
        {
            _debugCoordinator.ReleaseOnePause(sessionId);
        }
        return MicroflowOk(updated);
    }

    [HttpGet("debug-sessions/{sessionId}/variables")]
    public ActionResult<MicroflowApiResponse<IReadOnlyList<DebugVariableSnapshot>>> Variables(string sessionId)
    {
        var session = ResolveOwnedSession(sessionId, out var error);
        if (session is null)
        {
            return ConvertError<IReadOnlyList<DebugVariableSnapshot>>(error!);
        }

        return MicroflowOk(session.Variables);
    }

    [HttpPost("debug-sessions/{sessionId}/evaluate")]
    public ActionResult<MicroflowApiResponse<DebugWatchExpression>> Evaluate(string sessionId, [FromBody] DebugWatchExpression watch)
    {
        var session = ResolveOwnedSession(sessionId, out var error);
        if (session is null)
        {
            return ConvertError<DebugWatchExpression>(error!);
        }

        if ((watch.Expression?.Length ?? 0) > MaxWatchExpressionLength)
        {
            return MicroflowError<DebugWatchExpression>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowDebugPayloadTooLarge,
                    Message = "调试表达式过长。",
                    HttpStatus = 413
                },
                413);
        }

        var started = System.Diagnostics.Stopwatch.StartNew();
        var expression = (watch.Expression ?? string.Empty).Trim();
        var variable = ResolveWatchVariable(session, expression);
        started.Stop();
        var result = variable is null
            ? watch with
            {
                Error = "Watch expression cannot be evaluated from the current paused snapshot.",
                DurationMs = (int)started.ElapsedMilliseconds
            }
            : watch with
            {
                Type = variable.Type,
                ValuePreview = variable.ValuePreview,
                Error = null,
                DurationMs = (int)started.ElapsedMilliseconds
            };
        return MicroflowOk(result);
    }

    [HttpGet("debug-sessions/{sessionId}/trace")]
    public ActionResult<MicroflowApiResponse<IReadOnlyList<DebugTraceEvent>>> Trace(string sessionId)
    {
        var session = ResolveOwnedSession(sessionId, out var error);
        return session is null ? ConvertError<IReadOnlyList<DebugTraceEvent>>(error!) : MicroflowOk(session.Trace);
    }

    [HttpDelete("debug-sessions/{sessionId}")]
    public ActionResult<MicroflowApiResponse<bool>> Delete(string sessionId)
    {
        if (ResolveOwnedSession(sessionId, out var error) is null)
        {
            return ConvertError<bool>(error!);
        }

        _debugCoordinator.RemoveSession(sessionId);
        _sessions.Delete(sessionId);
        return MicroflowOk(true);
    }

    private MicroflowDebugSession? ResolveOwnedSession(
        string sessionId,
        out ActionResult<MicroflowApiResponse<MicroflowDebugSession?>>? error)
    {
        error = null;
        var session = _sessions.Get(sessionId);
        if (session is null)
        {
            error = MicroflowError<MicroflowDebugSession?>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowDebugSessionNotFound,
                    Message = "调试会话不存在。",
                    HttpStatus = 404
                },
                404);
            return null;
        }

        var current = _requestContextAccessor.Current;
        var workspaceMismatch = !string.IsNullOrWhiteSpace(session.WorkspaceId)
            && !string.IsNullOrWhiteSpace(current.WorkspaceId)
            && !string.Equals(session.WorkspaceId, current.WorkspaceId, StringComparison.Ordinal);
        var tenantMismatch = !string.IsNullOrWhiteSpace(session.TenantId)
            && !string.IsNullOrWhiteSpace(current.TenantId)
            && !string.Equals(session.TenantId, current.TenantId, StringComparison.Ordinal);
        var userMismatch = !string.IsNullOrWhiteSpace(session.CreatedBy)
            && !string.IsNullOrWhiteSpace(current.UserId)
            && !string.Equals(session.CreatedBy, current.UserId, StringComparison.Ordinal);
        if (workspaceMismatch || tenantMismatch || userMismatch)
        {
            error = MicroflowError<MicroflowDebugSession?>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowDebugSessionForbidden,
                    Message = "无权访问该调试会话。",
                    HttpStatus = 403
                },
                403);
            return null;
        }

        return session;
    }

    private static DebugVariableSnapshot? ResolveWatchVariable(MicroflowDebugSession session, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var normalized = expression.StartsWith("$", StringComparison.Ordinal)
            ? expression[1..]
            : expression;
        var memberSeparator = normalized.IndexOf('.', StringComparison.Ordinal);
        var variableName = memberSeparator >= 0 ? normalized[..memberSeparator] : normalized;
        var variable = session.Variables.FirstOrDefault(item =>
            string.Equals(item.Name, variableName, StringComparison.Ordinal)
            || string.Equals(item.Name, $"${variableName}", StringComparison.Ordinal));
        if (variable is null || memberSeparator < 0 || string.IsNullOrWhiteSpace(variable.RawValueJson))
        {
            return variable;
        }

        var memberPath = normalized[(memberSeparator + 1)..].Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(variable.RawValueJson);
            var current = document.RootElement;
            foreach (var member in memberPath)
            {
                if (current.ValueKind != System.Text.Json.JsonValueKind.Object || !current.TryGetProperty(member, out current))
                {
                    return null;
                }
            }

            return variable with
            {
                Name = expression,
                Type = current.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => "string",
                    System.Text.Json.JsonValueKind.Number => "decimal",
                    System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => "boolean",
                    System.Text.Json.JsonValueKind.Array => "list",
                    System.Text.Json.JsonValueKind.Object => "object",
                    _ => "unknown"
                },
                ValuePreview = current.ValueKind == System.Text.Json.JsonValueKind.String
                    ? current.GetString()
                    : current.GetRawText(),
                RawValueJson = current.GetRawText()
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private ActionResult<MicroflowApiResponse<T>> ConvertError<T>(ActionResult<MicroflowApiResponse<MicroflowDebugSession?>> source)
    {
        if (source.Result is ObjectResult objectResult
            && objectResult.Value is MicroflowApiResponse<MicroflowDebugSession?> response
            && response.Error is not null)
        {
            return MicroflowError<T>(response.Error, response.Error.HttpStatus ?? 500);
        }

        return MicroflowError<T>(
            new MicroflowApiError
            {
                Code = MicroflowApiErrorCode.MicroflowUnknownError,
                Message = "调试会话校验失败。",
                HttpStatus = 500
            },
            500);
    }
}
