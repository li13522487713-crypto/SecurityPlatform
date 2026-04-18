using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 服务端表达式预求值器默认实现（P4-2 PLAN §M02 后端骨架）。
///
/// 当前实现仅做"语法 lint + 作用域写约束校验"，**不真实求值**：
///  - 真实 jsonata 求值需要 jsonata.NET 端口或自实现简化求值器（工作量大且与前端 jsonata 行为对齐复杂）；
///  - 当前作为契约前置，让 P1-2 的 validate 端点 / dispatch 路径可以即时调用并接 NoopAuditor；
///  - 后续接入真实求值器时通过 services.Replace 注入 JsonataServerSideExpressionEvaluator 即可，契约稳定。
///
/// Lint 规则（与 @atlas/lowcode-expression scope-guard 对齐）：
///  1) 表达式不能含 'eval(' 'Function(' 'import(' 等 JS 危险函数（防注入）
///  2) 表达式中包含赋值 system.* / component.<id>.* / event.* / workflow.outputs.* / chatflow.outputs.* 时
///     若 WritingScope 非空 → 报 EXPR_SCOPE_VIOLATION
///  3) 表达式空 → EXPR_SYNTAX
/// </summary>
public sealed class ServerSideExpressionEvaluator : IServerSideExpressionEvaluator
{
    private static readonly string[] DangerousFunctions = { "eval(", "Function(", "import(", "require(", "process.", "global." };
    private static readonly Regex AssignmentToRoot = new(@"\b(system|component|event|workflow\.outputs|chatflow\.outputs)\b\s*\.\s*[a-zA-Z_][a-zA-Z0-9_]*\s*=", RegexOptions.Compiled);

    private readonly IExpressionAuditor _auditor;
    private readonly ILogger<ServerSideExpressionEvaluator> _logger;

    public ServerSideExpressionEvaluator(IExpressionAuditor auditor, ILogger<ServerSideExpressionEvaluator> logger)
    {
        _auditor = auditor;
        _logger = logger;
    }

    public Task<ExpressionLintReport> LintAsync(
        TenantId tenantId,
        IReadOnlyList<ExpressionLintRequest> requests,
        CancellationToken cancellationToken)
    {
        var errors = new List<ExpressionLintError>();
        foreach (var req in requests)
        {
            if (string.IsNullOrWhiteSpace(req.Expression))
            {
                errors.Add(new ExpressionLintError(req.Reference, "EXPR_SYNTAX", "表达式为空"));
                continue;
            }
            // 危险函数注入
            foreach (var bad in DangerousFunctions)
            {
                if (req.Expression.Contains(bad, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ExpressionLintError(req.Reference, "EXPR_SYNTAX",
                        $"表达式包含禁用函数：{bad}"));
                    break;
                }
            }
            // 跨作用域写校验
            if (!string.IsNullOrWhiteSpace(req.WritingScope))
            {
                var match = AssignmentToRoot.Match(req.Expression);
                if (match.Success)
                {
                    var root = match.Groups[1].Value;
                    errors.Add(new ExpressionLintError(req.Reference, "EXPR_SCOPE_VIOLATION",
                        $"在写作用域 {req.WritingScope} 内禁止写入只读作用域 {root}"));
                }
            }
        }
        return Task.FromResult(new ExpressionLintReport(requests.Count, errors.Count, errors));
    }

    public async Task<ExpressionEvalResult> EvaluateAsync(
        TenantId tenantId,
        string expression,
        IReadOnlyDictionary<string, object?> scope,
        CancellationToken cancellationToken)
    {
        // 当前默认实现为骨架，不真实求值。返回 NOT_IMPLEMENTED 让上层明确感知"未接入真实 jsonata"，
        // 比静默返回空字符串更安全。生产环境通过 services.Replace 注入真实求值器替换。
        _logger.LogDebug("ServerSideExpressionEvaluator.EvaluateAsync 当前为骨架实现：tenant={Tenant} expr.head={Head}",
            tenantId.Value, expression.Length > 64 ? expression[..64] + "…" : expression);
        await _auditor.RecordFailureAsync(tenantId, expression, JsonSerializer.Serialize(scope.Keys),
            "EVALUATOR_NOT_IMPLEMENTED", cancellationToken);
        return new ExpressionEvalResult(
            Success: false,
            ResultJson: null,
            ErrorCode: "EVALUATOR_NOT_IMPLEMENTED",
            ErrorMessage: "服务端 jsonata 求值器尚未接入；当前仅 lint 可用，请通过 services.Replace 注入真实实现。");
    }
}

/// <summary>
/// NoopExpressionAuditor：不做任何持久化（默认实现）。
/// 生产部署用 SqlSugar 落 LowCodeExpressionAuditLog 表实现。
/// </summary>
public sealed class NoopExpressionAuditor : IExpressionAuditor
{
    public Task RecordFailureAsync(TenantId tenantId, string expression, string scopeJson, string errorMessage, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
