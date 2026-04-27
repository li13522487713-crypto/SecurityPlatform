using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.AppHost.Microflows.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[ApiController]
[ServiceFilter(typeof(MicroflowApiExceptionFilter))]
public abstract class MicroflowApiControllerBase : ControllerBase
{
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    protected MicroflowApiControllerBase(IMicroflowRequestContextAccessor requestContextAccessor)
    {
        _requestContextAccessor = requestContextAccessor;
    }

    protected string TraceId
    {
        get
        {
            var traceId = _requestContextAccessor.Current.TraceId;
            Response.Headers["X-Trace-Id"] = traceId;
            return traceId;
        }
    }

    protected ActionResult<MicroflowApiResponse<T>> MicroflowOk<T>(T data)
    {
        return Ok(MicroflowApiResponse<T>.Ok(data, TraceId));
    }

    protected ActionResult<MicroflowApiResponse<T>> MicroflowError<T>(MicroflowApiError error, int httpStatus)
    {
        return StatusCode(httpStatus, MicroflowApiResponse<T>.Fail(error with { HttpStatus = httpStatus }, TraceId));
    }
}
