using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowMetricsActionExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Counter_Evaluates_Numeric_Value_And_Logs()
    {
        var context = Context("counter", new
        {
            metricName = "orders.total",
            valueExpression = new { raw = "2 + 3" },
            tags = new[] { "env:test" }
        });

        var result = await new MetricsActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Equal("orders.total=5", result.OutputPreview);
        Assert.Single(result.Logs);
    }

    [Fact]
    public async Task IncrementCounter_Uses_Implicit_Value_One()
    {
        var context = Context("incrementCounter", new
        {
            metricName = "orders.increment"
        });

        var result = await new MetricsActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Equal("orders.increment=1", result.OutputPreview);
    }

    [Fact]
    public async Task Gauge_Fails_For_NonNumeric_Expression()
    {
        var context = Context("gauge", new
        {
            metricName = "orders.gauge",
            valueExpression = new { raw = "'bad'" }
        });

        var result = await new MetricsActionExecutor().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
    }

    private static MicroflowActionExecutionContext Context(string actionKind, object config)
    {
        var plan = new MicroflowExecutionPlan { Id = "metrics-test", SchemaId = "metrics-test" };
        var runtime = RuntimeExecutionContext.Create(
            "run-metrics-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1" },
            DateTimeOffset.UtcNow);

        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "metrics-node", ActionId = "metrics-action" },
            ActionConfig = JsonSerializer.SerializeToElement(config, JsonOptions),
            ActionKind = actionKind,
            ObjectId = "metrics-node",
            ActionId = "metrics-action",
            VariableStore = runtime.VariableStore,
            ExpressionEvaluator = new MicroflowExpressionEvaluator(),
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            Options = new MicroflowActionExecutionOptions { Mode = MicroflowRuntimeExecutionMode.TestRun }
        };
    }
}
