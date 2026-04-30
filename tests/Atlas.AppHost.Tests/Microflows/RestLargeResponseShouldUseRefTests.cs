using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Actions.Http;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class RestLargeResponseShouldUseRefTests
{
    [Fact]
    public async Task HandleAsync_Large_Response_Uses_ValueRef()
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-rest-ref",
            SchemaId = "plan-rest-ref",
            ResourceId = "mf-rest-ref"
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-rest-ref",
            plan,
            MicroflowRuntimeExecutionMode.PreviewRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            memoryBudget: new ExecutionMemoryBudget { MaxHttpResponseBytes = 64, MaxVariableBytes = 64 });
        var context = new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "rest", ActionId = "rest-action" },
            ObjectId = "rest",
            ActionId = "rest-action",
            ActionKind = "restCall",
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                response = new
                {
                    handling = new { kind = "string", outputVariableName = "body" }
                }
            }),
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            Options = new MicroflowActionExecutionOptions { Mode = MicroflowRuntimeExecutionMode.PreviewRun }
        };
        var handler = new MicroflowRestResponseHandler();
        var result = await handler.HandleAsync(
            context,
            new MicroflowRuntimeHttpRequest { Method = "GET", Url = "https://example.com" },
            new MicroflowRuntimeHttpResponse
            {
                StatusCode = 200,
                ReasonPhrase = "OK",
                BodyText = new string('x', 2048),
                BodyPreview = "preview"
            },
            new MicroflowRuntimeHttpOptions(),
            CancellationToken.None);

        var produced = Assert.Single(result.ProducedVariables);
        Assert.Null(produced.RawValueJson);
        Assert.True(produced.IsLargeObject);
        Assert.NotNull(produced.ValueRef);
    }
}
