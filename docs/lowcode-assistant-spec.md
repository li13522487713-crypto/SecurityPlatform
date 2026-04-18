# 低代码智能体规格（lowcode-assistant-spec）

> 范围：M18 assistant_coze 17 篇主线 + 三栏 IDE + 人设独立配置 + 提示词模板库跨域 + 模型跨层复用 + 渠道适配运行实体 + 长期记忆 vs 记忆库 + 插件完整域。

## §1 三栏 IDE 形态

| 区域 | 职责 |
| --- | --- |
| 左侧 | 人设（角色定位 / 性格 / 风格）+ 回复逻辑（系统提示 / 引用模板 / 兜底回复） |
| 中间 | 技能面板：插件 / 工作流 / 知识库 / 数据库 / 变量 / 长期记忆 / 记忆库（每项可启用 / 配置参数 / 调试入口） |
| 右侧 | 预览与调试：会话窗 + Trace 视图 + 输入注入 + 中断/恢复（与 M13 调试台共用） |

## §2 自然语言创建（A01）+ AI 创建双入口

- "AI 创建" 与 "手动创建" 都进入**同一**编排界面（非黑盒），区别仅在初始 schema 是否预填
- AI 创建调用 `IAssistantGenerationService.GenerateAsync(prompt)`：返回 `AssistantSchema`，包含建议人设 / 提示词 / 推荐技能（用户可逐项确认 / 修改）

## §3 提示词体系（A04-A06）

- 模板语法：Jinja-like (`{{ var }} / {% if %} / {% for %} / {% break %} / {% continue %}`) + Markdown + @ 快速引用
- @ 引用菜单 5 类：变量 / 技能 / 工作流 / 知识库 / 提示词模板
- 提示词模板库（PromptTemplate）作为**独立资源**：CRUD 端点 `POST /api/v1/lowcode/prompt-templates`、可被智能体引用、可被工作流 LLM 节点引用

## §4 提示词模板库跨域资源

| 引用方 | 复用机制 |
| --- | --- |
| 智能体（人设 / 系统提示 / 兜底回复） | `@template:{templateId}` 占位 |
| 工作流 LLM 节点（N02 Llm 节点 systemPrompt） | NodeExecutionContext 内自动渲染 |
| 渠道适配运行实体 | 渠道级 systemPrompt 重写时优先模板引用 |

## §5 模型设置（A16）+ 跨层复用

- 同一租户的所有智能体 / 工作流 LLM 节点共用 `IChatClientFactory` 提供的模型配置池
- 模型配置 ID 由 `ModelRegistry` 维护；智能体 / 节点仅持引用
- 切换模型只在配置池里改，所有引用方下次调用时生效（无需逐个重发）

## §6 技能扩展（A08-A14）

| 技能类型 | Atlas 实体 | 配置规则 |
| --- | --- | --- |
| 插件 | LowCodePluginDefinition + LowCodePluginAuthorization | 凭据 AES-CBC + lcp: 前缀；调用经 PluginRegistry.RouteFunction |
| 工作流 | DagWorkflow | sync / async / batch 三种 invoke 模式；超时/重试/熔断/降级在 ResiliencePolicy 中配置 |
| 知识库 | KnowledgeBase | 向量召回 + Rerank（接 Qdrant） |
| 数据库 | AiDatabase | 通过 DatabaseQuery / DatabaseInsert / DatabaseUpdate / DatabaseDelete 节点访问 |
| 变量 | AppVariable | 三作用域隔离；仅 page/app 可写 |
| 长期记忆 | LongTermMemory | 跨会话持久；按 userId 隔离 |
| 记忆库 | MemoryBank | 会话内短期；按 sessionId 隔离 |

## §7 长期记忆 vs 记忆库（A12 / A13）

| 维度 | 长期记忆 | 记忆库 |
| --- | --- | --- |
| 隔离键 | userId | sessionId |
| 生命周期 | 跨会话持久 | 会话结束清理 |
| 写入时机 | 显式 MemoryWrite 节点 / 智能体回复后自动抽取 | 自动捕获每条对话 |
| 读取入口 | MemoryRead 节点（可显式 prompt 注入） | 默认隐式注入 system context |
| 可见性 | 用户可在"个性化"页查看 / 删除 | 调试台可查看 |

## §8 预览与调试（A07）

与 M13 调试台共用：

- Trace 6 维（traceId / appId+page / component / 时间范围 / errorType / userId）
- Span 时间线视图按 startedAt 排序，按总耗时绝对宽度比例渲染
- 输入注入 / 中断 / 恢复 → 复用 RuntimeChatflowService 的 Pause/Inject/Resume

## §9 消息日志（A17）

LowCodeMessageLogEntry 跨域聚合：

- chatflow message / tool_call / final
- workflow execution start / end / step
- agent invoke / tool_call
- dispatch entry / exit / error

按 sessionId / workflowId / agentId / traceId 4 个键检索。

## §10 多渠道发布

| 渠道 | 适配器 | 入口 |
| --- | --- | --- |
| 飞书 | FeishuChannelAdapter | 长连接事件订阅 + Webhook 回调 |
| 微信公众号 | WechatChannelAdapter | 公众平台 OAuth + 推送回调 |
| 抖音 | DouyinChannelAdapter | 开放平台 OAuth + 长连接 |
| 豆包 | DoubaoChannelAdapter | 内嵌 SDK 按 appId 分发 |

每个渠道发布时构建 `AgentRuntimeEntity`：

- 模型配置 ID 引用
- 启用技能 ID 列表
- 长期记忆 / 记忆库开关
- 提示词模板覆盖
- token 配额（按渠道隔离，避免单渠道挤占）

写入 `agent_runtime_entity` 表 + 同步注册到调度中心 + 渠道路由表更新。

## §11 后端 `AgentChannelAdapter` 接口

```csharp
public interface IAgentChannelAdapter
{
    string ChannelType { get; } // feishu / wechat / douyin / doubao
    Task RegisterAsync(TenantId tenantId, string agentId, ChannelConfig config, CancellationToken ct);
    Task<string> HandleIncomingAsync(TenantId tenantId, string agentId, IncomingMessage msg, CancellationToken ct);
    Task<bool> VerifyWebhookAsync(string signature, string body, CancellationToken ct);
}
```

- OAuth：渠道级 access_token / refresh_token 加密存储（LowCodeCredentialProtector）
- Webhook：签名验签（常量时间比对，避免时序攻击）
- 推送回调：失败重试 + 指数退避（与 ResiliencePolicy 对齐）
