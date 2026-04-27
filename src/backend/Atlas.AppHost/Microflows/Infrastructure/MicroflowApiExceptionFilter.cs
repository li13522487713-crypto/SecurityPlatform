using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Atlas.AppHost.Microflows.Infrastructure;

public sealed class MicroflowApiExceptionFilter : IAsyncExceptionFilter
{
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly ILogger<MicroflowApiExceptionFilter> _logger;

    public MicroflowApiExceptionFilter(
        IMicroflowRequestContextAccessor requestContextAccessor,
        ILogger<MicroflowApiExceptionFilter> logger)
    {
        _requestContextAccessor = requestContextAccessor;
        _logger = logger;
    }

    public Task OnExceptionAsync(ExceptionContext context)
    {
        var traceId = _requestContextAccessor.Current.TraceId;
        var mapped = MicroflowExceptionMapper.ToApiError(context.Exception, traceId);
        var response = MicroflowApiResponse<object>.Fail(mapped.Error, traceId);

        if (mapped.HttpStatus >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                context.Exception,
                "Microflow API failed with {Code} for {Method} {Path}. TraceId={TraceId}",
                mapped.Error.Code,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                context.Exception,
                "Microflow API rejected request with {Code} for {Method} {Path}. TraceId={TraceId}",
                mapped.Error.Code,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path,
                traceId);
        }

        context.HttpContext.Response.Headers["X-Trace-Id"] = traceId;
        context.Result = new ObjectResult(response)
        {
            StatusCode = mapped.HttpStatus
        };
        context.ExceptionHandled = true;

        return Task.CompletedTask;
    }
}
