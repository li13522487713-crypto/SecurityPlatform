---
name: Coze 低代码全量复刻实施计划
overview: 按 docx 架构报告 14 章 + 附录 A/B/C/D（共 49 节点 + 27 篇 UI Builder 文档 + 17 篇 assistant_coze 主线）全量完整复刻三层平台。严格使用 docx §十一推荐 10 项技术栈（React + TS + Semi + dnd-kit + Monaco + Zustand + TanStack Query + JSONata + Taro + Yjs），现有架构不满足处一律增强而非绕过；共 20 个里程碑，每个里程碑必须交付该领域的完整功能（不允许 MVP / 演示切片 / 占位 / 概念省略），按"先前端完整落地 → 后端能力按前端契约完整建模"的强约束节奏执行。
todos:
  - id: m01
    content: M01 lowcode-schema 完整 Schema 协议层：AppSchema/PageSchema/ComponentSchema/BindingSchema/EventSchema/ActionSchema/VariableSchema/SlotSchema/LifecycleSchema/PropertyPanelSchema/ComponentMeta/ResourceRef/PublishedArtifact/VersionArchive/RuntimeStatePatch/RuntimeTrace 共 16 类完整定义 + zod 校验 + 类型守卫 + Schema 升级器；后端 Atlas.Domain.LowCode 全套镜像聚合 + 完整 CRUD/draft/snapshot API
    status: pending
  - id: m02
    content: M02 lowcode-expression 完整表达式引擎：JSONata 完整接入 + 模板字符串求值（Jinja-like）+ 7 种作用域根（page/app/system/component/event/workflow.outputs/chatflow.outputs）+ 类型推断 + 错误位置 + 自动补全索引 + 依赖追踪 + 反向索引 + Monaco LSP 适配器；后端表达式安全沙箱与日志审计
    status: pending
  - id: m03
    content: M03 lowcode-action-runtime 完整动作运行时：7 种内置动作（set_variable/call_workflow/call_chatflow/navigate/open_external_link/show_toast/update_component）+ 动作链编排（顺序/并行/条件/异常分支）+ 事务式状态补丁（成功合并/失败回滚）+ Loading/Error 自动目标切换 + 扩展机制；与后端 dispatch 协议对齐
    status: pending
  - id: m04
    content: M04 lowcode-editor-canvas 完整画布：dnd-kit + dnd-kit/sortable + dnd-kit/modifiers 全套；自由 + 流式 + 响应式 三种布局；对齐线 + 吸附线 + 框选 + 多选 + 复制粘贴 + 缩放 + 网格 + 参考线 + 全套快捷键；画布快照 + 撤销/重做 + 版本对比；后端 autosave + 稿锁
    status: pending
  - id: m05
    content: M05 lowcode-editor-outline + lowcode-editor-inspector + lowcode-property-forms 设计器右侧三件套完整：结构树拖拽改父子/显隐/锁定；检查器三 Tab（属性/样式/事件）；propertyPanels 元数据驱动；5 种值源切换（static/variable/expression/workflow_output/chatflow_output）；Monaco 表达式编辑器内嵌
    status: pending
  - id: m06
    content: M06 lowcode-component-registry + lowcode-components-web 完整组件体系：ComponentMeta 完整字段（含 supportedValueType/bindableProps/supportedEvents/childPolicy/propertyPanels）；docx §8.2 + UI Builder 27 篇文档列出的全部 30+ 网页组件全量实现；后端 components/registry 持久化与租户级覆盖
    status: pending
  - id: m07
    content: M07 lowcode-studio-web 应用设计器完整壳：多页面 + 页面路径 + 多端类型（web/mini_program/hybrid）+ 三栏布局（资源/画布/检查器）+ 业务逻辑/用户界面 顶部切换 + 完整快捷键体系（U24）+ 资源面板聚合 + i18n 双语；后端 AppPages/AppVariables/AppResources 完整 CRUD
    status: pending
  - id: m08
    content: M08 lowcode-runtime-web 完整渲染器：Schema 渲染 + 多端类型分发 + 状态补丁 + 事件分发 + Workflow.loading/Workflow.error 自动绑定 + 生命周期 + 错误边界 + 性能埋点；后端 RuntimeSchemaController 完整 + 版本/草稿/灰度切换
    status: pending
  - id: m09
    content: M09 lowcode-workflow-adapter 完整 Workflow 适配器：覆盖 docx §8.3 模式 A（表单→工作流→回填）与模式 B（动态选项填充）；同步/异步/批量三种调用；inputMapping + outputMapping + loadingTargets + errorTargets + trace 全链路；后端 RuntimeWorkflowsController.Invoke + Batch + Async 三入口
    status: pending
  - id: m10
    content: M10 lowcode-asset-adapter 完整资产适配器：docx §8.3 模式 C 上传-处理-预览全闭环；prepare-upload / complete-upload 两阶段；File→fileHandle/URL/imageId 完整转换；mime 白名单 + 等保审计；资产生命周期 7 天 GC；后端 RuntimeFilesController + Hangfire 回收任务
    status: pending
  - id: m11
    content: M11 lowcode-chatflow-adapter + lowcode-session-adapter 完整：docx §8.3 模式 D 对话流+表单混合页；SSE/HTTP2 真流式 + tool_call/message/error/final 四类事件；多会话创建/切换/历史/清空；后端 RuntimeChatflowsController + RuntimeSessionsController 修复 Coze fallback
    status: pending
  - id: m12
    content: M12 lowcode-trigger-adapter + lowcode-webview-policy-adapter 完整：CRON + 事件双触发；外链域名白名单完整治理（添加/校验/审计/吊销）；后端 RuntimeTriggersController + WebviewDomainsController + Hangfire 调度 + DAG 节点 34/35/36 完整执行器
    status: pending
  - id: m13
    content: M13 lowcode-debug-client 完整调试台 + 统一事件分发：docx §10.4.2/10.4.3 dispatch 协议全量落地；Span 树 + 错误链路 + traceId 检索 + JSON 树 + 日志脱敏；OpenTelemetry 全量 instrumentation；后端 RuntimeEventsController.Dispatch + RuntimeTraceService + 把 list_spans 从 fallback 改为 OK
    status: pending
  - id: m14
    content: M14 lowcode-versioning-client 完整版本管理：VersionArchive 聚合（schema 快照 + 依赖资源版本 + 构建产物元数据 + 备注）；diff 视图 + rollback + 审计；IResourceReferenceGuardService 删除阻断；后端 AppVersionsController 完整三接口
    status: pending
  - id: m15
    content: M15 lowcode-runtime-mini + Taro 多端运行时完整：完整 Taro 工程；docx §10.5/§十一同一份 Schema 多渲染器；30+ 组件 web 与 mini 双实现；多端 Schema 兼容性测试；后端 ?renderer=web|mini 与组件能力差异返回
    status: pending
  - id: m16
    content: M16 Yjs + y-websocket 完整协同编辑：多人光标 + 选区 + 操作锁 + 离线快照 + 冲突合并 + 历史回放；后端 LowCodeCollabHub（SignalR + y-websocket bridge）+ 离线快照定期落 VersionArchive
    status: pending
  - id: m17
    content: M17 lowcode-web-sdk + 发布 + 外链白名单完整：Hosted App / Embedded SDK / Preview Artifact 三类产物完整产出；AtlasLowcode.mount() 完整 API；rsbuild library 模式打包；后端 AppPublishController 三入口 + 产物指纹与版本绑定 + 外链域名验证全链路
    status: pending
  - id: m18
    content: M18 智能体层完整复刻（assistant_coze A01-A17 + 17 篇主线文档）：自然语言创建 + AI 创建（A01）+ 提示词体系 Jinja/Markdown（A04-A06）+ 模型设置（A16）+ 插件/知识库/数据库/变量/长期记忆完整接入 + 预览调试（A07）+ 消息日志（A17）+ 多渠道发布（飞书/微信/抖音/豆包）；前端在 module-studio-react 内增强而非新建
    status: pending
  - id: m19
    content: M19 工作流父级工程能力完整补齐：AI 生成工作流（B07）+ 批量执行（B05）+ 异步执行（B06）+ 封装与解散子工作流（B08）+ 限制治理与 FAQ（B03/B04）；前端在 @coze-workflow/playground 内增强；后端补齐 Hangfire 批量调度 + 异步队列 + 子流程封装 API
    status: pending
  - id: m20
    content: M20 工作流节点 49 全集补齐 + Atlas 扩展对齐：图像 N44/N45/N46 + 视频 N47/N48/N49 + 上游 ImageGenerate(14)/Imageflow(15)/ImageReference(16)/ImageCanvas(17)/SceneVariable(24)/SceneChat(25)/上游 LTM(26) 拆分对齐 + Variable(11) 单节点 + TriggerUpsert/Read/Delete(34/35/36)；前端节点面板 + 后端执行器 + .http + 测试 + 校验矩阵 100% 覆盖
    status: pending
isProject: false
---

# Coze 低代码全量复刻实施计划

## 一、强约束（含二轮深审新增 4 条）

- **完整实现，禁止 MVP**：每个里程碑必须交付该领域的完整能力，不允许"演示页 / 最小切片 / 占位 / 二阶段再做"等表述。
- **概念零遗漏**：docx 14 章正文 + 附录 A/B/C/D（17+8+49+27=101 条概念）必须分配到具体里程碑，每条概念必须有可追溯的 case 编号；二轮深审新增 30 条细节同样必须可追溯。
- **技术栈强锁定**：仅使用 docx §十一 10 项推荐栈：
  - React 18 + TypeScript（严格模式）
  - `@douyinfe/semi-ui`
  - `@dnd-kit/core` + `@dnd-kit/sortable` + `@dnd-kit/modifiers`
  - `monaco-editor` + `@monaco-editor/react`
  - `zustand`
  - `@tanstack/react-query`
  - `jsonata`
  - `@tarojs/taro`
  - `yjs` + `y-websocket`
- **现状不满足必须增强**：现有 `@coze-workflow/playground` 缺协同/AI 生成/异步/批量等能力，必须在 M16/M19 直接增强；不允许"绕过"或"另建一套"。
- **前端先行 → 后端按契约建模**：每个里程碑的 case 编号 C 系列（前端）必须在 S 系列（后端）之前完成；后端只接受由前端契约推导的 DTO/REST 设计。
- **【新增 1】API 前缀双套强约束**：
  - **设计态写操作**（PlatformHost）：`/api/v1/lowcode/...`
  - **运行时只读/状态变更**（AppHost）：`/api/runtime/...`
  - 同一资源在两套前缀下语义不同；不允许混用、不允许任何"全部用 v1"或"全部用 runtime"的简化。
- **【新增 2】"标准化协议唯一桥梁"强约束**（docx §七）：UI 禁止直接调用 workflow / chatflow / files / triggers / sessions 零散 API，**所有运行时事件必须经 `POST /api/runtime/events/dispatch`**（M13 落地）；适配器层只能由 dispatch 控制器内部调用，不得在前端绕过。
- **【新增 3】"作用域变量隔离"强约束**（docx §8.5）：page / app / system 三作用域**禁止跨作用域 setVariable**；component/event/workflow.outputs/chatflow.outputs 四种只读作用域**禁止写入**；表达式引擎与 action-runtime 双层校验。
- **【新增 4】"元数据驱动 vs 硬编码"强约束**（docx §七 + §10.3）：所有组件 props/events/childPolicy/bindableProps/supportedValueType 由 ComponentMeta 声明；**禁止组件内部硬编码注入业务逻辑**（如硬写"调用某 workflow"），必须经事件 → 动作链。
- **零警告 + 零错误 + i18n 双语 + .http 同步 + contracts.md 同步**：进入下一里程碑前的硬门槛。
- **等保 2.0**：审计、加密、白名单、脱敏、生命周期、引用治理在每个里程碑明确列出对应 case。

## 二、技术栈与新增 packages / apps 锁定

### 2.1 新增 packages（共 22 个）

新增 [src/frontend/packages/lowcode-\*/](src/frontend/packages)：

- 协议层：`@atlas/lowcode-schema`（含 shared-types 子模块导出）、`@atlas/lowcode-expression`、`@atlas/lowcode-action-runtime`
- 设计器：`@atlas/lowcode-editor-canvas`、`@atlas/lowcode-editor-outline`、`@atlas/lowcode-editor-inspector`、`@atlas/lowcode-property-forms`、`@atlas/lowcode-component-registry`、`@atlas/lowcode-components-web`、`@atlas/lowcode-components-mini`
- 运行时：`@atlas/lowcode-runtime-web`、`@atlas/lowcode-runtime-mini`
- 适配器：`@atlas/lowcode-workflow-adapter`、`@atlas/lowcode-chatflow-adapter`、`@atlas/lowcode-session-adapter`、`@atlas/lowcode-trigger-adapter`、`@atlas/lowcode-asset-adapter`、`@atlas/lowcode-webview-policy-adapter`、`@atlas/lowcode-plugin-adapter`（M18 新增）
- 周边：`@atlas/lowcode-debug-client`、`@atlas/lowcode-versioning-client`、`@atlas/lowcode-web-sdk`、`@atlas/lowcode-collab-yjs`

### 2.2 新增 apps（共 4 个，端口分配）

| App | 端口 | 用途 | 里程碑 |
| --- | --- | --- | --- |
| `apps/lowcode-studio-web` | 5183 | 应用设计器主入口（自研全栈） | M07 |
| `apps/lowcode-preview-web` | 5184 | 调试预览壳（独立 app，HMR 推送 + 扫码移动端预览 + iframe 嵌套） | M08 |
| `apps/lowcode-sdk-playground` | 5186 | Web SDK 嵌入示例（script/npm/iframe 三种） | M17 |
| `apps/lowcode-mini-host` | 5187 (Taro dev) | Taro 多端宿主（微信/抖音小程序 + H5） | M15 |

`apps/*` 严格遵循 AGENTS.md "只装配、能力沉淀到 packages" 约束。

### 2.3 后端模块边界（docx §9.3 services 4+1 → Atlas Host 映射）

| docx services | Atlas 落点 | 主要 Controller / Service | 端口 |
| --- | --- | --- | --- |
| schema-service | PlatformHost | AppDefinitionsController / AppPagesController / AppVariablesController / AppVersionsController | 5001 |
| publish-service | PlatformHost | AppPublishController / WebviewDomainsController（设计态部分） | 5001 |
| runtime-api | AppHost | RuntimeSchemaController / RuntimeWorkflowsController / RuntimeChatflowsController / RuntimeFilesController / RuntimeSessionsController / RuntimeTriggersController / RuntimeEventsController（dispatch） / RuntimeVersionsController | 5002 |
| debug-hub | AppHost | RuntimeTraceService / RuntimeMessageLogService / SignalR `/hubs/lowcode-debug` | 5002 |
| collab-hub（新增第 5 个） | AppHost | LowCodeCollabHub（SignalR + y-websocket bridge） `/hubs/lowcode-collab` | 5002 |

后端新增域：[Atlas.Domain.LowCode](src/backend/Atlas.Domain.LowCode) / [Atlas.Application.LowCode](src/backend/Atlas.Application.LowCode) / [Atlas.Infrastructure/Services/LowCode](src/backend/Atlas.Infrastructure/Services/LowCode) / [Atlas.Infrastructure/Services/LowCodePlugin](src/backend/Atlas.Infrastructure/Services/LowCodePlugin)（M18）。

### 2.4 新增专项契约文档（共 9 份，docx 二轮深审新增 4 份）

- [docs/contracts.md](docs/contracts.md) 新增「低代码应用 UI Builder」章节
- [docs/lowcode-runtime-spec.md](docs/lowcode-runtime-spec.md)
- [docs/lowcode-binding-matrix.md](docs/lowcode-binding-matrix.md)
- [docs/lowcode-component-spec.md](docs/lowcode-component-spec.md)
- [docs/lowcode-publish-spec.md](docs/lowcode-publish-spec.md)
- [docs/lowcode-collab-spec.md](docs/lowcode-collab-spec.md)
- [docs/lowcode-assistant-spec.md](docs/lowcode-assistant-spec.md)
- **【新增】**[docs/lowcode-shortcut-spec.md](docs/lowcode-shortcut-spec.md) — 完整快捷键清单（M04/M07）
- **【新增】**[docs/lowcode-message-log-spec.md](docs/lowcode-message-log-spec.md) — 消息日志 + 执行链路统一查询契约（M13/M18）
- **【新增】**[docs/lowcode-resilience-spec.md](docs/lowcode-resilience-spec.md) — 超时/重试/退避/熔断/降级/配额（M09/M11/M19）
- **【新增】**[docs/lowcode-orchestration-spec.md](docs/lowcode-orchestration-spec.md) — 两种编排哲学 + 有状态工作流契约（M19/M20）
- **【新增】**[docs/lowcode-content-params-spec.md](docs/lowcode-content-params-spec.md) — 内容参数 6 类独立机制（M05/M06）
- **【新增】**[docs/lowcode-plugin-spec.md](docs/lowcode-plugin-spec.md) — 插件市场/创建/调用/授权/计量（M18）

## 三、Studio / Runtime 完整数据流

```mermaid
flowchart LR
    Designer["Studio 设计器(M04-M07)"] -->|编辑| Schema["Schema 协议(M01)"]
    Schema -->|jsonata| Expression["表达式引擎(M02)"]
    Schema -->|action_chain| ActionRuntime["动作运行时(M03)"]
    Schema -->|拉取| Runtime["runtime-web(M08)/runtime-mini(M15)"]
    Designer -->|HMR diff| PreviewWeb["preview-web(M08)"]
    Runtime -->|onClick| ActionRuntime
    ActionRuntime -->|"统一 dispatch(强约束)"| DispatchEndpoint["POST /api/runtime/events/dispatch(M13)"]
    DispatchEndpoint -->|workflow| WorkflowAdapter["workflow-adapter(M09)"]
    DispatchEndpoint -->|chatflow| ChatflowAdapter["chatflow-adapter(M11)"]
    DispatchEndpoint -->|asset| AssetAdapter["asset-adapter(M10)"]
    DispatchEndpoint -->|trigger| TriggerAdapter["trigger-adapter(M12)"]
    DispatchEndpoint -->|external| WebviewPolicy["webview-policy-adapter(M12)"]
    DispatchEndpoint -->|plugin| PluginAdapter["plugin-adapter(M18)"]
    DispatchEndpoint -->|trace| Debug["debug-client(M13)"]
    Debug -->|union view| MessageLog["消息日志+执行链路统一视图"]
    Designer -->|另存| Versioning["versioning-client(M14)"]
    Designer -->|协同| Collab["collab-yjs(M16)"]
    Versioning -->|发布| Publish["web-sdk + 三类产物(M17)"]
    Designer -->|智能体| Assistant["module-studio-react 增强 + 插件域(M18)"]
    Designer -->|工作流| WorkflowPlayground["@coze-workflow/playground 增强(M19,M20)"]
```

## 四、里程碑详细拆分

### M01 完整 Schema 协议层

**docx 概念覆盖**：§10.1（设计原则 5 条）+ §10.2.1-10.2.5（应用/页面/变量/组件/绑定/事件/动作 7 类 schema）+ §10.3（ComponentMeta）+ §10.7（PublishedArtifact）+ §10.6（VersionArchive）+ **【新增】§U11 内容参数独立类型 ContentParamSchema** + **【新增】§9.3 shared-types 子模块**。

**前端 case**：

- C01-1 `lowcode-schema/src/types/`：定义 17 类完整类型——`AppSchema`、`PageSchema`、`ComponentSchema`、`BindingSchema`、**`ContentParamSchema`**（独立于 BindingSchema，6 类内容参数 union）、`EventSchema`、`ActionSchema`（含 7 子类型 union）、`VariableSchema`、`SlotSchema`、`LifecycleSchema`、`PropertyPanelSchema`、`ComponentMeta`、`ResourceRef`、`PublishedArtifact`、`VersionArchive`、`RuntimeStatePatch`、`RuntimeTrace`。字段对齐 docx §10.2 全部代码块，**禁止裁剪任何字段**（如 `lifecycle`、`fallback`、`loadingTargets`、`errorTargets`、`trace`、`persist`、`readonly` 等）。
- C01-2 `lowcode-schema/src/zod/`：每类提供完整 zod 校验 + 反序列化器 + 错误路径定位。
- C01-3 `lowcode-schema/src/guards/`：类型守卫（`isCallWorkflowAction(a)` 等）+ Action discriminator + 作用域守卫（`isPageScope(s)` 等）。
- C01-4 `lowcode-schema/src/migrate/`：Schema 版本演进框架（v1→v2 升级器、向下兼容降级器、版本字段 `schemaVersion` 强约束）。
- C01-5 `lowcode-schema/src/shared/`：**shared-types 子模块**——通用枚举（ValueType/Scope/RendererType/ChannelType）、常量、工具类型；`exports` 字段单独导出 `./shared`。
- C01-6 `lowcode-schema/src/index.ts` 完整导出 + tsdoc 注释 + 100% Vitest 单测。
- C01-7 中英文词条进 [src/frontend/apps/app-web/src/app/i18n/zh-CN.ts](src/frontend/apps/app-web/src/app/i18n/zh-CN.ts) 与 `en-US.ts`。

**后端 case**：

- S01-1 [Atlas.Domain.LowCode/Entities/](src/backend/Atlas.Domain.LowCode)：`AppDefinition` / `PageDefinition` / `AppVariable` / `AppContentParam` / `AppVersionArchive` / `AppPublishArtifact` / `AppResourceReference` 共 7 个 `TenantEntity`，字段与 C01-1 镜像。
- S01-2 [Atlas.Application.LowCode/](src/backend/Atlas.Application.LowCode)：`AppDefinitionDto` / `PageDefinitionDto` / `AppSchemaSnapshotDto` / `AppContentParamDto` 全套；FluentValidation 完整覆盖；AutoMapper Profile。
- S01-3 [Atlas.Infrastructure/Repositories/LowCode/](src/backend/Atlas.Infrastructure/Repositories/LowCode)：SqlSugar 仓储；批量查询/批量更新（禁止循环内 DB 操作）；schema JSON 列存。
- S01-4 PlatformHost 新增 `AppDefinitionsController`（**设计态 v1 前缀**）：`GET/POST/PUT/DELETE /api/v1/lowcode/apps`、`GET /api/v1/lowcode/apps/{id}/draft`、`POST /api/v1/lowcode/apps/{id}/draft`、`POST /api/v1/lowcode/apps/{id}/snapshot`。
- S01-5 [src/backend/Atlas.PlatformHost/Bosch.http/AppDefinitions.http](src/backend/Atlas.PlatformHost/Bosch.http) 全 endpoint 覆盖；xUnit 单测覆盖创建/更新/快照/审计。
- S01-6 [docs/contracts.md](docs/contracts.md) 新增「低代码 AppDefinition」章节；[docs/lowcode-runtime-spec.md](docs/lowcode-runtime-spec.md) 写明 Schema 字段全集与 17 类类型 ER 图。

**等保 2.0**：所有写接口走 `IAuditWriter`；schema JSON 字段含敏感数据时按租户密钥加密。

**验证**：`pnpm run test:unit @atlas/lowcode-schema`、`dotnet build`（0 警告）、`dotnet test`、`pnpm run i18n:check`、`pnpm run lint`。

### M02 完整表达式引擎

**docx 概念覆盖**：§10.5（Adapter 协议翻译）+ §A04-A05（Jinja/Markdown 模板）+ §10.2.4（5 种 sourceType）+ **【新增】§8.5 作用域变量隔离强约束**。

**前端 case**：

- C02-1 `lowcode-expression/src/jsonata/`：完整封装 `jsonata`，提供 `evaluate(expr, scope)`、`evaluateAsync(expr, scope)`、`compile(expr)`（缓存 AST）。
- C02-2 `lowcode-expression/src/template/`：Jinja-like 模板字符串求值器（与提示词体系兼容），支持 `{{ var }}`、`{% if %}`、`{% for %}`。
- C02-3 7 种作用域根：`page.*`（读写）/ `app.*`（读写）/ `system.*`（只读）/ `component.<id>.*`（只读）/ `event.*`（只读）/ `workflow.outputs.*`（只读）/ `chatflow.outputs.*`（只读）。
- C02-4 **【新增】作用域强隔离**：`assertWritable(path, currentScope)`——禁止跨作用域写入（如 page 动作不能写 app.*；component/event/workflow/chatflow 只读作用域写入抛 `ScopeViolationError`）；与 M03 action-runtime 双层校验。
- C02-5 `lowcode-expression/src/inference/`：基于 schema 的类型推断 + 错误位置（行/列/范围）+ 自动补全候选索引（用于 Monaco）。
- C02-6 `lowcode-expression/src/deps/`：依赖追踪 `extractDeps(expr)` + 反向索引（变量改 → 哪些 binding 需重算）。
- C02-7 `lowcode-expression/src/monaco/`：Monaco LSP 适配器（语法高亮、悬浮提示、自动补全、跨作用域写入红线警告）。

**后端 case**：

- S02-1 后端 `IServerSideExpressionEvaluator`：服务端预校验绑定可解析性（不在循环内执行）；表达式 AST 缓存。
- S02-2 表达式审计：执行错误进入 `LowCodeExpressionAuditLog`（脱敏后入库，遵守 [docs/lowcode-resilience-spec.md](docs/lowcode-resilience-spec.md) 脱敏规则）。
- S02-3 [docs/lowcode-runtime-spec.md](docs/lowcode-runtime-spec.md) 新增「表达式语法、7 作用域、隔离规则」章节。

**验证**：表达式单测 ≥ 200 case；作用域违规 ≥ 30 用例覆盖；Monaco 集成截图测试。

### M03 完整动作运行时

**docx 概念覆盖**：§10.2.5 完整 ActionSchema（7 子类型）+ §10.4.3 statePatches/messages/errors 响应格式 + **【新增】§10.8 超时重试熔断降级抽象**。

**前端 case**：

- C03-1 `lowcode-action-runtime/src/dispatcher/`：`ActionDispatcher` 类，注册 7 种内置动作（`set_variable` / `call_workflow` / `call_chatflow` / `navigate` / `open_external_link` / `show_toast` / `update_component`）。
- C03-2 `lowcode-action-runtime/src/chain/`：动作链编排——顺序、并行（`Promise.all`）、条件（`when` 表达式）、异常分支（`onError`）；与表达式引擎联动。
- C03-3 `lowcode-action-runtime/src/state-patch/`：事务式状态补丁（基于 immer）；成功合并 / 失败回滚；批量补丁（多动作合并提交）。
- C03-4 `lowcode-action-runtime/src/loading/`：`loadingTargets`/`errorTargets` 自动挂载/卸载（Workflow.loading、Workflow.error 状态绑定 docx §七）。
- C03-5 **【新增】`lowcode-action-runtime/src/scope-guard/`**：跨作用域写入校验（与 M02 双层校验）；只读作用域写入直接抛错。
- C03-6 **【新增】`lowcode-action-runtime/src/resilience/`**：超时/重试/退避/熔断/降级抽象层（具体策略由各 Adapter 在 M09/M11 落实）；提供 `withResilience(action, policy)` 包装器。
- C03-7 `lowcode-action-runtime/src/extend/`：`registerActionKind(kind, handler)` 扩展机制（Adapter 注册）。
- C03-8 单测覆盖 7 种动作 + 4 种链式编排 + 状态补丁回滚 + 作用域违规 + 弹性策略。

**后端 case**：

- S03-1 后端 dispatch 协议契约文件 [docs/lowcode-runtime-spec.md](docs/lowcode-runtime-spec.md) 新增「动作链与状态补丁、超时熔断降级」章节，与 C03-1/C03-3/C03-6 完全对齐。
- S03-2 后端 `IActionExecutor`（M13 dispatch 控制器使用）类型预留；接口定义随 M03 落仓。

**验证**：单测 100% 分支；动作链可视化预览；resilience 策略 ≥ 20 用例。

### M04 完整画布

**docx 概念覆盖**：§九 Studio 设计态 + §七设计器形态 + **【新增】§U24 完整快捷键清单（独立专项文档）**。

**前端 case**：

- C04-1 `lowcode-editor-canvas/src/dnd/`：`@dnd-kit/core` + `@dnd-kit/sortable` + `@dnd-kit/modifiers` 完整集成；DragOverlay；拖拽到画布、画布内移动、跨容器拖拽、嵌套拖拽。
- C04-2 `lowcode-editor-canvas/src/layout/`：自由布局 + 流式布局 + 响应式布局 三种 LayoutEngine 完整实现（对齐 docx PageSchema.layout 三枚举）。
- C04-3 `lowcode-editor-canvas/src/guides/`：对齐线、吸附线、参考线、网格、智能距离提示。
- C04-4 `lowcode-editor-canvas/src/select/`：单选 + 框选 + 多选 + Ctrl/Shift 加选 + 跨层级选择。
- C04-5 `lowcode-editor-canvas/src/clipboard/`：复制/剪切/粘贴/同位粘贴/跨页面粘贴/跨应用粘贴。
- C04-6 `lowcode-editor-canvas/src/zoom/`：缩放（25%-400%）+ 适应屏幕 + 实际大小。
- C04-7 `lowcode-editor-canvas/src/keymap/`：完整快捷键体系；**枚举所有快捷键并落到 [docs/lowcode-shortcut-spec.md](docs/lowcode-shortcut-spec.md)**——撤销/重做/复制/剪切/粘贴/同位粘贴/删除/全选/取消选择/对齐（左中右上下）/分组/解组/锁定/显隐/居中/键盘移动 1px/Shift+10px/包裹容器/解包/前置/后置/上移一层/下移一层/Tab 切换 Tab/Ctrl+S 保存/Ctrl+Shift+P 命令面板/Ctrl+/ 帮助 等 ≥ 40 项。
- C04-8 `lowcode-editor-canvas/src/history/`：基于 zustand + 切片存储的撤销/重做（≥ 50 步）；与 M16 yjs 协同协议互斥（协同模式下走 CRDT 历史，本地模式走切片栈）。

**后端 case**：

- S04-1 `AppDraftAutoSaveController`（PlatformHost / 设计态 v1）：`POST /api/v1/lowcode/apps/{id}/autosave`（去抖 30s 自动保存，schema diff 增量提交）。
- S04-2 `IAppDraftLockService`：稿锁服务（基于 SQLite + 心跳），多设备并发警告与强制夺锁；审计完整。
- S04-3 [docs/lowcode-shortcut-spec.md](docs/lowcode-shortcut-spec.md)：完整快捷键清单 + 冲突检测 + 用户自定义键位策略。

**验证**：Playwright `pnpm run test:e2e:app -- lowcode-canvas.spec.ts` 覆盖拖拽/对齐/快捷键 ≥ 40 项/撤销重做完整脚本。

### M05 完整设计器右侧三件套（含内容参数独立机制）

**docx 概念覆盖**：§九"组件拖拽、结构树、属性面板"+ §10.3 ComponentMeta + **【新增】§U11 内容参数独立机制** + **【新增】§8.5 调试+版本是绑定系统伴随能力哲学**。

**前端 case**：

- C05-1 `lowcode-editor-outline`：结构树（react-arborist 风格自实现）；拖拽改父子；显隐切换；锁定切换；右键菜单（复制/删除/重命名/导出片段）；搜索过滤。
- C05-2 `lowcode-editor-inspector`：右侧三 Tab（属性 / 样式 / 事件）+ Tab 锁定 + 折叠组。
- C05-3 `lowcode-property-forms/src/renderer/`：基于 ComponentMeta.propertyPanels 元数据驱动的 Semi `Form` 渲染器；表单分组、依赖项、动态显示。
- C05-4 `lowcode-property-forms/src/value-source/`：5 种值源 Tab（static / variable / expression / workflow_output / chatflow_output）；切换时类型校验；fallback 配置；preview。
- C05-5 **【新增】`lowcode-property-forms/src/content-params/`**：6 类内容参数独立分支（与一般 binding 区分）：
  - `text`（文案）：模板字符串 + i18n key + 静态文本
  - `image`（图片）：URL / fileHandle / imageId / 占位图
  - `data`（数据）：Array / Object 数据源（接 workflow output 或变量）
  - `link`（链接）：内部路由 / 外部 URL（受 webview 白名单约束）
  - `media`（媒体）：视频/音频 URL + 封面
  - `ai`（AI 内容）：chatflow 流式输出 / AI 卡片配置
  - 详见 [docs/lowcode-content-params-spec.md](docs/lowcode-content-params-spec.md)
- C05-6 `lowcode-property-forms/src/monaco/`：内嵌 Monaco（基于 M02 LSP 适配器）的表达式编辑器；变量树侧栏；快捷插入；语法错误高亮 + **跨作用域写入红线警告**。
- C05-7 `lowcode-editor-inspector/src/events/`：事件配置面板，可视化编辑 ActionSchema（含 7 子类型表单）+ 动作链编排（顺序/并行/条件/异常分支可视化）+ 弹性策略配置（超时/重试/降级目标）。
- C05-8 **【新增】哲学声明**：在 `lowcode-property-forms/README.md` 显式声明"调试 + 版本是绑定系统的伴随能力"，禁止把调试/版本视为后置功能。

**后端 case**：

- S05-1 ComponentMeta 拉取：`GET /api/v1/lowcode/components/registry?renderer=web`（与 M06 共享）。
- S05-2 设计态校验：`POST /api/v1/lowcode/apps/{id}/validate` 返回完整错误列表（schema 校验 + 表达式校验 + 资源引用校验 + 内容参数校验 + 作用域校验）。
- S05-3 [docs/lowcode-content-params-spec.md](docs/lowcode-content-params-spec.md)：6 类内容参数完整规格 + 与 BindingSchema 的差异说明 + 后端校验规则。

**验证**：Vitest 单测覆盖 propertyPanels 渲染 ≥ 100 case；6 类内容参数 ≥ 30 用例；Playwright E2E 覆盖事件配置全流程。

### M06 完整组件注册表与 Web 组件库

**docx 概念覆盖**：§10.3 ComponentMeta + §8.2 表单组件矩阵 + 附录 D U06/U07/U26-U37 全部网页组件 + **【新增】§七 AI 原生组件特征 + §10.3 元数据驱动禁硬编码**。

**前端 case**：

- C06-1 `lowcode-component-registry/src/`：`registerComponent(meta)`、`getRegistry()`、`ComponentMeta`（完整字段：type/displayName/category/supportedValueType/bindableProps/contentParams/supportedEvents/childPolicy/propertyPanels/icon/group/version/runtimeRenderer）。**新增 contentParams 字段**声明组件支持的内容参数类型。
- C06-2 **【新增】`lowcode-component-registry/src/principles/`**：元数据驱动校验器——在组件注册时检查是否在组件实现内硬编码了业务逻辑（如直接 fetch、直接 import workflow client），违反者 CI 失败。
- C06-3 `lowcode-components-web/src/components/`：完整 30+ 网页组件实现（按 docx 全文 + 附录 D 抽取，**禁止减项**）：
  - 布局（layout）：Container / Row / Column / Tabs / Drawer / Modal / Grid / Section
  - 展示（display）：Text / Markdown / Image / Video / Avatar / Badge / Progress / Rate / Chart / EmptyState / Loading / Error / Toast
  - 输入（input）：Button / TextInput / NumberInput / Switch / Select / RadioGroup / CheckboxGroup / DatePicker / TimePicker / ColorPicker / Slider / FileUpload / ImageUpload / CodeEditor / FormContainer / FormField / SearchBox / Filter
  - **AI 原生（ai）**：AiChat / AiCard / AiSuggestion / AiAvatarReply
  - 数据（data）：WaterfallList / Table / List / Pagination
- C06-4 **【新增】AI 原生组件特征矩阵**（独立子章节交付到 [docs/lowcode-component-spec.md](docs/lowcode-component-spec.md)）：每个 AI 组件必须满足——绑定 chatflow / 绑定模型 / 带 SSE 流式渲染 / 支持 tool_call 气泡 / 历史回放 / 中断恢复。
- C06-5 每个组件的 ComponentMeta 完整声明（属性面板 + 支持事件 + 支持值类型 + 子策略 + 内容参数）。
- C06-6 每个组件的 Vitest + RTL 单测；视觉回归（基于 Playwright screenshot）。
- C06-7 i18n 中英双语完整覆盖；远程检索默认 20 条满足 AGENTS.md 前端规范。
- C06-8 **【新增】组件能力 6 维矩阵**作为 M06 强制交付物：组件 × (表单值采集 / 事件触发 / 工作流输出回填 / AI 原生绑定 / 上传产物 / 内容参数) → [docs/lowcode-component-spec.md](docs/lowcode-component-spec.md) 表格化呈现，零空缺。

**后端 case**：

- S06-1 `LowCodeComponentManifestService`：合并静态 manifest（前端构建时导出）+ 数据库租户级覆盖项（自定义组件、隐藏组件、默认 props 覆盖）。
- S06-2 PlatformHost `LowCodeComponentsController`（**设计态 v1**）：`GET /api/v1/lowcode/components/registry`、`POST /api/v1/lowcode/components/overrides`（租户级配置）。
- S06-3 [docs/lowcode-component-spec.md](docs/lowcode-component-spec.md)：列出全部 30+ 组件的能力 6 维矩阵 + AI 原生特征矩阵 + 元数据驱动原则示例（正例与反例）。

**验证**：每组件单测 + 视觉回归 + 注册表 API .http；元数据驱动校验器 CI 集成。

### M07 完整应用 Studio 壳

**docx 概念覆盖**：§七 UI Builder 应用层 + §U01-U24 全部 + **【新增】§七截图 5 个左侧 Tab + §四提示词模板库 + §七投射模式 + §U16 UI Builder FAQ + §U24 完整快捷键面板**。

**前端 case**：

- C07-1 新建 [src/frontend/apps/lowcode-studio-web](src/frontend/apps)（Rsbuild 工程，**端口 5183**）；`AGENTS.md` 约束："apps 只装配，能力沉淀到 packages"。
- C07-2 三栏壳层：**左侧 5 个 Tab（组件 / 模板 / 结构 / 数据 / 资源）**（按 docx §七截图原文）；中部画布；右侧检查器；顶部业务逻辑/用户界面切换；右上预览/调试/发布/版本/协作入口。
- C07-3 多页面管理（页面树 + 路由配置 + 多端类型 web/mini_program/hybrid + 复制/删除/排序）。
- C07-4 变量管理面板（界面变量 / 应用变量 / 系统变量 三作用域 + 9 类 valueType + 跨作用域写入校验提示）。
- C07-5 **【新增】资源面板"投射模式"**：UI Builder 不引入新资源类型，资源由各资源域管理（工作流 / 对话流 / 数据库 / 知识库 / 变量 / 会话 / 触发器 / 文件资产 / 插件 / 长期记忆 / 记忆库 / **提示词模板**）；UI Builder 只投射引用；远程检索 + 默认 20 条。
- C07-6 **【新增】"模板"Tab**：页面模板 / 组件组合模板 / 模式 A/B/C/D 模板 / 行业模板（电商 / 客服 / 内容生成 / 数据分析）；支持模板创建、分享、应用。
- C07-7 **【新增】"数据"Tab**：数据源管理（工作流输出绑定 / 数据库快捷查询 / 静态 mock / 共享数据源）+ 数据源预览。
- C07-8 完整快捷键体系（对齐 docx §U24）+ **快捷键面板（Ctrl+/）展示完整清单**。
- C07-9 **【新增】UI Builder FAQ 内置面板**（对应 §U16）：常见问题自动检索 + 一键定位（与 M19 工作流 FAQ 区分）。
- C07-10 i18n：新增 `lowcode_studio.*` 完整词条（≥ 300 条），中英对齐。
- C07-11 在 [src/frontend/packages/app-shell-shared/src/routes.ts](src/frontend/packages/app-shell-shared) 注册 `/apps/lowcode/:appId/studio` 路由。

**后端 case**：

- S07-1 `AppPagesController`（**设计态 v1**）：`GET/POST/PUT/DELETE /api/v1/lowcode/apps/{id}/pages`；批量排序 `POST .../pages/reorder`。
- S07-2 `AppVariablesController`：`GET/POST/PUT/DELETE /api/v1/lowcode/apps/{id}/variables`；批量导入。
- S07-3 `AppResourcesController`：聚合查询 `GET /api/v1/lowcode/apps/{id}/resources`；按类型过滤 + 分页 + 远程搜索；含**提示词模板**、**插件**、**记忆库**等全部资源。
- S07-4 **【新增】`AppTemplatesController`**：模板 CRUD 与共享市场。
- S07-5 .http 全覆盖；xUnit 多租户隔离测试。

**验证**：Playwright E2E 覆盖"新建应用 → 3 页面 → 8 变量 → 5 个 Tab 检索 → 资源拖拽 → 保存恢复 → FAQ 检索"完整链路。

### M08 完整 runtime-web 渲染器 + preview-web 独立预览 app

**docx 概念覆盖**：§九 Runtime + §10.4.1 完整 API + **【新增】§10.6 Preview HMR + §七标准化协议唯一桥梁强约束 + §9.3 preview-web 独立 app**。

**前端 case**：

- C08-1 `lowcode-runtime-web/src/renderer/`：`<RuntimeRenderer schema appId pageId mode />` 递归渲染 ComponentSchema；按 ComponentMeta.runtimeRenderer 解析；mode = `production | preview | debug` 三档。
- C08-2 多端类型分发：`web` 走 `lowcode-components-web`，`mini_program` 走 `lowcode-components-mini`（M15），`hybrid` 自动选择。
- C08-3 `lowcode-runtime-web/src/context/`：`RuntimeContext` 注入 7 个适配器（workflow/chatflow/asset/session/trigger/webview-policy/plugin）+ 状态管理（Zustand）+ TanStack Query 客户端。
- C08-4 状态补丁系统（基于 zustand + immer）：scope=page/app/component；与 M02 表达式依赖追踪联动重算 binding。
- C08-5 事件分发：onClick/onChange/onSubmit/onUploadSuccess/onPageLoad/onItemClick/onLoad/onError 8+ 完整事件类型；**统一经 dispatch（强约束）**——禁止任何组件内直接 `fetch('/api/workflow_api/...')` 或类似行为，CI 静态扫描守门。
- C08-6 Workflow.loading/Workflow.error 自动绑定（loadingTargets 显示骨架屏，errorTargets 显示错误态）。
- C08-7 生命周期：beforePageLoad/afterPageLoad/beforePageUnload；错误边界（React ErrorBoundary）。
- C08-8 性能埋点：组件渲染时长 / 事件处理时长 / 工作流调用时长（OTel front-end SDK）。
- C08-9 **【新增】新建 [src/frontend/apps/lowcode-preview-web](src/frontend/apps)（端口 5184）**：调试预览壳独立 app，与 runtime-web 共用渲染包但独立部署；包含：
  - **HMR 模式**：基于 SignalR `/hubs/lowcode-debug` 订阅 `schemaDiff` 事件，毫秒级热更新；不需重新发布。
  - **扫码移动端预览**：生成二维码，手机扫码进入移动端预览模式（h5）。
  - **iframe 嵌套预览**：在 Studio 右上角"预览"抽屉以 iframe 嵌入 preview-web。
  - **设备模拟**：iPhone / iPad / 桌面 多分辨率切换。
  - **状态注入**：Studio 直接注入 mock 变量值进行预览。

**后端 case**：

- S08-1 `RuntimeSchemaController`（**运行时 runtime 前缀**）：`GET /api/runtime/apps/{appId}/schema`、`GET /api/runtime/apps/{appId}/versions/{versionId}/schema`（docx §10.4.1）。
- S08-2 `RuntimePagesService`：合并 AppDefinition + 当前生效版本；支持 published/draft/preview/canary 四档版本切换。
- S08-3 **【新增】`LowCodePreviewHub`（SignalR）**：监听设计态 autosave 事件 → 推送 schemaDiff 到 preview 客户端；按 appId 划分 connection group。
- S08-4 `Atlas.AppHost` 注册新控制器；.http 覆盖。

**验证**：Playwright E2E 覆盖"打开 → 输入 → 点按钮 → 工作流调用 → Markdown 回填 → 错误重试"+ HMR 推送测试 + 二维码扫码移动端预览。

### M09 完整 Workflow 适配器

**docx 概念覆盖**：§8.3 模式 A + 模式 B + §10.5 协议翻译 + §B05/B06 批量异步 + **【新增】§六.7 两种编排哲学 + §10.8 超时熔断降级**。

**前端 case**：

- C09-1 `lowcode-workflow-adapter/src/`：`invokeWorkflow(id, inputs)` 同步、`invokeWorkflowAsync(id, inputs)` 异步、`invokeBatchWorkflow(id, inputArray)` 批量；返回 `outputs / traceId / status / loading / error`。
- C09-2 `action-runtime` 注册 `call_workflow`：执行 `inputMapping` → 调适配器 → 应用 `outputMapping`（Array→下拉/列表，Object→表单，Image→图片组件，String→Markdown/Text）。
- C09-3 模式 A 完整闭环：表单值采集 → 事件触发 → workflow 调用 → Markdown/Image/List/Video 回填。
- C09-4 模式 B 完整闭环：下拉/单选/列表组件不写死选项 → 数据源声明指向 workflow → 触发动作驱动数据源更新 → 自动渲染。
- C09-5 数据源绑定与触发动作完全解耦（`DataSourceBinding` vs `Action.refreshDataSource`）。
- C09-6 Studio 内"事件 → 调用工作流"配置面板：选择已发布工作流 + 入参映射 UI + 输出映射 UI + loadingTargets/errorTargets 选择器 + trace 开关 + **【新增】弹性策略面板**（超时秒数 / 重试次数 / 退避策略 select / 熔断阈值 / 降级目标）。
- C09-7 与现有 `@coze-workflow/playground` / DAG 工作流引擎对齐节点目录与 IO Schema。
- C09-8 **【新增】两种编排哲学切换**：在 Studio 工作流配置面板提供"模型自决调工具"vs"显式节点顺序"切换；模型自决模式生成 LLM + Agent + Tool 的隐式 DAG，显式模式生成完整节点链；两种模式均能落到现有 DAG 引擎。
- C09-9 **【新增】lowcode-workflow-adapter/src/resilience/**：超时（默认 30s 可配）/ 重试（默认 3 次指数退避）/ 熔断（5 失败 / 60s 窗口）/ 降级（fallback workflow id 或 静态值）；详见 [docs/lowcode-resilience-spec.md](docs/lowcode-resilience-spec.md)。

**后端 case**：

- S09-1 `RuntimeWorkflowsController`（**运行时 runtime 前缀**）：`POST /api/runtime/workflows/{id}:invoke`（同步）、`POST /api/runtime/workflows/{id}:invoke-async`（异步，返回 jobId）、`POST /api/runtime/workflows/{id}:invoke-batch`（批量）；内部统一桥到 DAG 引擎 `api/v2/workflows/{id}/run`。
- S09-2 `RuntimeAsyncJobsController`：`GET /api/runtime/async-jobs/{jobId}`、`POST /api/runtime/async-jobs/{jobId}:cancel`。
- S09-3 接受 `appId/pageId/componentId/eventName/inputs/stateSnapshot/versionId`；返回 `outputs/traceId/statePatches/messages/errors`（docx §10.4.3）。
- S09-4 **【新增】服务端弹性中间件**：Polly 集成；按租户/工作流粒度配置；超时/重试/熔断指标进 OTel；降级路由配置。
- S09-5 .http + xUnit 覆盖映射、错误、超时、并发、重试、批量、异步、熔断、降级。
- S09-6 [docs/lowcode-binding-matrix.md](docs/lowcode-binding-matrix.md) 完整列出模式 A/B 的所有 binding 黄金样本（≥ 20 个用例）。
- S09-7 [docs/lowcode-resilience-spec.md](docs/lowcode-resilience-spec.md)：完整弹性策略契约（默认值 / 可配范围 / 配额耗尽降级 / 灰度策略）。

**验证**：Playwright E2E 覆盖模式 A + 模式 B；批量与异步压测脚本；熔断触发场景；降级回退场景；模型自决 vs 显式节点切换。

### M10 完整 Asset 适配器（含模式 C）

**docx 概念覆盖**：§8.3 模式 C + §10.5 File/Image 转换 + §10.8 文件 URL 生命周期。

**前端 case**：

- C10-1 `lowcode-asset-adapter/src/`：`prepareUpload(file, opts)` → `completeUpload(token, blob)` 两阶段；统一返回 `{ fileHandle, url, contentType, size, imageId? }`。
- C10-2 `FileUpload` / `ImageUpload` / `Video` / `Image` / `AiCard` 全部接入；禁止把 `File` 对象直接塞 workflow 入参。
- C10-3 上传进度 + 断点续传 + 重试 + 取消；多文件并发；超大文件分片。
- C10-4 mime 白名单（图像/视频/PDF/Office）+ 大小校验 + 内容预览。
- C10-5 模式 C 完整闭环：图片上传 → workflow 调用（OCR/图像处理/视频抽帧）→ Markdown/Image/Video 预览。

**后端 case**：

- S10-1 `RuntimeFilesController`（**运行时 runtime 前缀**）：`POST /api/runtime/files:prepare-upload`（返回 token + 直传 URL）、`POST /api/runtime/files:complete-upload`、`GET /api/runtime/files/{handle}`、`DELETE /api/runtime/files/{handle}`。
- S10-2 复用 `IFileStorageService`；增强为支持分片、断点、签名 URL。
- S10-3 `LowCodeAssetGcJob`（Hangfire）：未在 binding 引用的文件 7 天 GC；删除前快照备份。
- S10-4 等保 2.0：每次上传/下载/删除审计；mime/大小服务端二次校验；私有桶。
- S10-5 .http 覆盖图像/视频/PDF/Office/超大文件。

**验证**：Playwright E2E 完整模式 C；GC 任务测试；并发上传测试。

### M11 完整 Chatflow + Session 适配器（含中断/恢复/插入 + 有状态运行 + 消息日志统一）

**docx 概念覆盖**：§8.3 模式 D + §10.5 Chatflow 流式 + 多会话 + §A17 消息日志 + **【新增】§五用户中断/继续/插入输入 + §六.7 有状态运行 + §A17 消息日志+执行链路统一视图**。

**前端 case**：

- C11-1 `lowcode-chatflow-adapter/src/sse/`：基于 `fetch` + `ReadableStream` + EventStream parser 实现 `streamChat(chatflowId, sessionId, input, abortSignal)`；返回 `AsyncIterable<ChatChunk>`。
- C11-2 处理 4 类事件：`tool_call`（函数调用气泡）、`message`（流式文本/Markdown 增量）、`error`（错误恢复）、`final`（结束信号 + outputs）。
- C11-3 `AiChat` 组件完整：消息流式渲染（增量 markdown 渲染）+ 工具调用气泡（折叠/展开/重试）+ 历史回放 + 滚动到底 + 中断/继续 + 复制 + 反馈点赞。
- C11-4 **【新增】用户中断/继续/插入输入**：`pauseChat(sessionId)` / `resumeChat(sessionId)` / `injectMessage(sessionId, message)`；中断后会话状态保留，可在任意位置插入用户消息再继续。
- C11-5 表单值能写入 chatflow 上下文（系统变量注入）+ 单独触发 workflow 回到聊天旁路。
- C11-6 `lowcode-session-adapter/src/`：`listSessions / createSession / switchSession / clearHistory / pinSession / archiveSession`。
- C11-7 Studio 顶部"会话管理"抽屉；运行时多会话切换 UI。
- C11-8 模式 D 完整闭环：左侧 AI 对话 + 右侧筛选条件表单 + 单独按钮触发 workflow → 结果回到聊天旁路。
- C11-9 **【新增】"有状态运行"声明**：chatflow / session / conversation / message / trigger 级状态在 ChatflowAdapter 与 SessionAdapter 内统一持久化，与 M20 节点级有状态绑定对齐；不允许把状态依赖塞入 page-scope 临时变量。

**后端 case**：

- S11-1 `RuntimeChatflowsController`（**运行时 runtime 前缀**）：`POST /api/runtime/chatflows/{id}:invoke`（SSE/HTTP2，docx §10.4.1）；底层桥接现有 `CozeWorkflowCompatController` 的 chatflow 链路并**完整修复 fallback**。新增 `POST /api/runtime/chatflows/sessions/{sessionId}:pause`、`:resume`、`:inject`。
- S11-2 `RuntimeSessionsController`：`GET/POST/DELETE /api/runtime/sessions`、`POST /api/runtime/sessions/{id}/clear`、`POST /api/runtime/sessions/{id}/pin`、`POST /api/runtime/sessions/{id}/archive`。
- S11-3 在 [docs/coze-api-gap.md](docs/coze-api-gap.md) 把 chatflow 流式与 list_spans 从 fallback 改为 OK。
- S11-4 [docs/lowcode-runtime-spec.md](docs/lowcode-runtime-spec.md) 新增「Chatflow SSE 协议 + 中断/恢复/插入」章节。
- S11-5 **【新增】消息日志 + 执行链路统一视图后端**：`RuntimeMessageLogService` 聚合 chatflow 消息 + workflow trace + agent 调用 + 工具调用为统一 timeline；`GET /api/runtime/message-log?sessionId=&workflowId=&agentId=&from=&to=`；详见 [docs/lowcode-message-log-spec.md](docs/lowcode-message-log-spec.md)。

**验证**：Playwright 录制流式 chat 完整链路；多会话切换；中断/恢复/插入完整脚本；有状态运行跨会话验证。

### M12 完整 Trigger + Webview Policy 适配器

**docx 概念覆盖**：§6.5 触发器节点 + §U20 应用触发器 + §10.8 外链白名单 + §U17 配置外链域名。

**前端 case**：

- C12-1 `lowcode-trigger-adapter/src/`：`upsertTrigger / listTriggers / deleteTrigger / pauseTrigger / resumeTrigger`；CRON + 事件 + Webhook 三种触发类型。
- C12-2 `lowcode-webview-policy-adapter/src/`：`addDomain / verifyDomain / listDomains / removeDomain`；DNS TXT / 文件验证两种归属证明。
- C12-3 Studio 顶部"触发器管理"抽屉；CRON 可视化构建器（基于 Semi `Cron`）；触发历史日志面板。
- C12-4 Studio 顶部"外链域名"管理面板；添加 / 验证 / 吊销 / 审计日志。
- C12-5 运行时跳转 `open_external_link` 自动校验白名单（拒绝未授权域）。

**后端 case**：

- S12-1 `RuntimeTriggersController`（**运行时 runtime 前缀**）：`GET/POST/PUT/DELETE /api/runtime/triggers`；存 `LowCodeTrigger` 聚合；Hangfire 定时任务调度；事件总线接入。
- S12-2 `RuntimeWebviewDomainsController`：`POST /api/runtime/webview-domains`、`POST /api/runtime/webview-domains/{id}:verify`、`GET /api/runtime/webview-domains`、`DELETE /api/runtime/webview-domains/{id}`。
- S12-3 DAG 节点目录补齐 `TriggerUpsert(34)` / `TriggerRead(35)` / `TriggerDelete(36)` 完整执行器；更新 [BuiltInWorkflowNodeDeclarations.cs](src/backend/Atlas.Infrastructure/Services/AiPlatform/WorkflowEngine/BuiltInWorkflowNodeDeclarations.cs)。
- S12-4 等保 2.0：白名单变更全量审计；触发器执行审计；CRON 表达式服务端二次校验。

**验证**：CRON 真实跑通（30s 测试触发器）；外链白名单拒绝场景；三节点 .http + 单测。

### M13 完整调试台 + 统一事件分发 + Trace 6 维检索 + 消息日志统一

**docx 概念覆盖**：§10.4.2/10.4.3 dispatch + §10.6 Debug + §10.8 调试日志脱敏 + **【新增】Trace 6 维检索 + 消息日志+执行链路统一视图前端**。

**前端 case**：

- C13-1 `lowcode-debug-client/src/panel/`：调试抽屉，**6 维度检索**——traceId / 页面 / 组件 / **时间范围** / **错误类型** / **用户** / **租户**。
- C13-2 时间线视图：组件 → 事件 → 动作链 → 工作流调用 → 输出 → state_patches → 错误，每个节点可展开 JSON 树。
- C13-3 错误链路高亮：堆栈、表达式错误位置、绑定路径、修复建议。
- C13-4 traceId 复制 / 分享 / 二维码（移动端预览跳转）；"重放"按钮（开发模式）。
- C13-5 与 `runtime-web` 集成：开发模式默认开启 Debug 抽屉；生产模式按租户配置启用。
- C13-6 性能视图：组件渲染时长、事件耗时、工作流时长、SSE 帧速率。
- C13-7 **【新增】"消息日志 + 执行链路"统一视图**：在 Debug 抽屉新增"运行监控"Tab，按时间线统一展示 chatflow 消息 + workflow trace + agent 调用 + 工具调用 + dispatch 事件；与 M11 后端 `RuntimeMessageLogService` 联动；详见 [docs/lowcode-message-log-spec.md](docs/lowcode-message-log-spec.md)。

**后端 case**：

- S13-1 **`RuntimeEventsController`**（**运行时 runtime 前缀**）：`POST /api/runtime/events/dispatch`（docx §10.4.2/10.4.3 请求与响应原样落地）；统一处理事件 → 解析 ActionSchema → 执行动作链 → 调用 Adapter → 收集 statePatches → 返回。
- S13-2 `RuntimeTraceService`：每次 dispatch 生成完整 `RuntimeTrace`（含子 spans：dispatcher.start / action.invoke / workflow.invoke / chatflow.stream / asset.upload / state.patch / error）；`GET /api/runtime/traces/{traceId}` 返回完整链路；**新增 6 维度查询参数**：`?page=&component=&from=&to=&errorType=&userId=&tenantId=`。
- S13-3 OpenTelemetry 全链路 instrumentation：metric（dispatch_latency / workflow_latency / error_count）、trace（spans 同上）、log（关键事件）。
- S13-4 调试日志脱敏：mask 表达式中的密钥/token/手机号/邮箱（基于规则 + 自定义脱敏策略）。
- S13-5 在 [docs/coze-api-gap.md](docs/coze-api-gap.md) 把 `list_spans` 从 fallback 改为 OK。
- S13-6 [docs/lowcode-runtime-spec.md](docs/lowcode-runtime-spec.md) 写明 dispatch 协议契约（请求/响应/错误码/SLA）。
- S13-7 [docs/lowcode-message-log-spec.md](docs/lowcode-message-log-spec.md)：消息日志统一查询契约 + 时间线模型 + 跨域聚合规则。

**验证**：人工跑模式 A/B/C/D 四种链路，调试面板看到完整 trace；6 维检索全覆盖；消息日志统一视图测试；OTel 数据导出验证。

### M14 完整版本管理 + 资源引用一致性（端点双套校准）

**docx 概念覆盖**：§10.6 Version + §10.8 资源引用 + §U15 应用版本管理 + **【新增】§10.4.1 端点双套校准 + §U16 UI Builder FAQ**。

**前端 case**：

- C14-1 `lowcode-versioning-client/src/timeline/`：版本时间线（按时间倒序）+ 版本备注 + 创建者 + 关联资源版本。
- C14-2 `lowcode-versioning-client/src/diff/`：JSON diff 视图（schema 字段级 + 组件级 + binding/event 级）；可视化 diff（Studio 模式直接红绿对比）。
- C14-3 `lowcode-versioning-client/src/rollback/`：回退确认弹窗 + 影响评估（哪些已发布产物会受影响 + 哪些资源版本不兼容）+ 二次确认 + 审计。
- C14-4 资源引用反查面板：选中工作流/变量/触发器/数据源/插件/提示词模板 → 看见所有引用的应用/页面/组件。
- C14-5 删除资源时弹出阻断提示 + 引用列表 + 推荐操作。
- C14-6 **【新增】UI Builder FAQ 内置面板**：在 lowcode-studio-web 顶栏增加"FAQ"按钮（与 M07 集成）；本地 FAQ 库 + 远程检索 + 一键定位。

**后端 case**：

- S14-1 **【端点双套校准】**：
  - **设计态 v1**（PlatformHost）：`GET /api/v1/lowcode/apps/{id}/versions`、`POST /api/v1/lowcode/apps/{id}/versions`、`GET /api/v1/lowcode/apps/{id}/versions/{ver}/diff/{ver2}`、`POST /api/v1/lowcode/apps/{id}/versions/{ver}/rollback`（设计态自助管理）。
  - **运行时 runtime**（AppHost）：`POST /api/runtime/versions/archive`（将当前生效版本归档）、`POST /api/runtime/versions/{versionId}:rollback`（运行时一键回滚到指定版本，影响所有正在跑的实例）。
  - 两套不混用，docx §10.4.1 原样落地 runtime 双端点。
- S14-2 `AppVersionArchive` 完整聚合：schema 快照 + 依赖资源版本（workflow versions、chatflow versions、knowledge versions、database snapshots、variable snapshots、**plugin versions**、**prompt template versions**）+ 构建产物元数据 + 发布时间 + 备注 + 操作者 + 审计。
- S14-3 `IResourceReferenceGuardService`：删除资源前检查 `AppVersionArchive` 与 `AppDefinition.draft` 引用；批量删除阻断；强制删除需高权限 + 审计。
- S14-4 `IResourceReferenceIndex`：异步索引 binding 中引用的资源；增量更新；查询接口 `GET /api/v1/lowcode/resources/{type}/{id}/references`。
- S14-5 **【新增】`AppFaqController`**：UI Builder FAQ CRUD 与全文检索（PlatformHost / 设计态）。

**验证**：Playwright E2E 覆盖"创建 v1 → 修改 → v2 → diff → rollback → 资源删除阻断 → FAQ 检索"；运行时 archive/rollback 全链路。

### M15 完整多端运行时（Taro 完整工程，独立 app）

**docx 概念覆盖**：§十二第 5 条 + §十一第 8 项 Taro。

**前端 case**：

- C15-1 `lowcode-runtime-mini/src/`：完整 Taro 工程；与 `lowcode-runtime-web` 共用 RuntimeContext 抽象。
- C15-2 `lowcode-components-mini/src/components/`：30+ 组件全部 mini 实现（与 web 双实现）；遵守 Taro 跨端约束。
- C15-3 新建 [src/frontend/apps/lowcode-mini-host](src/frontend/apps)（端口 5187）：完整 Taro 应用（微信小程序 / 抖音小程序 / H5 三端），跑通完整 30+ 组件 + 4 种模式（A/B/C/D）+ AI 原生组件。
- C15-4 多端差异隔离：主 Schema 不污染；样式适配在 components 层；事件适配在 runtime 层。
- C15-5 多端 Schema 兼容性测试：同一 AppSchema 在 web + 三种 mini 端跑通。

**后端 case**：

- S15-1 `RuntimeSchemaController` 新增 `?renderer=web|mini-wx|mini-douyin|h5` 参数，按渲染器返回组件能力差异说明（含降级策略）。
- S15-2 `LowCodeRendererCapabilityService`：维护各渲染器的组件支持度矩阵；不支持时返回降级建议。

**验证**：`pnpm run build:lowcode-mini-host` 通过；微信开发者工具 + 抖音开发者工具加载通过；多端 E2E 脚本。

### M16 完整 Yjs + y-websocket 协同编辑

**docx 概念覆盖**：§九 Studio 协同 + §U23 多人协作 + §十一第 9 项 Yjs。

**前端 case**：

- C16-1 `lowcode-collab-yjs/src/doc/`：基于 `yjs` 的 AppSchema CRDT 文档；自定义 Y.Map / Y.Array 适配 ComponentSchema 嵌套结构。
- C16-2 `lowcode-collab-yjs/src/awareness/`：多人光标 + 选区 + 当前选中组件高亮（基于 y-protocols/awareness）。
- C16-3 `lowcode-collab-yjs/src/lock/`：组件级操作锁（同一组件同一时间只允许一人编辑属性）；锁超时自动释放。
- C16-4 `lowcode-collab-yjs/src/offline/`：离线编辑（IndexedDB persistence）+ 重连合并 + 冲突可视化。
- C16-5 `lowcode-collab-yjs/src/history/`：协同历史回放（按用户 / 按时间）。
- C16-6 与 `lowcode-editor-canvas` `lowcode-editor-inspector` 集成：撤销/重做支持本地与协同两套栈互斥切换。
- C16-7 演示 5 个浏览器同时编辑 100 组件页面互不冲突。

**后端 case**：

- S16-1 `LowCodeCollabHub`：SignalR + y-websocket bridge；按 appId 划分 room；权限校验。
- S16-2 离线快照：每 10 分钟将 Yjs Doc 落 `AppVersionArchive`（系统快照，与用户主动版本区分）。
- S16-3 [docs/lowcode-collab-spec.md](docs/lowcode-collab-spec.md)：协同协议、CRDT 结构、冲突解决策略、性能指标。

**验证**：5 浏览器并发 E2E 测试；网络抖动模拟；协同延迟 < 200ms（局域网）；冲突合并不丢稿。

### M17 完整发布 + Web SDK + 外链白名单

**docx 概念覆盖**：§10.7 三类发布产物 + §U21 发布为 Web SDK + §U22 Web SDK 文档 + §10.4.1 webview-domains:verify。

**前端 case**：

- C17-1 `lowcode-web-sdk/src/`：`window.AtlasLowcode.mount({ container, appId, version, initialState, theme, onEvent })` 完整 API（对齐 docx §10.7 代码块）+ `unmount()` + `update()` + `getState()`。
- C17-2 SDK 用 rsbuild library 模式打包（UMD / ESM 双输出）；CDN 与 npm 双发布。
- C17-3 新建 [src/frontend/apps/lowcode-sdk-playground](src/frontend/apps)（端口 5186）：演示三种嵌入方式（`<script>` / npm import / iframe）。
- C17-4 Studio 顶部"发布"按钮：选择产物类型（Hosted App / Embedded SDK / Preview Artifact）+ 版本选择 + 域名白名单选择 + 主题 + 预览 + 一键发布。
- C17-5 Hosted App：分配独立 URL（`https://apps.atlas.local/{appId}`）+ 域名 CNAME 配置指引。
- C17-6 Embedded SDK：生成 `<script>` 嵌入代码 + 沙箱配置 + CSP 指引。
- C17-7 Preview Artifact：仅内部调试可见 + 二维码 + 移动端预览（与 M08 preview-web 联动）。
- C17-8 外链域名白名单完整 UI（含 DNS TXT / 文件验证）。

**后端 case**：

- S17-1 `AppPublishController`（PlatformHost / 设计态 v1）：`POST /api/v1/lowcode/apps/{id}/publish/web-sdk`、`/publish/hosted`、`/publish/preview`、`GET /api/v1/lowcode/apps/{id}/artifacts`、`POST /api/v1/lowcode/apps/{id}/publish/rollback`；同时在 AppHost 提供运行时只读端点 `GET /api/runtime/publish/{appId}/artifacts`。
- S17-2 `IAppPublishService`：产物打包（JS/CSS/Schema 一体）→ MinIO 对象存储 → CDN 刷新；产物指纹（SHA256）+ 版本 + 渲染器矩阵绑定。
- S17-3 `RuntimeWebviewDomainsController`：`POST /api/runtime/webview-domains:verify`（DNS TXT / HTTP 文件两种）；与 M12 共享 `LowCodeWebviewDomain` 聚合。
- S17-4 等保 2.0：发布全链路审计；产物指纹与版本绑定；SDK 加载来源校验；CSP 严格策略。
- S17-5 [docs/lowcode-publish-spec.md](docs/lowcode-publish-spec.md)：三类产物完整发布流程 + SDK API 契约 + 安全配置。

**验证**：sdk-playground 三种嵌入方式跑通；hosted 独立域名跑通；预览二维码跑通；外链白名单拒绝场景。

### M18 完整智能体层 + 插件完整域

**docx 概念覆盖**：附录 A01-A17 全 17 篇 + §四主线 5 大块 + **【新增】三栏 IDE 形态 + 人设独立配置 + 提示词模板库跨域 + @ 快速引用 + 模型跨层复用 + 渠道适配运行实体 + 长期记忆 vs 记忆库 + 插件完整域**。

**前端 case**：

- C18-1 增强 [module-studio-react/src/assistant/](src/frontend/packages/module-studio-react)：自然语言创建（A01）+ AI 创建（A01）双入口；创建后进入同一编排界面（**非黑盒**）。
- C18-2 **【新增】三栏 IDE 形态**（对应 docx §四 1）：左侧"人设与回复逻辑"（**人设独立配置项**：角色 / 口吻 / 边界 / 兜底） + 中间技能面板 + 右侧预览与调试。
- C18-3 提示词体系（A04-A06）：Jinja + Markdown 模板编辑器（基于 Monaco）+ **@ 快速引用菜单**（变量 / 技能 / 工作流 / 知识库 / 提示词模板）+ 模板库。
- C18-4 **【新增】"提示词模板库"跨域资源**：作为独立资源类型（与变量/工作流并列）；可在智能体 + 工作流 LLM 节点（M20）跨层复用；版本管理；分享。
- C18-5 模型设置（A16）：模型选择 + 参数调优 + **跨智能体/工作流 LLM 节点复用同一模型配置池**。
- C18-6 技能扩展面（A08-A14）：插件 / 工作流 / 知识库 / 数据库 / 变量 / **长期记忆**（A12，个性化用户画像）/ **记忆库**（A13，会话内短期记忆）— **两者独立可见 + 区分文档**。
- C18-7 **【新增】插件完整域**：
  - 插件市场（浏览 / 检索 / 评分 / 安装）
  - 插件创建（OpenAPI 导入 / 手动定义工具 / 测试调用）
  - 插件调用（智能体内调用 + 与工作流 N10 节点共享插件库）
  - 插件授权（OAuth / API Key / 租户级权限）
  - 插件计量（调用次数 / 配额 / 计费）
  - 插件发布（私有 / 公开 / 团队）
  - 详见 [docs/lowcode-plugin-spec.md](docs/lowcode-plugin-spec.md)
- C18-8 预览与调试（A07）：调试台带执行链路观察（与 M13 调试台共用基础设施）；输入/节点链路/调用结果/错误原因/最终回复 5 视图。
- C18-9 消息日志（A17）：完整链路观察 + 检索 + 导出（与 M13 消息日志统一视图联动）。
- C18-10 多渠道发布（飞书 / 微信 / 抖音 / 豆包）+ 渠道适配层 UI；**【新增】"渠道适配运行实体"概念**：发布生成可调度的运行实体（含模型 / 技能 / 记忆 / 提示词 / token 配额绑定），非静态配置。
- C18-11 i18n 中英完整覆盖。

**后端 case**：

- S18-1 增强 `AgentCommandService` / `AgentQueryService`：自然语言/AI 创建接口；提示词/模型/技能/记忆/调试/消息日志；多渠道发布。
- S18-2 渠道适配层 `IAgentChannelAdapter`（飞书 / 微信 / 抖音 / 豆包 4 实现）；OAuth + Webhook + 推送回调；**渠道运行实体注册中心**。
- S18-3 `AgentMessageLogController`：`GET /api/v1/agents/{id}/messages`（含完整执行链路；与 M13 `RuntimeMessageLogService` 共享聚合层）。
- S18-4 **【新增】插件域后端**：
  - [Atlas.Domain.LowCodePlugin](src/backend/Atlas.Domain.LowCodePlugin)：`PluginDefinition` / `PluginVersion` / `PluginAuthorization` / `PluginUsage` 4 个 `TenantEntity`。
  - `PluginsController`（**设计态 v1**）：CRUD + 市场 + 授权 + 调用统计。
  - `PluginAdapter`（前端 lowcode-plugin-adapter）+ 运行时 `POST /api/runtime/plugins/{id}:invoke`。
  - 与现有工作流 N10 插件节点共享 `PluginRegistry`。
- S18-5 **【新增】`PromptTemplatesController`**：提示词模板 CRUD 与跨域引用查询；`GET /api/v1/lowcode/prompt-templates`、`POST /api/v1/lowcode/prompt-templates`。
- S18-6 **【新增】`AgentRuntimeEntityService`**：渠道发布时构建运行实体 → 注册到调度中心 → 渠道路由表更新。
- S18-7 [docs/lowcode-assistant-spec.md](docs/lowcode-assistant-spec.md)：assistant_coze 17 篇全量映射到 Atlas 实现的章节对照表 + 三栏 IDE 形态 + 人设/提示词模板/记忆双层/插件/渠道运行实体完整契约。
- S18-8 [docs/lowcode-plugin-spec.md](docs/lowcode-plugin-spec.md)：插件全域契约。

**验证**：Playwright E2E 覆盖"AI 创建智能体 → 配置人设 → @ 引用提示词模板 → 配置插件 → 配置长期记忆 + 记忆库 → 调试 → 发布到飞书 → 消息日志检索"完整链路。

### M19 完整工作流父级工程能力

**docx 概念覆盖**：附录 B03-B08 全 6 篇 + §五工作流父级 + **【新增】AI 生成双模式 + 批量 3 输入源 + 封装/解散 IO 推断**。

**前端 case**：

- C19-1 增强 `@coze-workflow/playground`：AI 生成工作流入口（B07）——**【新增】双模式**：
  - **完全自动**：自然语言 → LLM 直接生成完整 DAG → 自动连线 → 一键预览运行
  - **半自动**：自然语言 → LLM 生成节点列表 → 人工确认 / 调整 / 连线 → 再生成绑定
- C19-2 批量执行入口（B05）：**【新增】3 种输入源**——CSV 上传 / JSON 上传 / **数据库查询**（接现有 AI 数据库节点）；批量执行 → 结果导出 → 失败重试。
- C19-3 异步执行入口（B06）：异步任务列表 + 进度 + 取消 + 结果查询 + 通知回调。
- C19-4 **【新增】封装/解散子工作流（B08）+ IO 推断**：
  - 封装：选中节点子集 → **自动从节点 IO 推断子流程接口**（输入参数 = 子集外部依赖；输出 = 子集对外发出）→ 生成子工作流；保留节点位置元数据用于解散。
  - 解散：反向操作 → **保留入参绑定关系**（外部父流程对子流程的入参映射自动还原到内部节点）。
- C19-5 限制治理 UI（B03）：工作流数量上限 / 节点上限 / 超时 / QPS 配额查看 + 告警。
- C19-6 FAQ 面板（B04）：内置常见问题与解决方案（与 M14 UI Builder FAQ 区分）。

**后端 case**：

- S19-1 增强 `DagWorkflowCommandService`：AI 生成（接 LLM + 双模式）；封装/解散子流程（IO 推断算法）；批量任务调度。
- S19-2 `DagWorkflowBatchController`：`POST /api/v2/workflows/{id}/batch`（CSV/JSON/**数据库查询** 三种输入源，Hangfire 调度，进度回调）。
- S19-3 `DagWorkflowAsyncController`：`POST /api/v2/workflows/{id}/async`、`GET /api/v2/workflows/async-jobs/{jobId}`、`POST .../cancel`、`POST .../webhook`。
- S19-4 `DagWorkflowCompositionController`：`POST /api/v2/workflows/{id}/compose`、`POST /api/v2/workflows/{id}/decompose`；服务端 IO 推断算法（拓扑分析 + 边界节点识别）。
- S19-5 `WorkflowQuotaService`：租户级配额 + 实时统计 + 告警；接 Atlas Alert 模块。
- S19-6 [docs/workflow-editor-validation-matrix.md](docs/workflow-editor-validation-matrix.md) 新增 4 项工程能力验证矩阵。
- S19-7 [docs/lowcode-resilience-spec.md](docs/lowcode-resilience-spec.md) 补充配额耗尽降级策略。

**验证**：AI 生成双模式 → 编辑 → 批量 3 输入源 → 异步 → 封装解散 全链路 E2E；配额触发告警测试。

### M20 完整工作流节点 49 全集 + 两种编排哲学 + 有状态运行

**docx 概念覆盖**：附录 C 全 49 节点 + Atlas 扩展 + **【新增】§六.7 两种编排哲学的运行时切换 + §六.7 有状态工作流节点级绑定**。

**前端 case**：

- C20-1 `@coze-workflow/playground` 节点面板新增分组与节点（按 docx N01-N49 全集对齐）：
  - 图像类：N44 ImageGeneration、N45 Canvas、N46 ImagePlugin
  - 视频类：N47 VideoGeneration、N48 VideoToAudio、N49 VideoFrameExtraction
  - 上游对齐：ImageGenerate(14)、Imageflow(15)、ImageReference(16)、ImageCanvas(17)、SceneVariable(24)、SceneChat(25)、上游 LTM(26) ID 对齐
  - 拆分：Variable(11) 单节点（与 VariableAggregator 区分）
  - 触发器：TriggerUpsert(34) / TriggerRead(35) / TriggerDelete(36)
- C20-2 每个节点的属性面板表单 + i18n 中英 + 校验规则 + 黄金样本配置。
- C20-3 节点目录 / 模板 API 同步更新。
- C20-4 **【新增】两种编排哲学切换 UI**：在工作流画布顶部添加"模式"切换器（模型自决 / 显式节点）；模型自决模式自动隐藏部分中间节点 → 显示 LLM + Tool 池配置；显式模式恢复全节点视图。
- C20-5 **【新增】节点级有状态绑定 UI**：会话/消息/触发器节点的"持久化作用域"配置（无状态 / session 级 / conversation 级 / trigger 级 / app 级）；与 M11 SessionAdapter / TriggerAdapter 联动。

**后端 case**：

- S20-1 [BuiltInWorkflowNodeDeclarations.cs](src/backend/Atlas.Infrastructure/Services/AiPlatform/WorkflowEngine/BuiltInWorkflowNodeDeclarations.cs) 注册全部新节点；ID 与 Coze 上游对齐（`coze-node-mapping.md` §1）。
- S20-2 实现节点执行器：`ImageGenerationExecutor` / `ImageCanvasExecutor` / `ImagePluginExecutor` / `VideoGenerateExecutor` / `VideoFrameExtractExecutor` / `VideoToAudioExecutor` / `SceneVariableExecutor` / `SceneChatExecutor` / `VariableExecutor`（单节点）。
- S20-3 把 Atlas `Ltm(62)` 单节点拆为 `MemoryRead(28)` / `MemoryWrite(29)` / `MemoryDelete` 三个独立节点；保留旧 ID 兼容映射；`docs/coze-node-mapping.md` 全量更新（§2 缺失表清空）。
- S20-4 每个节点 `.http` 文件 + xUnit 单测（输入/输出/错误/超时/边界）。
- S20-5 [docs/workflow-editor-validation-matrix.md](docs/workflow-editor-validation-matrix.md) 节点矩阵 100% 覆盖。
- S20-6 **【新增】DAG Executor 双哲学引擎**：
  - 显式模式：现有 `DagExecutor` 完整支持。
  - 模型自决模式：`AgenticOrchestrator` 接 LLM tool calling 协议，运行时根据模型决策动态调用 Tool 池中工具；执行轨迹仍落 trace 系统。
- S20-7 **【新增】节点级状态持久化**：`INodeStateStore`（按 session / conversation / trigger / app 作用域分库存储），各有状态节点的 executor 通过 `NodeExecutionContext.State` 访问。
- S20-8 [docs/lowcode-orchestration-spec.md](docs/lowcode-orchestration-spec.md)：两种编排哲学完整契约 + 有状态工作流节点状态作用域规范。

**验证**：节点目录 API 返回完整 49 节点；每节点 .http 通过；校验矩阵 100%；模型自决 vs 显式节点切换 E2E；有状态节点跨会话/跨触发持久化测试。

## 五、跨里程碑硬约束（含二轮深审新增）

- **i18n 双语**：每里程碑同步更新 [zh-CN.ts](src/frontend/apps/app-web/src/app/i18n/zh-CN.ts) 与 `en-US.ts`；`pnpm run i18n:check` 0 缺失。
- **契约同步**：每个新 API 同步更新 [docs/contracts.md](docs/contracts.md) + 对应 `.http` 文件 + 前端 TS 类型 + 后端 DTO；前后端类型镜像。
- **API 前缀双套强约束**：设计态 `/api/v1/lowcode/...` + 运行时 `/api/runtime/...`，禁止混用。
- **标准化协议唯一桥梁**：UI 禁止直调零散 API，必须经 dispatch；CI 静态扫描守门。
- **作用域变量隔离**：page/app/system 三作用域禁止跨作用域 setVariable；表达式引擎 + action-runtime 双层校验。
- **元数据驱动**：组件实现禁硬编码业务逻辑；ComponentMeta 注册时校验。
- **超时/重试/熔断/降级**：所有外部调用必须经 [docs/lowcode-resilience-spec.md](docs/lowcode-resilience-spec.md) 策略；默认值与可配项明确。
- **等保 2.0**：所有写接口走 `IAuditWriter`；文件 mime/大小双校验；外链域名白名单强制；调试日志脱敏；遵守 [AGENTS.md](AGENTS.md) 写接口安全基线（无 Idempotency-Key/X-CSRF-TOKEN）。
- **数据访问**：所有 Repository 实现禁止在循环内查 DB（违反将无法通过 review）；批量查询/聚合优先。
- **强类型**：前端禁 `any`/`unknown`/`eval`；后端禁反射/`dynamic`/运行时编译/表达式树（除 jsonata/Monaco LSP 既定推荐栈外）。
- **零警告**：`dotnet build` 0 错误 0 警告；`pnpm run lint` 0 警告。
- **完整性**：每里程碑必须 docx 概念零遗漏（含二轮深审 30 条细节）；进入下一里程碑前的 review checklist 包括"docx 章节覆盖率 + 二轮深审条目"项。
- **完成才宣告**：禁止"已完成""已修复"伪声明，必须执行验证命令并粘贴输出后才进入下一里程碑。

## 六、验证矩阵

- 后端：`dotnet build`（0 警告）；`dotnet test tests/Atlas.WorkflowCore.Tests`；`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"`；`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~Integration"`；对应 `.http` 手测。
- 前端：`pnpm run lint`；`pnpm run test:unit`；`pnpm run i18n:check`；`pnpm run build`；`pnpm run test:e2e:app`。
- 前端 E2E 重点场景：模式 A/B/C/D + 中断恢复插入 + 超时熔断降级 + Trace 6 维检索 + 协同 5 浏览器 + Preview HMR + 多端 + 三类发布产物 + 模型自决 vs 显式节点。
- 文档：[docs/contracts.md](docs/contracts.md)、[docs/lowcode-runtime-spec.md](docs/lowcode-runtime-spec.md)、[docs/lowcode-binding-matrix.md](docs/lowcode-binding-matrix.md)、[docs/lowcode-component-spec.md](docs/lowcode-component-spec.md)、[docs/lowcode-publish-spec.md](docs/lowcode-publish-spec.md)、[docs/lowcode-collab-spec.md](docs/lowcode-collab-spec.md)、[docs/lowcode-assistant-spec.md](docs/lowcode-assistant-spec.md)、[docs/lowcode-shortcut-spec.md](docs/lowcode-shortcut-spec.md)、[docs/lowcode-message-log-spec.md](docs/lowcode-message-log-spec.md)、[docs/lowcode-resilience-spec.md](docs/lowcode-resilience-spec.md)、[docs/lowcode-orchestration-spec.md](docs/lowcode-orchestration-spec.md)、[docs/lowcode-content-params-spec.md](docs/lowcode-content-params-spec.md)、[docs/lowcode-plugin-spec.md](docs/lowcode-plugin-spec.md)、[docs/coze-node-mapping.md](docs/coze-node-mapping.md)、[docs/workflow-editor-validation-matrix.md](docs/workflow-editor-validation-matrix.md) 全部同步。

## 七、不在本次范围内（明确边界）

- 不重写 ORM 与数据库引擎（沿用 SqlSugar + SQLite）。
- 不重做 Setup Console（沿用现状，仅在 M16 触发协同时增加 collab-hub 健康检查面板）。
- 不调整 [AGENTS.md](AGENTS.md) 写接口安全基线（沿用现行无 Idempotency-Key/X-CSRF-TOKEN 设计）。
- 不引入未在 docx §十一推荐 10 项栈中的额外前端框架（如 Vue / Solid / Svelte / Storybook / ladle 等）。
- 不要求强行还原 Coze 内部未公开实现（docx §十三明确这是工程推断，不是事实）；只复刻 docx 已确证的能力边界与协议。

## 八、概念覆盖追溯表（含二轮深审 30 条细节）

### 8.1 docx 14 章正文映射

- §一 / §二 / §三 → 总策略 + 数据流图。
- §四 assistant_coze 主线 → M18（含三栏 IDE / 人设 / 提示词模板库 / 渠道运行实体 / 长期记忆 vs 记忆库 / 插件域）。
- §五 工作流父级 → M19（含中断/恢复/插入由 M11 落实）。
- §六 49 节点 → M20（含两种编排哲学 + 有状态运行）。
- §七 UI Builder → M04-M07 + M14 + M16（含左侧 5 Tab + 投射模式 + 标准化协议唯一桥梁）。
- §八 表单-工作流绑定 → 模式 A/B = M09 / 模式 C = M10 / 模式 D = M11 / §8.4 工程配套 = M13/M14/M16/M17 / §8.5 五种一等能力 = M02/M05/M13/M14。
- §九 前端工程骨架 → M01-M17 全部新建包 + apps/lowcode-preview-web 独立 + 后端 services 4+1 → PlatformHost/AppHost 映射。
- §10.1 Schema 设计原则 / §10.2 七类 Schema → M01。
- §10.3 ComponentMeta → M06。
- §10.4 Runtime API → M08（schema）+ M09（workflows）+ M10（files）+ M11（chatflows/sessions）+ M12（triggers/webview）+ M13（events/dispatch + traces）+ M14（versions：双套）+ M17（publish）。
- §10.5 Adapter 6 类 → M09/M10/M11/M11/M12/M12 + M18 plugin 第 7 类。
- §10.6 Preview/Debug/Version → M08 preview-web 独立 + HMR + M13 debug + M14 version。
- §10.7 Web SDK 三类产物 → M17。
- §10.8 安全治理 6 条 → 外链白名单 M12+M17 / 文件生命周期 M10 / 调试日志脱敏 M13 / 超时重试熔断降级 M09+M11+M19+resilience-spec / 版本回退审计 M14 / 资源引用检查 M14。
- §十一 推荐栈 → 总策略锁定。
- §十二 落地原则 5 条 → 跨里程碑硬约束。
- §十三 / §十四 → 边界声明。

### 8.2 docx 附录映射

- 附录 A 17 篇 → M18。
- 附录 B 8 篇 → M19。
- 附录 C 49 节点 → M20。
- 附录 D 27 篇 → M06+M07+M08+M09+M10+M11+M12+M13+M14+M16+M17。

### 8.3 二轮深审 30 条细节追溯

- 1 双套 API 前缀 → 总策略 + M14 校准 + 各里程碑 S 系列。
- 2 两种编排哲学 → M19+M20+orchestration-spec。
- 3 有状态工作流 → M11+M12+M20+orchestration-spec。
- 4 Preview HMR → M08 preview-web 独立 app + LowCodePreviewHub。
- 5 设计器 5 Tab → M07。
- 6 内容参数独立机制 → M05+M06+content-params-spec。
- 7 渠道适配运行实体 → M18 + AgentRuntimeEntityService。
- 8 消息日志 + 执行链路统一视图 → M13 前端 + M11 RuntimeMessageLogService + message-log-spec。
- 9 用户中断/继续/插入输入 → M11。
- 10 长期记忆 vs 记忆库 → M18。
- 11 提示词模板库跨域资源 → M07 + M18 + PromptTemplatesController。
- 12 提示词模型跨层复用 → M18+M20。
- 13 超时重试熔断降级策略 → M09+M11+M19+resilience-spec。
- 14 Trace 6 维检索 → M13。
- 15 作用域变量隔离强约束 → M02+M03 + 跨里程碑硬约束。
- 16 完整快捷键清单 → M04+M07+shortcut-spec。
- 17 AI 原生组件特征 → M06。
- 18 元数据驱动禁硬编码 → M06 + 跨里程碑硬约束。
- 19 组件能力 6 维矩阵 → M06+component-spec。
- 20 AI 生成工作流双模式 → M19。
- 21 批量执行 3 输入源 → M19。
- 22 封装/解散 IO 推断 → M19。
- 23 标准化协议唯一桥梁 → 跨里程碑硬约束 + M08+M13。
- 24 应用层投射模式 → M07。
- 25 调试+版本是绑定系统伴随能力哲学 → M05/M06。
- 26 UI Builder FAQ 内置面板 → M07+M14。
- 27 lowcode-shortcut-spec.md → M04+M07。
- 28 lowcode-message-log-spec.md → M11+M13+M18。
- 29 lowcode-resilience-spec.md → M09+M11+M19。
- 30 lowcode-orchestration-spec.md → M19+M20；外加 lowcode-content-params-spec.md（M05/M06）+ lowcode-plugin-spec.md（M18）。
