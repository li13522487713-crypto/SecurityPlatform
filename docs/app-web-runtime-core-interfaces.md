# app-web runtime 核心接口定义（主线版）

## 1. 文档目标

本文档用于定义 `app-web` 下一阶段主线建设所需的**核心运行时协议**，作为：

- 前端 runtime 目录落地基线
- 后端 runtime / release / execution API 对齐基线
- 后续设计器、流程、审批、AI 接入的统一消费协议

这份文档不讨论页面视觉设计器，也不讨论具体组件清单，而是聚焦 **`app-web` 作为统一应用运行时内核** 所必须具备的接口层。

---

## 2. 约束前提

本方案建立在当前仓库已经明确的几条基线上：

1. **页面 Schema 唯一主线：AMIS**
2. **表达式唯一主线：CEL**
3. **运行态只消费已发布页面**
4. **运行态需区分 RuntimeContext 与 RuntimeExecution**
5. **app-web 当前已有运行时壳雏形，但仍偏 Schema Renderer**

因此，本文中的所有接口都围绕以下目标设计：

> **让页面、动作、绑定、流程、审计都运行在同一套 runtime 协议中。**

---

## 3. 设计原则

### 3.1 单一页面 DSL

- 页面结构统一由 AMIS Schema 承载
- runtime 通过 adapter 桥接页面 DSL 与平台行为
- 不新增第二套页面/表单 DSL

### 3.2 单一表达式协议

- 前后端统一以 CEL 作为表达式规范
- 前端不再扩张任意 JS 执行模型
- 前端表达式试算只作为调试/预览能力存在

### 3.3 页面声明，运行时执行

- 页面负责声明结构和行为意图
- runtime 负责真正执行跳转、绑定、提交流程、调用 AI、审计上报等行为

### 3.4 发布态与运行态分离

- runtime 仅消费已发布 release 的产物
- 草稿态不直接进入运行时
- 页面运行必须显式带 release 版本信息

### 3.5 上下文先于页面

- 页面渲染只是结果
- 统一上下文、统一动作、统一绑定，才是平台运行时的根基

---

## 4. 目录建议

建议在 `src/frontend/apps/app-web/src/runtime/` 下收敛主线协议：

```text
src/frontend/apps/app-web/src/runtime/
  context/
    runtime-context.ts
    runtime-context-store.ts

  actions/
    action-types.ts
    action-result.ts

  bindings/
    binding-types.ts

  release/
    runtime-manifest.ts
    runtime-execution.ts

  expressions/
    expression-types.ts
```

本文的接口可以直接落到这些文件中。

---

## 5. 基础类型

## 5.1 通用标识类型

```ts
export type Id = string;
export type Key = string;
export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];
export interface JsonObject {
  [key: string]: JsonValue;
}
```

## 5.2 字典类型

```ts
export type StringMap = Record<string, string>;
export type ValueMap = Record<string, unknown>;
```

## 5.3 运行模式

```ts
export type RuntimeEntryMode = 'public-runtime' | 'workspace-runtime';
export type RuntimePageMode = 'view' | 'edit' | 'create';
export type RuntimeStatus = 'ready' | 'running' | 'success' | 'failed' | 'cancelled';
```

---

## 6. RuntimeContext

## 6.1 目标

`RuntimeContext` 是整个 runtime 的第一核心对象。

它用于统一承载：

- 当前租户
- 当前应用
- 当前页面
- 当前用户
- 当前路由
- 当前记录
- 当前选中项
- 全局变量
- 运行环境与追踪信息

页面、表达式、动作、流程、审计都应基于它执行。

## 6.2 接口定义

```ts
export interface RuntimeTenantContext {
  id: Id;
  code?: string;
  name?: string;
}

export interface RuntimeProjectContext {
  id?: Id;
  code?: string;
  name?: string;
}

export interface RuntimeUserContext {
  id?: Id;
  username?: string;
  displayName?: string;
  roles: string[];
  permissions: string[];
  departmentIds?: Id[];
}

export interface RuntimeAppContext {
  id?: Id;
  appKey: Key;
  appName?: string;
  appCode?: string;
  releaseId?: Id;
  releaseVersion?: number;
}

export interface RuntimePageContext {
  id?: Id;
  pageKey: Key;
  pageName?: string;
  pageType?: string;
  title?: string;
  mode?: RuntimePageMode;
}

export interface RuntimeRouteContext {
  path: string;
  fullPath?: string;
  params: StringMap;
  query: StringMap;
}

export interface RuntimeRecordContext {
  id?: Id;
  entityKey?: Key;
  data?: ValueMap;
}

export interface RuntimeEnvironmentContext {
  entryMode: RuntimeEntryMode;
  directRuntimeMode?: boolean;
  traceId?: string;
  runtimeExecutionId?: Id;
  locale?: string;
  timezone?: string;
}

export interface RuntimeContext {
  tenant: RuntimeTenantContext;
  project?: RuntimeProjectContext;
  user: RuntimeUserContext;
  app: RuntimeAppContext;
  page: RuntimePageContext;
  route: RuntimeRouteContext;
  record?: RuntimeRecordContext;
  selection?: ValueMap[];
  global: ValueMap;
  env: RuntimeEnvironmentContext;
}
```

## 6.3 设计说明

### 为什么要显式拆成多个子对象

这样做有三个好处：

1. 对齐表达式变量域：`user.* / page.* / app.* / tenant.* / record.* / global.*`
2. 对齐后端对象模型：`RuntimeContext` 是应用级对象，不再是散落状态
3. 对齐后续治理：traceId、releaseVersion、executionId 可以自然进入上下文

### 为什么 `record` 和 `selection` 不放进 page

因为它们是**运行态数据上下文**，不是页面静态定义的一部分。

- `page` 表达“我是谁”
- `record / selection` 表达“我现在操作谁”

这两个维度必须分开。

---

## 7. RuntimeManifest

## 7.1 目标

`RuntimeManifest` 是 runtime 启动时获取的**发布态清单**。

它不是草稿文档，也不是编辑器文档，而是运行时真正消费的产物集合。

它建议包含：

- 页面 schema
- 当前 release 信息
- 当前 route 信息
- 菜单信息
- 页面生命周期定义
- 动作/绑定注册信息

## 7.2 接口定义

```ts
export interface RuntimeMenuItem {
  key: Key;
  title: string;
  pageKey?: Key;
  path?: string;
  icon?: string;
  children?: RuntimeMenuItem[];
  permissionCode?: string;
  hidden?: boolean;
}

export interface RuntimeLifecycleDefinition {
  onPageInit?: RuntimeAction[];
  onRouteChanged?: RuntimeAction[];
  beforeSubmit?: RuntimeAction[];
  afterSubmit?: RuntimeAction[];
}

export interface RuntimeManifest {
  appKey: Key;
  pageKey: Key;
  releaseId: Id;
  releaseVersion: number;
  routeId?: Id;
  schemaJson: string;
  pageType?: string;
  pageName?: string;
  title?: string;
  menu: RuntimeMenuItem[];
  lifecycle?: RuntimeLifecycleDefinition;
  actionRegistry?: Record<string, RuntimeActionDefinition>;
  bindingRegistry?: Record<string, RuntimeBinding>;
  initialContextPatch?: Partial<RuntimeContext>;
}
```

## 7.3 设计说明

### 为什么 manifest 中保留 `schemaJson: string`

因为你们现有代码里运行时接口就是按字符串拉取 schema，再由前端 JSON.parse 后渲染。

这能保证和当前实现兼容，再逐步演进到更强的 typed schema 容器。

### 为什么要有 `initialContextPatch`

有些页面会在服务端就拿到一部分上下文增强信息，例如：

- 页面标题
n- 初始 record
- feature flags
- 租户定制变量

直接由 manifest 提供 patch，会比前端二次拼装更稳。

---

## 8. RuntimeAction

## 8.1 目标

`RuntimeAction` 是 runtime 的第二核心对象。

它用于承接：

- AMIS 事件桥接
- 页面生命周期动作
- 按钮点击行为
- 表单提交后动作
- 流程/审批/AI/刷新/跳转等平台行为

### 核心原则

页面不直接承载复杂执行逻辑，页面只声明要做什么，具体怎么执行交给 runtime。

## 8.2 基础定义

```ts
export interface RuntimeActionBase<TType extends string, TInput = unknown> {
  id?: Id;
  type: TType;
  name?: string;
  label?: string;
  description?: string;
  when?: string; // CEL expression
  input?: TInput;
  continueOnError?: boolean;
}
```

## 8.3 动作枚举

```ts
export type RuntimeAction =
  | NavigateAction
  | OpenDialogAction
  | SubmitFormAction
  | CallApiAction
  | RunFlowAction
  | RunWorkflowAction
  | RunAgentAction
  | RefreshAction
  | SetVariableAction
  | BranchAction
  | ForeachAction;
```

## 8.4 具体动作类型

```ts
export interface NavigateAction extends RuntimeActionBase<'navigate', {
  pageKey: Key;
  params?: ValueMap;
  query?: ValueMap;
  replace?: boolean;
}> {}

export interface OpenDialogAction extends RuntimeActionBase<'openDialog', {
  dialogKey: Key;
  title?: string;
  payload?: ValueMap;
  width?: string | number;
}> {}

export interface SubmitFormAction extends RuntimeActionBase<'submitForm', {
  formKey?: Key;
  validateOnly?: boolean;
}> {}

export interface CallApiAction extends RuntimeActionBase<'callApi', {
  apiKey?: Key;
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  url?: string;
  pathParams?: ValueMap;
  query?: ValueMap;
  body?: unknown;
}> {}

export interface RunFlowAction extends RuntimeActionBase<'runFlow', {
  flowKey: Key;
  input?: ValueMap;
}> {}

export interface RunWorkflowAction extends RuntimeActionBase<'runWorkflow', {
  workflowKey: Key;
  input?: ValueMap;
}> {}

export interface RunAgentAction extends RuntimeActionBase<'runAgent', {
  agentKey: Key;
  input?: ValueMap;
  awaitResult?: boolean;
}> {}

export interface RefreshAction extends RuntimeActionBase<'refresh', {
  target?: string;
}> {}

export interface SetVariableAction extends RuntimeActionBase<'setVar', {
  scope?: 'global' | 'page' | 'local';
  name: string;
  value: unknown;
}> {}

export interface BranchAction extends RuntimeActionBase<'branch', {
  condition: string; // CEL expression
  then: RuntimeAction[];
  else?: RuntimeAction[];
}> {}

export interface ForeachAction extends RuntimeActionBase<'foreach', {
  items: string; // expression
  itemName?: string;
  actions: RuntimeAction[];
}> {}
```

## 8.5 ActionDefinition

```ts
export interface RuntimeActionDefinition {
  key: Key;
  name?: string;
  actions: RuntimeAction[];
}
```

## 8.6 设计说明

### 为什么 `when` 要挂在所有 action 上

这样：

- 页面按钮
- 生命周期动作
- 批量动作
- AI / workflow 触发动作

都可以统一通过 CEL 表达式做条件裁决。

### 为什么保留 `callApi`

因为并不是所有动作都已经抽象成 flow/workflow/agent。主线方案不应一开始就强迫所有后端能力都做成 flow。

但 `callApi` 只应作为平台动作的一种，不应继续让页面自己散落 URL 行为。

---

## 9. ActionResult 与执行反馈

## 9.1 接口定义

```ts
export interface RuntimeActionResult {
  actionId?: Id;
  actionType: RuntimeAction['type'];
  success: boolean;
  data?: unknown;
  message?: string;
  errorCode?: string;
  errorMessage?: string;
}

export interface RuntimeActionExecutionSummary {
  success: boolean;
  results: RuntimeActionResult[];
}
```

## 9.2 设计说明

未来无论动作来源是：

- 页面按钮
- 页面初始化
- 表单提交后
- 审批通过
- AI 调用

都应能被统一记录为 `ActionResult`，方便：

- 调试
- 审计
- 页面回显
- 失败补偿

---

## 10. RuntimeBinding

## 10.1 目标

`RuntimeBinding` 是 runtime 的第三核心对象。

它用于描述页面“需要什么数据”，而不是页面“自己怎么拼 URL 取数据”。

## 10.2 绑定类型

```ts
export type RuntimeBinding =
  | ListBinding
  | RecordBinding
  | FormBinding
  | QueryBinding;
```

## 10.3 具体绑定定义

```ts
export interface ListBinding {
  kind: 'list';
  key: Key;
  entityKey: Key;
  queryKey?: Key;
  filters?: unknown;
  sort?: unknown;
  pageSize?: number;
}

export interface RecordBinding {
  kind: 'record';
  key: Key;
  entityKey: Key;
  idExpr: string; // CEL expression
}

export interface FormBinding {
  kind: 'form';
  key: Key;
  entityKey: Key;
  mode: RuntimePageMode;
  recordIdExpr?: string;
  initialValueExpr?: string;
}

export interface QueryBinding {
  kind: 'query';
  key: Key;
  source: 'api' | 'flow' | 'workflow' | 'agent';
  sourceKey: Key;
  inputExpr?: string;
}
```

## 10.4 设计说明

### 为什么区分 `RecordBinding` 和 `FormBinding`

因为二者关注点不同：

- `RecordBinding`：读一条记录
- `FormBinding`：围绕编辑模式、初始值、提交行为展开

如果混成一个类型，后续表单生命周期和记录上下文会很容易打架。

### 为什么 `QueryBinding` 要单独存在

因为 app-web 后面不仅要绑定实体数据，还会绑定：

- 逻辑流结果
- 审批结果
- Agent 输出
- 聚合报表查询

所以不能只以“实体 CRUD”来理解 binding。

---

## 11. RuntimeExecution

## 11.1 目标

`RuntimeExecution` 是 runtime 治理的起点。

每次用户进入运行页，都应该有一个 execution 记录，用于承载：

- 当前运行实例标识
- 所属 release
- 所属 app/page
- 当前状态
- 错误信息
- 开始结束时间

## 11.2 接口定义

```ts
export interface RuntimeExecution {
  executionId: Id;
  releaseId: Id;
  releaseVersion: number;
  appKey: Key;
  pageKey: Key;
  userId?: Id;
  tenantId: Id;
  traceId?: string;
  status: RuntimeStatus;
  startedAt: string;
  finishedAt?: string;
  errorCode?: string;
  errorMessage?: string;
}
```

## 11.3 扩展事件定义

```ts
export interface RuntimeAuditEvent {
  executionId: Id;
  traceId?: string;
  eventType:
    | 'page.enter'
    | 'page.leave'
    | 'action.start'
    | 'action.finish'
    | 'binding.load'
    | 'binding.submit'
    | 'runtime.error';
  timestamp: string;
  payload?: ValueMap;
}
```

## 11.4 设计说明

### 为什么页面进入就要创建 execution

因为以后很多问题都不是“页面没渲染出来”，而是：

- 某个 release 在某个租户失败
- 某次页面进入后某个动作出错
- 某个审批按钮在部分上下文失败

没有 execution，你就很难把问题和具体运行实例绑起来。

---

## 12. ExpressionContext（前端对齐接口）

## 12.1 目标

前端不再自行扩张表达式执行，而是统一围绕后端 CEL 接口组织请求上下文。

## 12.2 接口定义

```ts
export interface RuntimeExpressionContext {
  record?: ValueMap;
  user?: ValueMap;
  page?: ValueMap;
  app?: ValueMap;
  tenant?: ValueMap;
  global?: ValueMap;
  form?: ValueMap;
}

export interface ExpressionValidateRequest {
  expression: string;
}

export interface ExpressionValidateResponse {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  variables: string[];
}

export interface ExpressionEvaluateRequest {
  expression: string;
  record?: ValueMap;
  user?: ValueMap;
  page?: ValueMap;
  app?: ValueMap;
  tenant?: ValueMap;
  global?: ValueMap;
  form?: ValueMap;
}

export interface ExpressionEvaluateResponse {
  success: boolean;
  resultValue?: string | null;
  resultBool?: boolean | null;
  error?: string | null;
}
```

## 12.3 设计说明

当前后端接口只显式接收 `record / user / page`，但主线方向建议前端协议先按完整变量域准备好。

这样做的好处是：

- 前端 runtime 内部模型先稳定
- 后端接口可以按阶段扩展
- 不会因为早期接口少字段，导致前端协议未来再次大改

---

## 13. Store 与 Host 的最小职责

这部分不是协议主体，但建议同步固定职责边界。

## 13.1 RuntimeContextStore

```ts
export interface RuntimeContextStore {
  getContext(): RuntimeContext;
  patchContext(patch: Partial<RuntimeContext>): void;
  setRecord(record?: RuntimeRecordContext): void;
  setSelection(selection?: ValueMap[]): void;
  setGlobal(name: string, value: unknown): void;
}
```

## 13.2 RuntimePageHostProps

```ts
export interface RuntimePageHostProps {
  manifest: RuntimeManifest;
  context: RuntimeContext;
}
```

### 边界要求

- `RuntimePageHost` 负责渲染与桥接
- `ActionExecutor` 负责执行动作
- `BindingResolver` 负责解释绑定
- `ContextStore` 负责状态容器

不要让 `RuntimePageHost` 再回到“大而全页面组件”的旧路。

---

## 14. 与当前代码的映射建议

## 14.1 现有 `PageRuntimeRenderer.vue`

建议演进为：

- `RuntimePageHost.vue`
- 只负责：拿 manifest、接 context、渲染 AMIS、桥接事件
- 不再直接负责所有 API 拼接和运行时逻辑堆积

## 14.2 现有 `api-runtime.ts`

建议演进为：

```ts
export interface RuntimeGateway {
  getRuntimeManifest(appKey: string, pageKey: string): Promise<RuntimeManifest>;
  createRuntimeExecution(input: {
    appKey: string;
    pageKey: string;
    releaseId: string;
  }): Promise<RuntimeExecution>;
  reportRuntimeEvent(event: RuntimeAuditEvent): Promise<void>;
}
```

## 14.3 现有 `ExpressionEngine.ts`

建议演进为：

- `cel-preview-client.ts`
- 只负责：validate / evaluate 请求封装
- 不再承担运行期独立表达式执行职责

## 14.4 现有 `applyRuntimeApis()`

建议演进为：

- `amis-binding-adapter.ts`
- 从“遍历 schema 自动补 URL”升级为“按 binding 协议注入数据适配”

---

## 15. 推荐的首批文件骨架

建议第一批只落这些文件，避免一下子铺太大：

```text
src/frontend/apps/app-web/src/runtime/
  context/runtime-context.ts
  context/runtime-context-store.ts
  actions/action-types.ts
  actions/action-result.ts
  bindings/binding-types.ts
  release/runtime-manifest.ts
  release/runtime-execution.ts
  expressions/expression-types.ts
  hosts/RuntimePageHost.vue
```

这样可以先把**协议层**立住，再推进执行器和适配器。

---

## 16. 这份接口文档解决什么问题

这套接口不是为了“写得更好看”，而是为了解决当前 runtime 的主线问题：

### 问题 1：上下文散落

通过 `RuntimeContext` 收口。

### 问题 2：页面行为散落

通过 `RuntimeAction` 收口。

### 问题 3：数据获取方式散落

通过 `RuntimeBinding` 收口。

### 问题 4：线上运行不可治理

通过 `RuntimeExecution + RuntimeAuditEvent` 收口。

### 问题 5：表达式前后端双轨

通过 `RuntimeExpressionContext` 和 CEL 接口收口。

---

## 17. 下一步建议

基于这份接口文档，最适合继续输出两份文档：

### 17.1 `app-web runtime 首批迭代拆解.md`

把这份接口直接拆成 2~4 周任务包，例如：

- 第 1 周：落 context / manifest / execution
- 第 2 周：落 action types / host / gateway
- 第 3 周：落 binding 协议与 AMIS adapter
- 第 4 周：接入 CEL validate/evaluate 与审计事件

### 17.2 `app-web runtime 时序图.md`

把以下链路画清楚：

- 页面进入
- 页面按钮点击
- 表单提交
- workflow / agent 触发
- 异常上报与执行收尾

---

## 18. 最终结论

如果上一份文档解决的是：

> `app-web` 下一阶段应该往哪里走

那么这份文档解决的是：

> `app-web` 应该先把哪些协议固定下来

主线结论不变：

> **先固定 runtime 核心协议，再扩动作引擎、绑定引擎和治理层，最后再让设计器稳定生产这些协议对应的产物。**

只有这样，`app-web` 才会真正从“页面渲染壳”，演进成“统一应用运行时内核”。

## 19. 第三轮深挖（2026-04）执行同步

### 19.1 迁移落点

- `@atlas/runtime-core` 作为纯协议+执行中枢，不承担框架耦合依赖。
  - `src/frontend/packages/runtime-core/src/actions/`：动作类型、结果、处理器注册、执行入口。
  - `src/frontend/packages/runtime-core/src/bindings/`：绑定类型、解析器、查询构造、元数据与记录服务。
  - `src/frontend/packages/runtime-core/src/lifecycle/`：生命周期钩子定义与执行器。
- `app-web` 侧边界化接入，只保留适配器能力。
  - `src/frontend/apps/app-web/src/runtime/actions/action-executor.ts`：注入 `Pinia` 上下文与 `vue-router`。
  - `src/frontend/apps/app-web/src/runtime/bindings/binding-resolver.ts`：注入运行时 URL 构造函数。
  - `src/frontend/apps/app-web/src/runtime/bindings/runtime-data-service.ts`：适配 `RuntimeDataClient` 到 `api-runtime`。
  - `src/frontend/apps/app-web/src/runtime/bindings/entity-metadata-service.ts`：适配 `RuntimeMetadataClient` 到 `api-core/requestApi`。
- 设计器宿主统一化。
  - `src/frontend/packages/designer-vue/src/canvas/DesignerCanvas.vue`：以 `mode` + 插槽组合托管实体/关系/视图/转换设计器。
  - `src/frontend/apps/platform-web/src/pages/dynamic/SharedDesignerPage.vue`：统一消费 `DesignerCanvas`，并按 `mode/tableKey/viewKey` 驱动路由参数。
  - `src/frontend/apps/platform-web/src/pages/dynamic/DataDesignerPage.vue`：保持兼容委托到 `SharedDesignerPage`。

### 19.2 运行时内核迁移验收清单

- 协议边界
  - `runtime-core` 目录已承载 `action-types`、`action-result`、`action-registry`、`action-executor`、`binding-*`、`lifecycle-*` 的核心定义与执行逻辑。
  - `app-web` 侧未重复实现动作求值、绑定解析、绑定执行流程。
- 接口一致性
  - `app-web/src/runtime/actions/*`、`app-web/src/runtime/bindings/*` 与 `@atlas/runtime-core` 的类型导出/代理关系一致。
  - 新增/修改的类型变更已回写到文档契约（`docs/contracts.md`）及接口说明（本文件）。
- 运行链路
  - AMIS 事件触发最终进入 `RuntimeActionContext` 的执行入口。
  - `when` 条件由表达式器在适配层求值并正确支持分支与循环类动作。
  - 页面生命周期钩子可按 `RuntimeLifecycleHooks` 调度并回传 `ActionExecutionSummary`。
- 设计器可用性
  - 页面模式切换由 `mode` 参数驱动，URL 查询参数保持幂等更新。
  - 现网行为保持兼容，历史入口仍可进入实体/关系/视图/转换模式页面。
- 质量与交付
  - `@atlas/runtime-core` 与 `@atlas/designer-vue` 的导出清单保持可追踪（`index.ts` 统一聚合）。
  - 与等保约束一致：关键写操作带幂等与 CSRF 要求，避免重复数据库访问模式，控制器通过服务层调用。
- 回归入口
  - 启动平台+应用工作台链路，确认运行时页面、关系/视图/转换设计器可正常打开和交互。
