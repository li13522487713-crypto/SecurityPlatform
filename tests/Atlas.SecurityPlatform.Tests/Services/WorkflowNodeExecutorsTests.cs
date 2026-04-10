using System.Text.Json;
using Atlas.Core.Tenancy;
using Atlas.Core.Expressions;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Infrastructure.LogicFlow.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions.Functions;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class WorkflowNodeExecutorsTests
{
    [Fact]
    public async Task SelectorNodeExecutor_ShouldEvaluateStructuredConditions()
    {
        var executor = new SelectorNodeExecutor();
        var node = BuildNode(
            "selector_1",
            WorkflowNodeType.Selector,
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["logic"] = JsonSerializer.SerializeToElement("and"),
                ["conditions"] = JsonSerializer.SerializeToElement(new object[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["left"] = "{{risk}}",
                        ["op"] = "ge",
                        ["right"] = 80
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["left"] = "{{level}}",
                        ["op"] = "eq",
                        ["right"] = "high"
                    }
                })
            });
        var variables = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["risk"] = JsonSerializer.SerializeToElement(96),
            ["level"] = JsonSerializer.SerializeToElement("high")
        };

        var result = await executor.ExecuteAsync(CreateContext(node, variables), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Outputs.ContainsKey("selector_result"));
        Assert.True(result.Outputs.ContainsKey("selected_branch"));
    }

    [Fact]
    public async Task LoopNodeExecutor_ForEachMode_ShouldEmitCurrentItemAndIndexes()
    {
        var executor = new LoopNodeExecutor();
        var node = BuildNode(
            "loop_1",
            WorkflowNodeType.Loop,
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["mode"] = JsonSerializer.SerializeToElement("forEach"),
                ["collectionPath"] = JsonSerializer.SerializeToElement("items"),
                ["itemVariable"] = JsonSerializer.SerializeToElement("current_item"),
                ["itemIndexVariable"] = JsonSerializer.SerializeToElement("current_index"),
                ["maxIterations"] = JsonSerializer.SerializeToElement(10)
            });
        var variables = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = JsonSerializer.SerializeToElement(new[] { "alpha", "beta" })
        };

        var result = await executor.ExecuteAsync(CreateContext(node, variables), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Outputs["loop_completed"].GetBoolean());
        Assert.Equal(1, result.Outputs["loop_index"].GetInt32());
        Assert.Equal(0, result.Outputs["current_index"].GetInt32());
        Assert.Equal("alpha", result.Outputs["current_item"].GetString());
    }

    [Fact]
    public async Task LoopNodeExecutor_WhileMode_ShouldStopWhenConditionFalse()
    {
        var executor = new LoopNodeExecutor();
        var node = BuildNode(
            "loop_while",
            WorkflowNodeType.Loop,
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["mode"] = JsonSerializer.SerializeToElement("while"),
                ["condition"] = JsonSerializer.SerializeToElement("{{count}} < 3"),
                ["maxIterations"] = JsonSerializer.SerializeToElement(10)
            });

        var continueVariables = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["count"] = JsonSerializer.SerializeToElement(2)
        };
        var continueResult = await executor.ExecuteAsync(CreateContext(node, continueVariables), CancellationToken.None);
        Assert.True(continueResult.Success);
        Assert.False(continueResult.Outputs["loop_completed"].GetBoolean());

        var stopVariables = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["count"] = JsonSerializer.SerializeToElement(5)
        };
        var stopResult = await executor.ExecuteAsync(CreateContext(node, stopVariables), CancellationToken.None);
        Assert.True(stopResult.Success);
        Assert.True(stopResult.Outputs["loop_completed"].GetBoolean());
    }

    [Fact]
    public async Task BreakAndContinueExecutors_ShouldEmitControlSignals()
    {
        var breakExecutor = new BreakNodeExecutor();
        var continueExecutor = new ContinueNodeExecutor();

        var breakNode = BuildNode(
            "break_1",
            WorkflowNodeType.Break,
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["reason"] = JsonSerializer.SerializeToElement("risk>=90")
            });
        var continueNode = BuildNode("continue_1", WorkflowNodeType.Continue, new Dictionary<string, JsonElement>());

        var breakResult = await breakExecutor.ExecuteAsync(CreateContext(breakNode, new Dictionary<string, JsonElement>()), CancellationToken.None);
        var continueResult = await continueExecutor.ExecuteAsync(CreateContext(continueNode, new Dictionary<string, JsonElement>()), CancellationToken.None);

        Assert.True(breakResult.Success);
        Assert.True(continueResult.Success);
        Assert.True(breakResult.Outputs["loop_break"].GetBoolean());
        Assert.True(continueResult.Outputs["loop_continue"].GetBoolean());
    }

    [Fact]
    public async Task BatchNodeExecutor_ShouldNormalizeBatchConfig()
    {
        var executor = new BatchNodeExecutor();
        var node = BuildNode(
            "batch_1",
            WorkflowNodeType.Batch,
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["concurrentSize"] = JsonSerializer.SerializeToElement(999),
                ["batchSize"] = JsonSerializer.SerializeToElement(0),
                ["inputArrayPath"] = JsonSerializer.SerializeToElement("items"),
                ["outputKey"] = JsonSerializer.SerializeToElement("result_set")
            });

        var result = await executor.ExecuteAsync(CreateContext(node, new Dictionary<string, JsonElement>()), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(64, result.Outputs["batch_concurrent_size"].GetInt32());
        Assert.Equal(1, result.Outputs["batch_size"].GetInt32());
        Assert.Equal("items", result.Outputs["batch_input_array_path"].GetString());
        Assert.Equal("result_set", result.Outputs["batch_output_key"].GetString());
    }

    [Fact]
    public async Task InputReceiverNodeExecutor_WithoutInput_ShouldInterrupt()
    {
        var executor = new InputReceiverNodeExecutor();
        var node = BuildNode("input_1", WorkflowNodeType.InputReceiver, new Dictionary<string, JsonElement>());
        var result = await executor.ExecuteAsync(CreateContext(node, new Dictionary<string, JsonElement>()), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(InterruptType.QuestionAnswer, result.InterruptType);
        Assert.True(result.Outputs.ContainsKey("input_received"));
        Assert.False(result.Outputs["input_received"].GetBoolean());
    }

    [Fact]
    public void NodeExecutionContext_EvaluateExpression_ShouldUseExprEvaluator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAstCache>(new AstCompilationCache(128));
        services.AddSingleton<IFunctionRegistry, BuiltinFunctionRegistry>();
        services.AddSingleton<ExprEvaluator>();
        var provider = services.BuildServiceProvider();

        var node = BuildNode("expr_1", WorkflowNodeType.Selector, new Dictionary<string, JsonElement>());
        var context = new NodeExecutionContext(
            node,
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["risk"] = JsonSerializer.SerializeToElement(98)
            },
            provider,
            new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            workflowId: 1001L,
            executionId: 2002L,
            workflowCallStack: new[] { 1001L },
            eventChannel: null);

        var evaluated = context.EvaluateExpression("{{risk}} >= 90");

        Assert.True(evaluated.GetBoolean());
    }

    private static NodeExecutionContext CreateContext(
        NodeSchema node,
        Dictionary<string, JsonElement> variables)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAstCache>(new AstCompilationCache(128));
        services.AddSingleton<IFunctionRegistry, BuiltinFunctionRegistry>();
        services.AddSingleton<ExprEvaluator>();
        var serviceProvider = services.BuildServiceProvider();
        return new NodeExecutionContext(
            node,
            variables,
            serviceProvider,
            new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            workflowId: 1001L,
            executionId: 2002L,
            workflowCallStack: new[] { 1001L },
            eventChannel: null);
    }

    private static NodeSchema BuildNode(
        string key,
        WorkflowNodeType nodeType,
        Dictionary<string, JsonElement> config)
    {
        return new NodeSchema(
            key,
            nodeType,
            key,
            config,
            new NodeLayout(0, 0, 120, 60));
    }
}
