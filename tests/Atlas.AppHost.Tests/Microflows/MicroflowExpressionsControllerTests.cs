using Atlas.AppHost.Microflows.Controllers;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Expressions;
using Microsoft.AspNetCore.Authorization;
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

        Assert.NotNull(controller.Parse(request).Value?.Data);
        Assert.NotNull(controller.Validate(request).Value?.Data);
        Assert.NotNull(controller.InferType(request).Value?.Data);
        var completions = Assert.NotNull(controller.Completions(request).Value?.Data);
        Assert.NotEmpty(completions.Completions);
        var formatted = Assert.NotNull(controller.Format(request).Value?.Data);
        Assert.Equal("$sample", formatted.FormattedExpression);
        Assert.NotNull(controller.Preview(request).Value?.Data);
    }

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
