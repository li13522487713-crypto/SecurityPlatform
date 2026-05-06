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

    [HttpPost("debug-sessions/{sessionId}/suspend-policy")]
    public ActionResult<MicroflowApiResponse<DebugSuspendPolicyResult>> SetSuspendPolicy(
        string sessionId,
        [FromBody] DebugSuspendPolicyRequest request)
    {
        var session = ResolveOwnedSession(sessionId, out var error);
        if (session is null)
        {
            return ConvertError<DebugSuspendPolicyResult>(error!);
        }

        var policy = string.IsNullOrWhiteSpace(request.Policy) ? "all" : request.Policy.Trim();
        if (!string.Equals(policy, "all", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(policy, "branchOnly", StringComparison.OrdinalIgnoreCase))
        {
            return MicroflowError<DebugSuspendPolicyResult>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowValidationFailed,
                    Message = "Suspend policy 仅支持 all 或 branchOnly。",
                    HttpStatus = 422
                },
                422);
        }

        policy = string.Equals(policy, "branchOnly", StringComparison.OrdinalIgnoreCase) ? "branchOnly" : "all";
        var updated = _sessions.Upsert(session with
        {
            LastCommand = $"suspendPolicy:{policy}"
        });
        return MicroflowOk(new DebugSuspendPolicyResult
        {
            SessionId = updated.Id,
            Policy = policy
        });
    }

    [HttpGet("debug-sessions/{sessionId}/timeline")]
    public ActionResult<MicroflowApiResponse<IReadOnlyList<DebugTimelineItem>>> Timeline(
        string sessionId,
        [FromQuery] int take = 200)
    {
        var session = ResolveOwnedSession(sessionId, out var error);
        if (session is null)
        {
            return ConvertError<IReadOnlyList<DebugTimelineItem>>(error!);
        }

        var size = take <= 0 ? 200 : Math.Min(take, 1000);
        var timeline = session.Trace
            .OrderByDescending(item => item.CreatedAt)
            .Take(size)
            .Select(item => new DebugTimelineItem
            {
                Id = item.Id,
                SessionId = session.Id,
                RunId = item.RunId,
                ObjectId = item.NodeObjectId,
                FlowId = item.FlowId,
                BranchId = item.BranchId,
                Phase = item.Kind,
                OccurredAt = item.CreatedAt,
                Summary = item.Message
            })
            .ToArray();
        return MicroflowOk<IReadOnlyList<DebugTimelineItem>>(timeline);
    }

    [HttpPost("debug-sessions/{sessionId}/variables:mutate")]
    public ActionResult<MicroflowApiResponse<DebugVariableMutationResult>> MutateVariable(
        string sessionId,
        [FromBody] DebugVariableMutateRequest request)
    {
        var session = ResolveOwnedSession(sessionId, out var error);
        if (session is null)
        {
            return ConvertError<DebugVariableMutationResult>(error!);
        }

        if (session.CurrentSafePoint is null)
        {
            return MicroflowError<DebugVariableMutationResult>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowDebugSessionForbidden,
                    Message = "仅允许在暂停点修改变量。",
                    HttpStatus = 409
                },
                409);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return MicroflowError<DebugVariableMutationResult>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowValidationFailed,
                    Message = "变量名不能为空。",
                    HttpStatus = 422
                },
                422);
        }

        var variables = session.Variables.ToList();
        var index = variables.FindIndex(item => string.Equals(item.Name, request.Name, StringComparison.Ordinal));
        if (index < 0)
        {
            return MicroflowError<DebugVariableMutationResult>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowValidationFailed,
                    Message = $"变量不存在：{request.Name}",
                    HttpStatus = 404
                },
                404);
        }

        var original = variables[index];
        if (!request.AllowUnsafe && original.RedactionApplied)
        {
            return MicroflowError<DebugVariableMutationResult>(
                new MicroflowApiError
                {
                    Code = MicroflowApiErrorCode.MicroflowDebugSessionForbidden,
                    Message = "该变量被标记为脱敏，默认不允许修改。",
                    HttpStatus = 403
                },
                403);
        }

        var normalizedValuePreview = request.ValuePreview ?? request.Value?.ToString();
        variables[index] = original with
        {
            ValuePreview = normalizedValuePreview ?? original.ValuePreview,
            RawValueJson = request.RawValueJson ?? original.RawValueJson,
            RedactionApplied = false
        };
        var updated = _sessions.Upsert(session with
        {
            Variables = variables,
            Trace = session.Trace.Concat(new[]
            {
                new DebugTraceEvent
                {
                    Kind = "mutation",
                    Message = $"debug variable mutated: {request.Name}",
                    RunId = session.RunId,
                    NodeObjectId = session.CurrentNodeObjectId
                }
            }).ToArray()
        });

        return MicroflowOk(new DebugVariableMutationResult
        {
            SessionId = updated.Id,
            Name = request.Name,
            ValuePreview = variables[index].ValuePreview,
            UpdatedAt = updated.UpdatedAt,
            Mutated = true
        });
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

    public sealed record DebugSuspendPolicyRequest
    {
        public string? Policy { get; init; }
    }

    public sealed record DebugSuspendPolicyResult
    {
        public string SessionId { get; init; } = string.Empty;
        public string Policy { get; init; } = "all";
    }

    public sealed record DebugTimelineItem
    {
        public string Id { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public string? RunId { get; init; }
        public string? ObjectId { get; init; }
        public string? FlowId { get; init; }
        public string? BranchId { get; init; }
        public string? Phase { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
        public string? Summary { get; init; }
    }

    public sealed record DebugVariableMutateRequest
    {
        public string Name { get; init; } = string.Empty;
        public object? Value { get; init; }
        public string? ValuePreview { get; init; }
        public string? RawValueJson { get; init; }
        public bool AllowUnsafe { get; init; }
    }

    public sealed record DebugVariableMutationResult
    {
        public string SessionId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? ValuePreview { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public bool Mutated { get; init; }
    }
}
