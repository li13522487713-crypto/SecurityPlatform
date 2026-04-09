# 微软 Semantic Kernel 与 Microsoft Agent Framework 子代理 (Sub-Agent) 团队构建指南 (2026 最新版)

## 1. 框架演进与最新版本 (2026)

作为深耕 C# 领域的工程师，您可能已经熟悉了 **Semantic Kernel (SK)**。在 2025 年底至 2026 年初，微软对 AI 代理框架进行了重大升级。原有的 **Semantic Kernel Agent Framework** 已经正式演进为 **Microsoft Agent Framework (MAF)**。

**Microsoft Agent Framework (MAF)** 是 Semantic Kernel Agents 与 AutoGen 的继任者，旨在提供更强大的企业级代理能力。它集成了 AutoGen 的简单抽象与 Semantic Kernel 的会话管理、类型安全及遥测功能。

### 核心 NuGet 包 (2026 公测版)
- `Microsoft.Agents.AI`: 代理核心抽象。
- `Microsoft.Agents.Workflows`: 多代理编排与图模型工作流。
- `Microsoft.Agents.AI.OpenAI` / `Azure.AI.OpenAI`: 模型适配器。
- `Microsoft.SemanticKernel.Agents.Orchestration`: 针对 SK 的兼容与编排层。

---

## 2. 打造子代理 (Sub-Agent) 团队的核心模式

在最新框架中，构建“团队”能力主要有三种最佳方案，根据业务逻辑的复杂度选择：

| 编排模式 | 适用场景 | 核心组件 |
| :--- | :--- | :--- |
| **Group Chat (群聊)** | 多个代理共同协作、讨论，模拟会议或头脑风暴。 | `GroupChatOrchestration` |
| **Workflow (工作流)** | 具有明确业务逻辑、状态机、分支和循环的复杂流程。 | `Workflow`, `Executor`, `Edge` |
| **Handoff (接力)** | 代理在完成阶段性任务后，将控制权移交给另一个特定代理。 | `HandoffOrchestration` |

---

## 3. 最佳方案：Group Chat 团队编排示例

以下是使用最新 `Microsoft.SemanticKernel.Agents.Orchestration` 打造一个“文案策划团队”的代码示例。该方案允许子代理（CopyWriter）和审核代理（Reviewer）通过 `GroupChat` 进行协作。

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;

// 1. 初始化 Kernel
Kernel kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o", apiKey)
    .Build();

// 2. 定义子代理 (Sub-Agents)
ChatCompletionAgent writer = new ChatCompletionAgent {
    Name = "CopyWriter",
    Instructions = "你是一位资深文案，擅长简洁幽默的风格。每次只提供一个方案。",
    Kernel = kernel,
};

ChatCompletionAgent reviewer = new ChatCompletionAgent {
    Name = "Reviewer",
    Instructions = "你是一位创意总监，负责审核文案。如果合格请回复'批准'，否则提供修改建议。",
    Kernel = kernel,
};

// 3. 设置团队编排 (Group Chat)
// 使用 RoundRobin 管理器，轮流发言，最多 5 轮
GroupChatOrchestration teamOrchestration = new GroupChatOrchestration(
    new RoundRobinGroupChatManager { MaximumInvocationCount = 5 },
    writer, 
    reviewer);

// 4. 启动运行时并执行任务
InProcessRuntime runtime = new InProcessRuntime();
await runtime.StartAsync();

var result = await teamOrchestration.InvokeAsync(
    "为一款平价且好开的纯电 SUV 创作一句口号。", 
    runtime);

string finalOutput = await result.GetValueAsync();
Console.WriteLine($"最终方案: {finalOutput}");
```

---

## 4. 进阶方案：基于 Workflow 的子代理编排

如果您需要更严密的子代理控制（例如：子代理 A 完成后必须由子代理 B 校验，失败则返回 A），推荐使用 **Microsoft Agent Framework Workflows**。

- **Executor**: 每一个子代理或业务函数都是一个执行器。
- **Edge**: 定义执行器之间的流转逻辑，支持条件判断。
- **State Management**: 自动保存中间状态，支持长耗时任务的断点续传。

### 专家建议
1. **优先使用 Microsoft Agent Framework (MAF)**: 对于 2026 年的新项目，MAF 提供了更完备的图模型编排能力，是 SK Agents 的正式升级路径。
2. **利用 Microsoft.Extensions.AI**: 确保您的代理逻辑与底层模型解耦，方便在 Azure OpenAI、OpenAI 或本地模型（如 Ollama）之间切换。
3. **子代理作为 Plugin**: 在 SK 中，您可以将一个完整的 Agent 封装为一个 `KernelPlugin`，供主代理调用，从而实现层级化（Hierarchical）代理架构。
