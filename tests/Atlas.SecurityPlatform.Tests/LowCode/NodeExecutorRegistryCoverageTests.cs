using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Atlas.SecurityPlatform.Tests.LowCode;

/// <summary>
/// P0-2 + P0-3 守门测试：所有已声明的 <see cref="WorkflowNodeType"/>（除 Comment / Entry 兜底类型）
/// 必须在 <see cref="NodeExecutorRegistry"/>._executorTypes 中有对应 INodeExecutor 注册。
///
/// 此前 M12 的 3 个 Trigger 节点 + M20 的 17 个新节点全部"已声明未注册"，
/// DagExecutor 静默 SuccessResult 跳过，导致画布业务逻辑被悄悄吞掉（FINAL 报告未发现）。
///
/// 本测试在每次新增 WorkflowNodeType 时强制要求同步注册执行器，否则立即挂掉。
/// </summary>
public sealed class NodeExecutorRegistryCoverageTests
{
    /// <summary>
    /// Comment 是纯画布注释，无运行时语义，DagExecutor 主动跳过，无需注册。
    /// </summary>
    private static readonly HashSet<WorkflowNodeType> ExemptedTypes =
    [
        WorkflowNodeType.Comment,
    ];

    [Fact]
    public void Every_Declared_WorkflowNodeType_Must_Have_Executor_Registered()
    {
        var registry = new NodeExecutorRegistry(new ServiceCollection().BuildServiceProvider());
        var executorTypes = GetExecutorTypesDictionary(registry);

        var allDeclared = Enum.GetValues<WorkflowNodeType>();
        var missing = new List<WorkflowNodeType>();
        foreach (var t in allDeclared)
        {
            if (ExemptedTypes.Contains(t)) continue;
            if (!executorTypes.ContainsKey(t))
            {
                missing.Add(t);
            }
        }

        Assert.True(missing.Count == 0,
            $"以下 WorkflowNodeType 已声明但未在 NodeExecutorRegistry._executorTypes 注册执行器：\n" +
            string.Join("\n", missing.Select(x => $"  - {x} (={(int)x})")) +
            "\n请在 NodeExecutorRegistry 字典中加入对应 typeof(XxxNodeExecutor) 注册，" +
            "否则 DagExecutor 将抛 NODE_EXECUTOR_NOT_REGISTERED 错误（不再静默吞业务）。");
    }

    [Theory]
    // P0-2 修复：M12 三个 Trigger 节点
    [InlineData(WorkflowNodeType.TriggerUpsert)]
    [InlineData(WorkflowNodeType.TriggerRead)]
    [InlineData(WorkflowNodeType.TriggerDelete)]
    // P0-3 修复：M20 17 个新节点
    [InlineData(WorkflowNodeType.Variable)]
    [InlineData(WorkflowNodeType.ImageGenerate)]
    [InlineData(WorkflowNodeType.Imageflow)]
    [InlineData(WorkflowNodeType.ImageReference)]
    [InlineData(WorkflowNodeType.ImageCanvas)]
    [InlineData(WorkflowNodeType.SceneVariable)]
    [InlineData(WorkflowNodeType.SceneChat)]
    [InlineData(WorkflowNodeType.LtmUpstream)]
    [InlineData(WorkflowNodeType.MemoryRead)]
    [InlineData(WorkflowNodeType.MemoryWrite)]
    [InlineData(WorkflowNodeType.MemoryDelete)]
    [InlineData(WorkflowNodeType.ImageGeneration)]
    [InlineData(WorkflowNodeType.Canvas)]
    [InlineData(WorkflowNodeType.ImagePlugin)]
    [InlineData(WorkflowNodeType.VideoGeneration)]
    [InlineData(WorkflowNodeType.VideoToAudio)]
    [InlineData(WorkflowNodeType.VideoFrameExtraction)]
    public void P0_New_Node_Type_Must_Be_Registered(WorkflowNodeType nodeType)
    {
        var registry = new NodeExecutorRegistry(new ServiceCollection().BuildServiceProvider());
        var executorTypes = GetExecutorTypesDictionary(registry);
        Assert.True(executorTypes.ContainsKey(nodeType),
            $"P0 修复要求：{nodeType} 必须在 NodeExecutorRegistry._executorTypes 注册。");
    }

    [Fact]
    public void Total_Registered_Executor_Types_Must_Include_All_M12_M20_Plus_Existing()
    {
        var registry = new NodeExecutorRegistry(new ServiceCollection().BuildServiceProvider());
        var executorTypes = GetExecutorTypesDictionary(registry);
        // P0 修复后：Existing 41 + M12(3) + M20(17) = 61
        // 此处只断言"至少 ≥ 60"，留出未来扩展空间，不锁死具体数字
        Assert.True(executorTypes.Count >= 60,
            $"NodeExecutorRegistry 注册数量应 ≥60（P0 修复后），实际 {executorTypes.Count}");
    }

    private static Dictionary<WorkflowNodeType, Type> GetExecutorTypesDictionary(NodeExecutorRegistry registry)
    {
        var f = typeof(NodeExecutorRegistry).GetField("_executorTypes", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(f);
        var dict = (Dictionary<WorkflowNodeType, Type>?)f!.GetValue(registry);
        Assert.NotNull(dict);
        return dict!;
    }
}
