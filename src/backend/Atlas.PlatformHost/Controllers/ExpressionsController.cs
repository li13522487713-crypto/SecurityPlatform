using Atlas.Core.Expressions;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/expressions")]
public sealed class ExpressionsController : ControllerBase
{
    private readonly IExpressionEngine _engine;

    public ExpressionsController(IExpressionEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// 静态校验表达式语法（不求值）
    /// </summary>
    [HttpPost("validate")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public ActionResult<ApiResponse<ExpressionValidateResponse>> Validate([FromBody] ExpressionValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Expression))
            return Ok(ApiResponse<ExpressionValidateResponse>.Fail("VALIDATION_ERROR", ApiResponseLocalizer.T(HttpContext, "ExpressionRequired"), HttpContext.TraceIdentifier));

        var result = _engine.Validate(request.Expression);
        var variables = _engine.GetVariables(request.Expression);

        return Ok(ApiResponse<ExpressionValidateResponse>.Ok(new ExpressionValidateResponse(
            result.IsValid,
            result.Errors,
            result.Warnings,
            variables), HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 带上下文对表达式求值（沙箱模式，仅用于调试/试运行）
    /// </summary>
    [HttpPost("evaluate")]
    [Authorize(Policy = PermissionPolicies.DebugRun)]
    public ActionResult<ApiResponse<ExpressionEvaluateResponse>> Evaluate([FromBody] ExpressionEvaluateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Expression))
            return Ok(ApiResponse<ExpressionEvaluateResponse>.Fail("VALIDATION_ERROR", ApiResponseLocalizer.T(HttpContext, "ExpressionRequired"), HttpContext.TraceIdentifier));

        var validation = _engine.Validate(request.Expression);
        if (!validation.IsValid)
            return Ok(ApiResponse<ExpressionEvaluateResponse>.Fail("VALIDATION_ERROR", string.Join("; ", validation.Errors), HttpContext.TraceIdentifier));

        var ctx = new ExpressionContext
        {
            Record = request.Record ?? new Dictionary<string, object?>(),
            User = request.User ?? new Dictionary<string, object?>(),
            Page = request.Page ?? new Dictionary<string, object?>(),
            App = request.App ?? new Dictionary<string, object?>(),
            Tenant = request.Tenant ?? new Dictionary<string, object?>(),
            Global = request.Global ?? new Dictionary<string, object?>()
        };

        try
        {
            var result = _engine.Evaluate(request.Expression, ctx);
            return Ok(ApiResponse<ExpressionEvaluateResponse>.Ok(new ExpressionEvaluateResponse(
                true,
                result?.ToString(),
                result is bool b ? b : null,
                null), HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<ExpressionEvaluateResponse>.Ok(new ExpressionEvaluateResponse(
                false,
                null,
                null,
                ex.Message), HttpContext.TraceIdentifier));
        }
    }
}

public sealed record ExpressionValidateRequest(
    string Expression);

public sealed record ExpressionValidateResponse(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Variables);

public sealed record ExpressionEvaluateRequest(
    string Expression,
    IReadOnlyDictionary<string, object?>? Record,
    IReadOnlyDictionary<string, object?>? User,
    IReadOnlyDictionary<string, object?>? Page,
    IReadOnlyDictionary<string, object?>? App = null,
    IReadOnlyDictionary<string, object?>? Tenant = null,
    IReadOnlyDictionary<string, object?>? Global = null);

public sealed record ExpressionEvaluateResponse(
    bool Success,
    string? ResultValue,
    bool? ResultBool,
    string? Error);
