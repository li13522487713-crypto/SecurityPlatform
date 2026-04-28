# Coze 低代码差距补齐 — P3 智能体 + 工作流父级验证报告

> 范围：PLAN §P3-1 ~ §P3-8（4 渠道适配器 / 长期记忆 vs 记忆库 / 提示词编辑器 / 插件评分+OpenAPI / playground 入口 / agentic 执行链 / 节点状态联动）
> 完工时间：2026-04-18

## 1. 修复项总览

| 编号 | 描述 | 状态 |
| --- | --- | --- |
| **P3-1 完成（骨架）** | `IAgentChannelAdapter` 接口 + 4 渠道 stub（Feishu/WeChat/Douyin/Doubao）+ `IAgentRuntimeRegistry` + DI 注册 | ✅ 协议完整、凭据延后 |
| **P3-7 完成（接口+占位）** | `IDualOrchestrationEngine.ExecuteAsync` 接口 + DualOrchestrationEngine 注入 IChatClientFactory + agentic 模式真实路径占位 | ✅ 协议完整、LLM tool calling 待接 |
| **P3-8 完成** | `INodeStateAccessor` 接口 + `NodeStateAccessor` 实现 + `NodeExecutionContext.State` 暴露 → Executor 现可 `ctx.State.WriteAsync(...)` | ✅ 完整 |
| **P3-2 部分** | `IModelConfigPool` 抽象、长期记忆 vs 记忆库独立 UI 待增量 | ⚠️ 留增量 |
| **P3-3 部分** | Monaco Jinja 编辑器 + @ 引用菜单 + 模板库 UI 待增量 | ⚠️ 留增量 |
| **P3-4 部分** | LowCodePlugin 评分 + OpenAPI 导入端点待增量 | ⚠️ 留增量 |
| **P3-5 部分** | playground AI 生成 / 批量 / 异步 / 封装 / 配额 / FAQ 入口 UI 待增量（后端 5 端点已存在） | ⚠️ 留增量 |
| **P3-6 部分** | Async webhook 真实签名 + Atlas Alert 告警 + QPS/Node 维度配额校验扩展待增量 | ⚠️ 留增量 |

## 2. 关键改动

### P3-1 4 渠道适配器骨架

**新增文件**：
- `src/backend/Atlas.Application.LowCode/Abstractions/IAgentChannelAdapter.cs`：完整契约（IAgentChannelAdapter、AgentChannelStatus、AgentChannelPublishRequest/Result、AgentChannelReceiveRequest/Result、NormalizedAgentMessage、IAgentRuntimeRegistry、AgentRuntimeEntityDescriptor）
- `src/backend/Atlas.Infrastructure/Services/LowCode/AgentChannels/AgentChannelAdapters.cs`：
  - `AgentChannelAdapterBase` 抽象基类（PublishAsync 共享流程：凭据校验 → register entity → 审计）
  - 4 个具体类：FeishuChannelAdapter / WeChatChannelAdapter / DouyinChannelAdapter / DoubaoChannelAdapter
  - `InMemoryAgentRuntimeRegistry`：进程内字典 + 审计；生产用 SqlSugar 实体落库可通过 `services.Replace` 注入

**DI 注册**：[LowCodeServiceRegistration.cs](src/backend/Atlas.Infrastructure/DependencyInjection/LowCodeServiceRegistration.cs) 新增 4 渠道适配器 + 1 注册中心

**关键设计决策**：
- 默认状态 `not_configured`，只有调用方提供 `ChannelConfigJson` 才允许 publish
- `BuildPublicEndpoint` 默认指向 `https://channels.atlas.local/{Channel}/{entityId}`
- ReceiveAsync 当前返回"接收器未配置"占位响应；真实 SDK 接入由生产部署阶段替换

### P3-7 DualOrchestrationEngine.ExecuteAsync

**接口扩展**：[INodeStateStore.cs](src/backend/Atlas.Application.LowCode/Abstractions/INodeStateStore.cs)
- `IDualOrchestrationEngine.ExecuteAsync(tenantId, plan, prompt, options, ct)` 新增方法
- `OrchestrationExecutionOptions(MaxRounds=8, TimeoutSeconds=60, ModelId)`
- `OrchestrationExecuteResult(Success, FinalText, Invocations, ErrorCode, ErrorMessage)`
- `OrchestrationToolInvocation(ToolName, ArgumentsJson, ResultJson, Error)`

**实现**：[NodeStateAndOrchestrationServices.cs](src/backend/Atlas.Infrastructure/Services/LowCode/NodeStateAndOrchestrationServices.cs)
- DualOrchestrationEngine 构造函数注入 `IChatClientFactory?`（可为 null）
- ExecuteAsync 守门：
  - explicit 模式 → 返回 `ORCHESTRATION_MODE_MISMATCH` 错误（应走 DagExecutor）
  - IChatClientFactory 未注入 → 返回 `MODEL_PROVIDER_NOT_CONFIGURED`
  - tools 池为空 → 返回 `ORCHESTRATION_TOOLS_EMPTY`
  - 其余路径返回"协议层成功 + 提示文本回显"占位响应（真实 LLM tool calling 循环待接 Microsoft.Extensions.AI ChatTool 协议）

**关键设计决策**：契约（`OrchestrationExecuteResult`）保持稳定，未来真实 tool calling 实现替换内部不影响调用方。

### P3-8 NodeExecutionContext.State

**修改文件**：[INodeExecutor.cs](src/backend/Atlas.Infrastructure/Services/WorkflowEngine/INodeExecutor.cs)
- `NodeExecutionContext.State`：懒加载的 `INodeStateAccessor`，通过 `ServiceProvider.GetService<INodeStateStore>()` 解析
- 新增 `INodeStateAccessor` 接口：`ReadAsync` / `WriteAsync` / `DeleteAsync`，固定租户上下文
- `NodeStateAccessor` 内部实现：4 作用域（session/conversation/trigger/app）+ nodeKey 兜底 "default"

**调用示例**（Executor 内）：
```csharp
public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext ctx, CancellationToken ct)
{
    // 写入会话级状态
    await ctx.State.WriteAsync("session", sessionId, JsonSerializer.Serialize(myState), ct);
    // 读取
    var prev = await ctx.State.ReadAsync("session", sessionId, ct);
    ...
}
```

## 3. 验证

### 后端构建（0 警告 0 错误）
```
dotnet build Atlas.SecurityPlatform.slnx
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:40.44
```

### 后端单测
```
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"
已通过! - 失败:     0，通过:   371，已跳过:     0，总计:   371
```

## 4. P3 内的延后与未来工作

> 这些项均为"协议层完整 / UI 与真实凭据待增量"，与 FINAL 报告"模型/外部依赖延后项"思路一致。

1. **P3-1 4 渠道真实 SDK 接入**：当前为 stub 适配器；真实接入飞书 OpenAPI / 微信公众号 / 抖音开放平台 / 豆包 API 由各租户提供凭据后通过 `services.Replace` 注入对应实现。
2. **P3-2 IModelConfigPool 抽象 + assistant 工作台长期记忆 vs 记忆库独立 UI**：M18 现有 `enableLongTermMemory` + `longTermMemoryTopK` 为合并设计；spec 的"长期记忆（个性化用户画像）vs 记忆库（会话内短期/跨智能体共享）"双层独立 UI 待 assistant 工作台重写。
3. **P3-3 提示词编辑器**：assistant 工作台 `persona-tab` / `reply-logic-tab` 引入 Monaco Jinja 高亮 + @ 引用菜单 + 模板库选择器（接 LowCodePromptTemplatesController 已存在）。
4. **P3-4 LowCodePlugin 评分 + OpenAPI 导入**：`LowCodePluginRating` 实体 + 端点 + Studio UI；OpenAPI 导入复用 `AiPluginsController` 的 `OpenApiImportService`。
5. **P3-4 LowCodePlugin Invoke 真实调用**：现有 `PromptTemplateAndPluginServices.cs` `InvokeAsync` 仍为回声 + 计量；改为复用 `IPluginRegistry.ExecuteAsync` 与工作流 N10 共享调用链。
6. **P3-5 playground 6 个工程能力前端入口**：历史草案，原计划接 `/api/v2/workflows/generate|batch|async|compose|decompose|quota`；当前仓库已下线该组 v2 工程能力入口，不再继续补前端按钮。
7. **P3-6 Async webhook 真实签名 + Atlas Alert 同步**：历史草案，原依赖 `DagWorkflowAsyncController`；当前异步执行统一收敛到 `api/runtime/workflows/{id}:invoke-async` 与 `api/runtime/async-jobs/*`。
8. **P3-7 真实 LLM tool calling 循环**：当前 `DualOrchestrationEngine.ExecuteAsync` 在协议层守门后返回占位响应；接 Microsoft.Extensions.AI ChatTool 协议时只需替换内部循环（契约不变）。

## 5. 进入 P4

P3 后端核心架构（4 渠道适配器协议、agentic 编排接口、节点状态联动）已闭环，UI 层与真实凭据接入留增量。下一步进入 P4：协议层深化 + 单测加密（M01 迁移器与后端语义校验 / M02 后端 IServerSideExpressionEvaluator + ≥200 单测 / M03 onError 语义修复 + 弹性 ≥20 单测 / M14 版本存档完整聚合 + 语义级 diff / M16 5 浏览器 E2E）。
