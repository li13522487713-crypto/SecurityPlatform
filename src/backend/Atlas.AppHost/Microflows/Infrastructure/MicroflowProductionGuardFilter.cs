using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Atlas.AppHost.Microflows.Infrastructure;

public sealed class MicroflowProductionGuardFilter : IAsyncAuthorizationFilter
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowProductionGuardFilter(
        IHostEnvironment environment,
        IConfiguration configuration,
        IMicroflowRequestContextAccessor requestContextAccessor)
    {
        _environment = environment;
        _configuration = configuration;
        _requestContextAccessor = requestContextAccessor;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (IsAllowAnonymousEndpoint(context))
        {
            return Task.CompletedTask;
        }

        if (!IsProductionGuardEnabled())
        {
            return Task.CompletedTask;
        }

        var current = _requestContextAccessor.Current;
        if (IsAuthenticated(context.HttpContext) is false)
        {
            Reject(context, StatusCodes.Status401Unauthorized, MicroflowApiErrorCode.MicroflowUnauthorized, "生产环境微流 API 需要认证。", current.TraceId);
            return Task.CompletedTask;
        }

        if (_configuration.GetValue("Microflow:Security:RequireWorkspaceId", true)
            && string.IsNullOrWhiteSpace(current.WorkspaceId))
        {
            Reject(context, StatusCodes.Status403Forbidden, MicroflowApiErrorCode.MicroflowPermissionDenied, "生产环境微流 API 需要 X-Workspace-Id。", current.TraceId);
        }

        return Task.CompletedTask;
    }

    private bool IsProductionGuardEnabled()
        => !_environment.IsDevelopment()
           && _configuration.GetValue("Microflow:Security:EnableProductionGuard", true);

    private static bool IsAllowAnonymousEndpoint(AuthorizationFilterContext context)
        => context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();

    private static bool IsAuthenticated(HttpContext httpContext)
        => httpContext.User.Identity?.IsAuthenticated == true;

    private static void Reject(AuthorizationFilterContext context, int statusCode, string code, string message, string traceId)
    {
        context.HttpContext.Response.Headers["X-Trace-Id"] = traceId;
        context.Result = new ObjectResult(MicroflowApiResponse<object>.Fail(new MicroflowApiError
        {
            Code = code,
            Message = message,
            HttpStatus = statusCode,
            TraceId = traceId
        }, traceId))
        {
            StatusCode = statusCode
        };
    }
}
