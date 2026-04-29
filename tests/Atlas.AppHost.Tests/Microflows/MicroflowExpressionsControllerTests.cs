using Atlas.AppHost.Microflows.Controllers;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowExpressionsControllerTests
{
    [Fact]
    public void ExpressionController_InheritsAuthorizeRequirement()
    {
        Assert.NotNull(typeof(MicroflowApiControllerBase).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public void ParseValidateInferCompletionsFormatPreview_ReturnEnvelopeData()
    {
        var controller = CreateController();
        var request = new MicroflowExpressionEditorRequest { Expression = "$sample" };

        AssertParseData(controller.Parse(request));
        AssertParseData(controller.Validate(request));
        AssertParseData(controller.InferType(request));
        var completionsEnvelope = AssertOkEnvelope(controller.Completions(request));
        Assert.NotEmpty(completionsEnvelope.Completions);
        var formatted = AssertOkEnvelope(controller.Format(request));
        Assert.Equal("$sample", formatted.FormattedExpression);
        AssertParseData(controller.Preview(request));
    }

    [Fact]
    public void Parse_WithExplicitSupportedMetadataVersion_Ok()
    {
        var controller = CreateController();
        var request = new MicroflowExpressionEditorRequest
        {
            Expression = "$sample",
            MetadataVersion = MicroflowExpressionApiMetadata.SupportedMetadataVersion
        };
        AssertParseData(controller.Parse(request));
    }

    [Fact]
    public void Parse_WithUnsupportedMetadataVersion_Returns422()
    {
        var controller = CreateController();
        var request = new MicroflowExpressionEditorRequest { Expression = "$a", MetadataVersion = "0.0.0-not-supported" };
        var actionResult = controller.Parse(request);
        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(422, objectResult.StatusCode);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowExpressionEditorResponse>>(objectResult.Value);
        Assert.Equal(MicroflowApiErrorCode.MicroflowMetadataVersionMismatch, envelope.Error?.Code);
    }

    private static MicroflowExpressionEditorResponse AssertOkEnvelope(
        ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> actionResult)
    {
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowExpressionEditorResponse>>(ok.Value);
        Assert.NotNull(envelope.Data);
        return envelope.Data!;
    }

    private static void AssertParseData(ActionResult<MicroflowApiResponse<MicroflowExpressionEditorResponse>> actionResult)
        => _ = AssertOkEnvelope(actionResult);

    private static MicroflowExpressionsController CreateController()
    {
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            TraceId = "trace-expression"
        });
        return new MicroflowExpressionsController(
            new MicroflowExpressionEditorService(new MicroflowExpressionEvaluator()),
            accessor);
    }
}
