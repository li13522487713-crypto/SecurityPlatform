using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class DagExecutorIntegrationTests
{
    [Fact]
    public async Task RunAsync_ShouldEmitSelectorEdgeStatus_BySelectedBranch()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SelectorNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "selector_1",
                    WorkflowNodeType.Selector,
                    new Dictionary<string, JsonElement>
                    {
                        ["condition"] = JsonSerializer.SerializeToElement("true")
                    }),
                BuildNode("true_1", WorkflowNodeType.TextProcessor),
                BuildNode("false_1", WorkflowNodeType.TextProcessor),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "selector_1", "input", null),
                new ConnectionSchema("selector_1", "true", "true_1", "input", "true"),
                new ConnectionSchema("selector_1", "false", "false_1", "input", "false"),
                new ConnectionSchema("true_1", "output", "exit_1", "input", null),
                new ConnectionSchema("false_1", "output", "exit_1", "input", null)
            ]);

        var eventChannel = Channel.CreateUnbounded<SseEvent>();
        var execution = BuildExecution(30109L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel,
            CancellationToken.None);

        var events = await ReadEventsAsync(eventChannel);
        var selectorEdgeEvents = events
            .Where(x => string.Equals(x.Event, "edge_status_changed", StringComparison.Ordinal))
            .Select(x => JsonDocument.Parse(x.Data).RootElement)
            .Where(x =>
                x.TryGetProperty("edge", out var edge) &&
                edge.TryGetProperty("sourceNodeKey", out var sourceNodeKey) &&
                string.Equals(sourceNodeKey.GetString(), "selector_1", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(selectorEdgeEvents);

        var trueEdge = selectorEdgeEvents.LastOrDefault(x =>
        {
            var edge = x.GetProperty("edge");
            return edge.GetProperty("sourcePort").GetString() == "true";
        });
        var falseEdge = selectorEdgeEvents.LastOrDefault(x =>
        {
            var edge = x.GetProperty("edge");
            return edge.GetProperty("sourcePort").GetString() == "false";
        });

        Assert.NotEqual(JsonValueKind.Undefined, trueEdge.ValueKind);
        Assert.NotEqual(JsonValueKind.Undefined, falseEdge.ValueKind);
        Assert.Equal((int)EdgeExecutionStatus.Success, trueEdge.GetProperty("edge").GetProperty("status").GetInt32());
        Assert.Equal((int)EdgeExecutionStatus.Skipped, falseEdge.GetProperty("edge").GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task RunAsync_ShouldEmitIncompleteEdgeStatus_WhenNodeStarts()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SlowTextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode("text_1", WorkflowNodeType.TextProcessor),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var eventChannel = Channel.CreateUnbounded<SseEvent>();
        var execution = BuildExecution(30115L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel,
            CancellationToken.None);

        var events = await ReadEventsAsync(eventChannel);
        var incompleteEdgeEvent = events
            .Where(x => string.Equals(x.Event, "edge_status_changed", StringComparison.Ordinal))
            .Select(x => JsonDocument.Parse(x.Data).RootElement)
            .FirstOrDefault(x =>
            {
                if (!x.TryGetProperty("edge", out var edge))
                {
                    return false;
                }

                return string.Equals(edge.GetProperty("sourceNodeKey").GetString(), "text_1", StringComparison.OrdinalIgnoreCase) &&
                       edge.GetProperty("status").GetInt32() == (int)EdgeExecutionStatus.Incomplete;
            });

        Assert.NotEqual(JsonValueKind.Undefined, incompleteEdgeEvent.ValueKind);
    }

    [Fact]
    public async Task RunAsync_ShouldHandleSelectorBranchSkipping()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SelectorNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "selector_1",
                    WorkflowNodeType.Selector,
                    new Dictionary<string, JsonElement>
                    {
                        ["condition"] = JsonSerializer.SerializeToElement("true")
                    }),
                BuildNode(
                    "true_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("TRUE"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("branch_result")
                    }),
                BuildNode(
                    "false_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("FALSE"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("branch_result")
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "selector_1", "input", null),
                new ConnectionSchema("selector_1", "true", "true_1", "input", "true"),
                new ConnectionSchema("selector_1", "false", "false_1", "input", "false"),
                new ConnectionSchema("true_1", "output", "exit_1", "input", null),
                new ConnectionSchema("false_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30101L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel: null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        Assert.NotNull(execution.OutputsJson);
        using var doc = JsonDocument.Parse(execution.OutputsJson);
        Assert.Equal("TRUE", doc.RootElement.GetProperty("branch_result").GetString());
    }

    [Fact]
    public async Task RunAsync_ShouldPersistSkippedExecution_ForUnselectedSelectorBranch()
    {
        var dag = CreateDagExecutor(
            out var nodeExecutions,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SelectorNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "selector_1",
                    WorkflowNodeType.Selector,
                    new Dictionary<string, JsonElement>
                    {
                        ["condition"] = JsonSerializer.SerializeToElement("true")
                    }),
                BuildNode("true_1", WorkflowNodeType.TextProcessor),
                BuildNode("false_1", WorkflowNodeType.TextProcessor),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "selector_1", "input", null),
                new ConnectionSchema("selector_1", "true", "true_1", "input", "true"),
                new ConnectionSchema("selector_1", "false", "false_1", "input", "false"),
                new ConnectionSchema("true_1", "output", "exit_1", "input", null),
                new ConnectionSchema("false_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30105L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel: null,
            CancellationToken.None);

        var skippedNode = nodeExecutions.FirstOrDefault(n =>
            string.Equals(n.NodeKey, "false_1", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(skippedNode);
        Assert.Equal(ExecutionStatus.Skipped, skippedNode!.Status);
    }

    [Fact]
    public async Task RunAsync_ShouldHandleSelectorFalseBranchOutput()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SelectorNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "selector_1",
                    WorkflowNodeType.Selector,
                    new Dictionary<string, JsonElement>
                    {
                        ["condition"] = JsonSerializer.SerializeToElement("false")
                    }),
                BuildNode(
                    "true_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("TRUE"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("branch_result")
                    }),
                BuildNode(
                    "false_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("FALSE"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("branch_result")
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "selector_1", "input", null),
                new ConnectionSchema("selector_1", "true", "true_1", "input", "true"),
                new ConnectionSchema("selector_1", "false", "false_1", "input", "false"),
                new ConnectionSchema("true_1", "output", "exit_1", "input", null),
                new ConnectionSchema("false_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30106L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel: null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        Assert.NotNull(execution.OutputsJson);
        using var doc = JsonDocument.Parse(execution.OutputsJson);
        Assert.Equal("FALSE", doc.RootElement.GetProperty("branch_result").GetString());
    }

    [Fact]
    public async Task RunAsync_ShouldHandleDeepSelectorSkipPropagation_WithoutStackOverflow()
    {
        var dag = CreateDagExecutor(
            out var nodeExecutions,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SelectorNodeExecutor(),
            new TextProcessorNodeExecutor());

        const int falseBranchDepth = 1200;
        var nodes = new List<NodeSchema>
        {
            BuildNode("entry_1", WorkflowNodeType.Entry),
            BuildNode(
                "selector_1",
                WorkflowNodeType.Selector,
                new Dictionary<string, JsonElement>
                {
                    ["condition"] = JsonSerializer.SerializeToElement("true")
                }),
            BuildNode("true_1", WorkflowNodeType.TextProcessor),
            BuildNode("exit_1", WorkflowNodeType.Exit)
        };
        var connections = new List<ConnectionSchema>
        {
            new("entry_1", "output", "selector_1", "input", null),
            new("selector_1", "true", "true_1", "input", "true"),
            new("true_1", "output", "exit_1", "input", null)
        };

        for (var i = 0; i < falseBranchDepth; i++)
        {
            nodes.Add(BuildNode($"false_{i}", WorkflowNodeType.TextProcessor));
            if (i == 0)
            {
                connections.Add(new ConnectionSchema("selector_1", "false", "false_0", "input", "false"));
                continue;
            }

            connections.Add(new ConnectionSchema($"false_{i - 1}", "output", $"false_{i}", "input", null));
        }

        var canvas = new CanvasSchema(nodes, connections);
        var execution = BuildExecution(30112L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel: null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        var deepSkippedNode = nodeExecutions.FirstOrDefault(x =>
            string.Equals(x.NodeKey, $"false_{falseBranchDepth - 1}", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(deepSkippedNode);
        Assert.Equal(ExecutionStatus.Skipped, deepSkippedNode!.Status);
    }

    [Fact]
    public async Task RunAsync_WhenSelectorSkipPropagationTooDeep_ShouldFailWithDepthGuard()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SelectorNodeExecutor(),
            new TextProcessorNodeExecutor());

        const int falseBranchDepth = 2100;
        var nodes = new List<NodeSchema>
        {
            BuildNode("entry_1", WorkflowNodeType.Entry),
            BuildNode(
                "selector_1",
                WorkflowNodeType.Selector,
                new Dictionary<string, JsonElement>
                {
                    ["condition"] = JsonSerializer.SerializeToElement("true")
                }),
            BuildNode("true_1", WorkflowNodeType.TextProcessor),
            BuildNode("exit_1", WorkflowNodeType.Exit)
        };
        var connections = new List<ConnectionSchema>
        {
            new("entry_1", "output", "selector_1", "input", null),
            new("selector_1", "true", "true_1", "input", "true"),
            new("true_1", "output", "exit_1", "input", null)
        };

        for (var i = 0; i < falseBranchDepth; i++)
        {
            nodes.Add(BuildNode($"false_{i}", WorkflowNodeType.TextProcessor));
            if (i == 0)
            {
                connections.Add(new ConnectionSchema("selector_1", "false", "false_0", "input", "false"));
                continue;
            }

            connections.Add(new ConnectionSchema($"false_{i - 1}", "output", $"false_{i}", "input", null));
        }

        var canvas = new CanvasSchema(nodes, connections);
        var execution = BuildExecution(30113L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel: null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Failed, execution.Status);
        Assert.Contains("图传播深度超过限制", execution.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenNodeTimeoutConfigured_ShouldFailByTimeout()
    {
        var dag = CreateDagExecutor(
            out var nodeExecutions,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new SlowTextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "text_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["timeoutMs"] = JsonSerializer.SerializeToElement(20)
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30114L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel: null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Failed, execution.Status);
        Assert.Contains("节点执行超时", execution.ErrorMessage, StringComparison.Ordinal);
        var timedOutNode = nodeExecutions.FirstOrDefault(x => string.Equals(x.NodeKey, "text_1", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(timedOutNode);
        Assert.Equal(ExecutionStatus.Failed, timedOutNode!.Status);
    }

    [Fact]
    public async Task RunAsync_ShouldHandleLoopBreakSignal()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new LoopNodeExecutor(),
            new BreakNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "loop_1",
                    WorkflowNodeType.Loop,
                    new Dictionary<string, JsonElement>
                    {
                        ["mode"] = JsonSerializer.SerializeToElement("count"),
                        ["maxIterations"] = JsonSerializer.SerializeToElement(10),
                        ["indexVariable"] = JsonSerializer.SerializeToElement("loop_index")
                    }),
                BuildNode(
                    "break_1",
                    WorkflowNodeType.Break,
                    new Dictionary<string, JsonElement>
                    {
                        ["reason"] = JsonSerializer.SerializeToElement("manual-stop")
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "loop_1", "input", null),
                new ConnectionSchema("loop_1", "body", "break_1", "input", null),
                new ConnectionSchema("loop_1", "done", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30102L);
        await dag.RunAsync(Tenant(), execution, canvas, new Dictionary<string, JsonElement>(), null, CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        using var doc = JsonDocument.Parse(execution.OutputsJson ?? "{}");
        Assert.True(doc.RootElement.GetProperty("loop_completed").GetBoolean());
    }

    [Fact]
    public async Task RunAsync_ShouldExecuteBatchSubCanvas()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new BatchNodeExecutor(),
            new TextProcessorNodeExecutor());

        var childCanvas = new CanvasSchema(
            Nodes:
            [
                BuildNode(
                    "child_text_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("{{batch_item}}"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("item_text")
                    })
            ],
            Connections: []);

        var batchNode = BuildNode(
            "batch_1",
            WorkflowNodeType.Batch,
            new Dictionary<string, JsonElement>
            {
                ["inputArrayPath"] = JsonSerializer.SerializeToElement("items"),
                ["outputKey"] = JsonSerializer.SerializeToElement("batch_results")
            },
            childCanvas);

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                batchNode,
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "batch_1", "input", null),
                new ConnectionSchema("batch_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30103L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>
            {
                ["items"] = JsonSerializer.SerializeToElement(new[] { "A", "B" })
            },
            null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        using var doc = JsonDocument.Parse(execution.OutputsJson ?? "{}");
        var results = doc.RootElement.GetProperty("batch_results");
        Assert.Equal(2, results.GetArrayLength());
    }

    [Fact]
    public async Task RunAsync_ShouldMaterializeInputMappings_IntoNodeExecutionContext()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "text_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("告警：{{incident.summary}}"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("rendered"),
                        ["inputMappings"] = JsonSerializer.SerializeToElement(new Dictionary<string, string>
                        {
                            ["incident"] = "ticket.payload"
                        })
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30116L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>
            {
                ["ticket"] = JsonSerializer.SerializeToElement(new
                {
                    payload = new
                    {
                        summary = "主机异常登录"
                    }
                })
            },
            null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        Assert.NotNull(execution.OutputsJson);
        using var doc = JsonDocument.Parse(execution.OutputsJson);
        Assert.Equal("告警：主机异常登录", doc.RootElement.GetProperty("rendered").GetString());
    }

    [Fact]
    public async Task RunAsync_WithPreCompletedNodeKeys_ShouldSkipSpecifiedNode()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "text_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("HELLO"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("greet")
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30104L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            null,
            CancellationToken.None,
            preCompletedNodeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text_1" });

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        using var doc = JsonDocument.Parse(execution.OutputsJson ?? "{}");
        Assert.False(doc.RootElement.TryGetProperty("greet", out _));
    }

    [Fact]
    public async Task RunAsync_WithSkippedPreCompletedNodeKeys_ShouldSkipSpecifiedNode()
    {
        var dag = CreateDagExecutor(
            out var nodeExecutions,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "text_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("HELLO"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("greet")
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30107L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            null,
            CancellationToken.None,
            preCompletedNodeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text_1" });

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        var textNodeExecution = nodeExecutions.FirstOrDefault(x => string.Equals(x.NodeKey, "text_1", StringComparison.OrdinalIgnoreCase));
        Assert.Null(textNodeExecution);
    }

    [Fact]
    public async Task RunAsync_WhenAllPredecessorsSkipped_ShouldSkipDownstreamNode()
    {
        var dag = CreateDagExecutor(
            out var nodeExecutions,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new TextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode(
                    "text_1",
                    WorkflowNodeType.TextProcessor,
                    new Dictionary<string, JsonElement>
                    {
                        ["template"] = JsonSerializer.SerializeToElement("HELLO"),
                        ["outputKey"] = JsonSerializer.SerializeToElement("greet")
                    }),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30110L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            null,
            CancellationToken.None,
            preCompletedNodeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text_1" });

        Assert.Equal(ExecutionStatus.Completed, execution.Status);
        var exitExecution = nodeExecutions.FirstOrDefault(x => string.Equals(x.NodeKey, "exit_1", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(exitExecution);
        Assert.Equal(ExecutionStatus.Skipped, exitExecution!.Status);
    }

    [Fact]
    public async Task RunAsync_WhenNodeExecutionFails_ShouldFailWorkflowAndStopDownstream()
    {
        var dag = CreateDagExecutor(
            out var nodeExecutions,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new FailingTextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode("text_1", WorkflowNodeType.TextProcessor),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var execution = BuildExecution(30108L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            null,
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Failed, execution.Status);
        Assert.Contains("模拟节点失败", execution.ErrorMessage, StringComparison.Ordinal);
        var blocked = nodeExecutions.FirstOrDefault(x => string.Equals(x.NodeKey, "exit_1", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(blocked);
        Assert.Equal(ExecutionStatus.Blocked, blocked!.Status);
    }

    [Fact]
    public async Task RunAsync_WhenNodeExecutionFails_ShouldEmitNodeBlockedEvent()
    {
        var dag = CreateDagExecutor(
            out _,
            new EntryNodeExecutor(),
            new ExitNodeExecutor(),
            new FailingTextProcessorNodeExecutor());

        var canvas = new CanvasSchema(
            Nodes:
            [
                BuildNode("entry_1", WorkflowNodeType.Entry),
                BuildNode("text_1", WorkflowNodeType.TextProcessor),
                BuildNode("exit_1", WorkflowNodeType.Exit)
            ],
            Connections:
            [
                new ConnectionSchema("entry_1", "output", "text_1", "input", null),
                new ConnectionSchema("text_1", "output", "exit_1", "input", null)
            ]);

        var eventChannel = Channel.CreateUnbounded<SseEvent>();
        var execution = BuildExecution(30111L);
        await dag.RunAsync(
            Tenant(),
            execution,
            canvas,
            new Dictionary<string, JsonElement>(),
            eventChannel,
            CancellationToken.None);

        var events = await ReadEventsAsync(eventChannel);
        var blockedEvent = events.FirstOrDefault(x => string.Equals(x.Event, "node_blocked", StringComparison.Ordinal));
        Assert.NotNull(blockedEvent);
        using var payload = JsonDocument.Parse(blockedEvent.Data);
        Assert.Equal("exit_1", payload.RootElement.GetProperty("nodeKey").GetString());
    }

    private static TenantId Tenant() => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private static WorkflowExecution BuildExecution(long executionId)
        => new(
            Tenant(),
            workflowId: 1001L,
            versionNumber: 1,
            createdByUserId: 1L,
            inputsJson: "{}",
            id: executionId);

    private static NodeSchema BuildNode(
        string key,
        WorkflowNodeType type,
        Dictionary<string, JsonElement>? config = null,
        CanvasSchema? childCanvas = null,
        IReadOnlyList<NodeFieldMapping>? inputSources = null)
        => new(
            key,
            type,
            key,
            config ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase),
            new NodeLayout(0, 0, 160, 60),
            childCanvas,
            InputSources: inputSources);

    private static DagExecutor CreateDagExecutor(
        out List<WorkflowNodeExecution> nodeExecutions,
        params INodeExecutor[] executors)
    {
        var capturedNodeExecutions = new List<WorkflowNodeExecution>();
        var nodeRepo = Substitute.For<IWorkflowNodeExecutionRepository>();
        nodeRepo.AddAsync(Arg.Any<WorkflowNodeExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        nodeRepo
            .When(x => x.AddAsync(Arg.Any<WorkflowNodeExecution>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => capturedNodeExecutions.Add(callInfo.Arg<WorkflowNodeExecution>()));
        nodeRepo.UpdateAsync(Arg.Any<WorkflowNodeExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var executionRepo = Substitute.For<IWorkflowExecutionRepository>();
        executionRepo.AddAsync(Arg.Any<WorkflowExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        executionRepo.UpdateAsync(Arg.Any<WorkflowExecution>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        executionRepo.FindByIdAsync(Arg.Any<TenantId>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkflowExecution?>(null));

        var idGenerator = Substitute.For<IIdGeneratorAccessor>();
        var nextId = 5000L;
        idGenerator.NextId().Returns(_ => Interlocked.Increment(ref nextId));

        var registry = new NodeExecutorRegistry(executors);
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = Substitute.For<ILogger<DagExecutor>>();
        nodeExecutions = capturedNodeExecutions;
        return new DagExecutor(registry, nodeRepo, executionRepo, idGenerator, services, logger);
    }

    private static async Task<List<SseEvent>> ReadEventsAsync(Channel<SseEvent> eventChannel)
    {
        var result = new List<SseEvent>();
        while (await eventChannel.Reader.WaitToReadAsync())
        {
            while (eventChannel.Reader.TryRead(out var ev))
            {
                result.Add(ev);
            }
        }

        return result;
    }

    private sealed class FailingTextProcessorNodeExecutor : INodeExecutor
    {
        public WorkflowNodeType NodeType => WorkflowNodeType.TextProcessor;

        public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NodeExecutionResult(
                Success: false,
                Outputs: new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase),
                ErrorMessage: "模拟节点失败"));
        }
    }

    private sealed class SlowTextProcessorNodeExecutor : INodeExecutor
    {
        public WorkflowNodeType NodeType => WorkflowNodeType.TextProcessor;

        public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(200, cancellationToken);
            return new NodeExecutionResult(
                Success: true,
                Outputs: new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
