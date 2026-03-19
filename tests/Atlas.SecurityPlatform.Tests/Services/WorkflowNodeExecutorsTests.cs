using System.Text.Json;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
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
        Assert.True(result.Outputs["selector_result"].GetBoolean());
        Assert.Equal("true_branch", result.Outputs["selected_branch"].GetString());
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

    private static NodeExecutionContext CreateContext(
        NodeSchema node,
        Dictionary<string, JsonElement> variables)
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
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
