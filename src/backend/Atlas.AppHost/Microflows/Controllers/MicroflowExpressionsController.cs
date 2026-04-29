using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Expressions;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflow-expressions")]
public sealed class MicroflowExpressionsController : MicroflowApiControllerBase
{
    private readonly MicroflowExpressionEditorService _service;

    public MicroflowExpressionsController(
        MicroflowExpressionEditorService service,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _service = service;
    }

    [HttpPost("parse")]
    public ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> Parse(
        [FromBody] MicroflowExpressionEditorRequest request)
        => MicroflowOk(_service.Parse(request));

    [HttpPost("validate")]
    public ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> Validate(
        [FromBody] MicroflowExpressionEditorRequest request)
        => MicroflowOk(_service.Validate(request));

    [HttpPost("infer-type")]
    public ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> InferType(
        [FromBody] MicroflowExpressionEditorRequest request)
        => MicroflowOk(_service.InferType(request));

    [HttpPost("completions")]
    public ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> Completions(
        [FromBody] MicroflowExpressionEditorRequest request)
        => MicroflowOk(_service.Completions(request));

    [HttpPost("format")]
    public ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> Format(
        [FromBody] MicroflowExpressionEditorRequest request)
        => MicroflowOk(_service.Format(request));

    [HttpPost("preview")]
    public ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> Preview(
        [FromBody] MicroflowExpressionEditorRequest request)
        => MicroflowOk(_service.Preview(request));
}
