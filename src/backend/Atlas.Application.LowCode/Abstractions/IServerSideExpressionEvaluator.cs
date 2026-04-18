using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 服务端表达式预求值器（P4-2 PLAN §M02 后端）。
///
/// 用途：
///  1) 设计态 validate 端点（POST /api/v1/lowcode/apps/{id}/validate）做"绑定可解析性"预检：
///     在用户保存草稿前预先求值所有 BindingSchema，让前端立刻显示语法/作用域错误。
///  2) 运行时 dispatch 路径：当 dispatch 收到含 expression sourceType 的 binding 时，
///     在服务端再次求值（与前端 jsonata 双层校验对齐）。
///
/// 与前端 @atlas/lowcode-expression 协议一致：
///  - 7 种作用域根 page/app/system/component/event/workflow.outputs/chatflow.outputs
///  - 跨作用域写入由 ValidateAsync 拒绝；读侧不拒绝
///  - 失败时返回 ExpressionEvalError 而非抛异常（让上层统一收集错误列表）
///
/// 默认实现 <c>NoopServerSideExpressionEvaluator</c> 仅做基础结构校验（非空 / 非禁字符），不真实求值；
/// 生产环境通过 services.Replace 注入 <c>JsonataServerSideExpressionEvaluator</c>（jsonata.NET 端口）替换。
/// </summary>
public interface IServerSideExpressionEvaluator
{
    /// <summary>
    /// 校验表达式语法 + 作用域合法性（不真实求值，仅 lint）。
    /// 用于设计态 validate 端点：批量校验 N 个表达式时单次调用聚合错误，避免循环 DB / 循环求值。
    /// </summary>
    Task<ExpressionLintReport> LintAsync(
        TenantId tenantId,
        IReadOnlyList<ExpressionLintRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>
    /// 真实求值表达式：返回结果 JSON 序列化字符串，或 ExpressionEvalError。
    /// AST 缓存由实现自决（默认 LRU 1000）；缓存键 = expression。
    /// 失败时不抛异常，而是返回 Error 字段。
    /// </summary>
    Task<ExpressionEvalResult> EvaluateAsync(
        TenantId tenantId,
        string expression,
        IReadOnlyDictionary<string, object?> scope,
        CancellationToken cancellationToken);
}

public sealed record ExpressionLintRequest(
    /// <summary>调用方提供的引用 id（如 binding 路径），用于错误回填定位。</summary>
    string Reference,
    string Expression,
    /// <summary>调用方所在的写作用域（用于跨作用域写校验）。空表示只读上下文。</summary>
    string? WritingScope = null);

public sealed record ExpressionLintError(
    string Reference,
    /// <summary>EXPR_SYNTAX / EXPR_SCOPE_VIOLATION / EXPR_UNKNOWN_VARIABLE。</summary>
    string Code,
    string Message,
    int? Line = null,
    int? Column = null);

public sealed record ExpressionLintReport(
    int TotalChecked,
    int ErrorCount,
    IReadOnlyList<ExpressionLintError> Errors);

public sealed record ExpressionEvalResult(
    bool Success,
    /// <summary>JSON 字符串（任意 JSON value）。</summary>
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// 表达式审计日志记录器（P4-2 PLAN §M02 S02-2）。
///
/// dispatch 运行时表达式失败 → mask 后入 <c>LowCodeExpressionAuditLog</c>，
/// 与 ISensitiveMaskingService 联动避免日志泄密。
/// 当前接口稳定，默认 NoopAuditor 不入库（避免引入新表）；接入真实存储时通过
/// services.Replace 注入。
/// </summary>
public interface IExpressionAuditor
{
    Task RecordFailureAsync(
        TenantId tenantId,
        string expression,
        string scopeJson,
        string errorMessage,
        CancellationToken cancellationToken);
}
