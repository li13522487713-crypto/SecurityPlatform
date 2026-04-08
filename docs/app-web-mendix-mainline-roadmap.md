# app-web Mendix 化主线方向方案

## 1. 文档目标

本文档用于明确 `app-web` 下一阶段的**主线方向**，用于统一产品、前端、后端与平台设计侧的建设节奏。

这份方案不追求一次性覆盖所有能力，而是聚焦一条最重要的主航道：

> **把 `app-web` 从“页面渲染壳”升级为“统一应用运行时内核”。**

也就是说，后续建设重点不是继续堆页面组件，也不是先做一个很重的设计器，而是优先把运行时基础设施补齐，使平台具备承载复杂业务应用的能力。

---

## 2. 主线结论

### 2.1 核心判断

当前 `app-web` 已经具备低代码运行时的雏形：

- 能基于页面标识加载运行时页面
- 能获取页面 Schema 并进行渲染
- 已经具备一定的菜单、路由、运行时接口能力
- 页面运行形态已经开始向“按应用 + 页面动态解释执行”靠拢

但当前阶段仍主要停留在：

> **Schema Renderer（Schema 渲染器）**

离 Mendix 这类平台级产品真正接近的关键，不是再继续增强单个页面的渲染能力，而是补齐以下四个核心内核：

1. **Runtime Context Engine**：统一运行上下文
2. **Action Engine**：统一动作执行引擎
3. **Data Binding Engine**：统一数据绑定与解析引擎
4. **Runtime Governance Layer**：统一运行治理能力

### 2.2 主线方向

下一阶段的唯一主线建议如下：

> **以 AMIS 作为唯一页面 DSL，以 CEL 作为唯一表达式规范，在 `app-web` 内建设统一运行时内核。**

这条主线成立后，页面、表单、审批、工作流、AI、导航、菜单、权限、发布、审计，才能自然汇聚到一套运行时体系中。

---

## 3. 本阶段不作为主线的事项

为了保证推进效率，以下事项不应作为当前阶段的主线：

### 3.1 不以“重设计器建设”作为先手

设计器当然重要，但设计器的本质是生产工具。

如果运行时协议没有稳定下来，设计器只会不断反向挤压运行时，最终造成：

- Schema 结构频繁变化
- 行为协议反复推翻
- 页面能画出来但应用跑不起来
- 设计态和运行态脱节

因此，当前阶段应坚持：

> **先运行时，后设计器。**

### 3.2 不新增第二套页面 DSL

页面模型必须继续收敛，避免再次分裂为：

- 一套 AMIS
- 一套自研页面 JSON
- 一套审批专用表单 DSL

主线要求是：

> **页面层统一 Schema，能力层通过 runtime 扩展，而不是通过新增 DSL 扩展。**

### 3.3 不继续扩张前端任意 JS 表达式能力

表达式能力必须走统一规范，避免前端与后端行为不一致。

主线要求是：

> **表达式统一收口到 CEL 体系，前端不再继续扩张运行期 `new Function` 风格表达式执行。**

---

## 4. 下一阶段建设目标

## 4.1 总体目标

把 `app-web` 建设为统一应用运行时壳，使其能够承载：

- 页面运行
- 页面事件与动作流
- 表单提交与生命周期
- 数据查询与记录上下文
- 流程、审批、AI 的触发与结果回显
- 已发布版本的稳定运行
- 运行过程审计与问题追踪

## 4.2 阶段性目标

本阶段不追求“一次做成完整 Mendix”，而是实现以下阶段性跨越：

> **从“能渲染页面”升级为“能稳定运行应用”。**

这意味着验收标准会从“页面出来了”变成：

- 页面能拿到统一上下文
- 页面事件能走统一动作引擎
- 数据绑定不再靠散落的 URL 拼接
- 页面执行过程可追踪、可定位、可审计

---

## 5. 主线建设的四大内核

## 5.1 Runtime Context Engine

### 目标

构建统一运行上下文，让页面、组件、表达式、动作、流程都运行在同一上下文中。

### 需要统一承载的上下文

- 当前租户
- 当前应用
- 当前页面
- 当前用户
- 当前路由参数
- 当前查询参数
- 当前记录（record）
- 当前选中项（selection）
- 全局变量（global vars）
- 环境信息（traceId、runtimeExecutionId 等）

### 为什么它是主线第一优先级

Mendix 的核心不在于控件库，而在于：

- 页面知道自己在哪个应用里
- 组件知道当前绑定的是哪个记录
- 表达式知道当前用户、当前租户、当前页面状态
- 动作执行能基于统一上下文做判断和跳转

如果没有统一上下文，就只能停留在“页面展示系统”，而不是“应用运行平台”。

### 本阶段产物

建议新增：

- `runtime/context/runtime-context.ts`
- `runtime/context/runtime-context-store.ts`
- `runtime/context/runtime-context-provider.ts`

---

## 5.2 Action Engine

### 目标

把页面事件、按钮行为、页面生命周期行为统一收口到一个平台级动作协议中。

### 为什么必须做

如果按钮行为仍然依赖页面中散落的 URL、散落的 API 配置、散落的 onEvent 逻辑，那么平台会越来越难维护。

一旦需要：

- 跳转
- 打开弹窗
- 提交表单
- 调接口
- 刷新数据
- 执行动作流
- 触发审批
- 触发 AI Agent

页面层就会变得异常混乱。

### 主线要求

页面层不再直接承载复杂行为定义，而是通过统一动作协议交给运行时执行。

### 建议支持的动作类型

- `navigate`
- `openDialog`
- `submitForm`
- `callApi`
- `runFlow`
- `runWorkflow`
- `runAgent`
- `refresh`
- `setVar`
- `branch`
- `foreach`

### 本阶段产物

建议新增：

- `runtime/actions/action-types.ts`
- `runtime/actions/action-executor.ts`
- `runtime/actions/action-registry.ts`
- `runtime/actions/action-result.ts`

---

## 5.3 Data Binding Engine

### 目标

把页面组件与数据之间的关系，从“组件里手写接口地址”，升级为“运行时统一解析 binding”。

### 当前问题

如果表单、CRUD、详情页都由页面临时拼装 API 地址，那么：

- 页面层会越来越重
- 不同组件的数据协议不统一
- 难以沉淀模型驱动能力
- 无法真正支持 record/list/form 的统一上下文

### 主线要求

统一引入 binding 概念，例如：

- `ListBinding`
- `RecordBinding`
- `FormBinding`

并通过运行时统一解析：

- 当前实体
- 当前记录 ID
- 当前查询条件
- 当前分页、排序、过滤条件
- 当前父子关系上下文

### 本阶段产物

建议新增：

- `runtime/bindings/binding-types.ts`
- `runtime/bindings/binding-resolver.ts`
- `runtime/bindings/runtime-data-service.ts`
- `runtime/bindings/runtime-query-builder.ts`

---

## 5.4 Runtime Governance Layer

### 目标

让运行时成为“可治理系统”，而不是只能跑起来的黑盒。

### 必须覆盖的能力

- 已发布版本运行
- release 与 route 显式关联
- runtime execution 创建与上报
- traceId 全链路跟踪
- 审计日志
- 错误定位
- 页面级回滚与问题追查基础

### 为什么它是主线而不是锦上添花

平台一旦进入真实业务场景，问题一定来自：

- 某个版本发布后异常
- 某个租户在某个页面失败
- 某个按钮执行流程时参数不一致
- 某个条件表达式在部分页面上行为异常

没有运行治理，平台能力越强，故障定位越难。

### 本阶段产物

建议新增：

- `runtime/release/runtime-release.ts`
- `runtime/release/runtime-route.ts`
- `runtime/release/runtime-execution.ts`
- `runtime/audit/runtime-audit.ts`

---

## 6. 统一主线下的技术原则

## 6.1 页面 DSL 唯一化

- 页面结构统一由 AMIS Schema 承载
- 页面扩展优先通过 runtime adapter 实现
- 不再新增第二套页面 DSL

## 6.2 表达式唯一化

- 表达式规则统一收口至 CEL
- 前端仅保留设计期或兼容期能力
- 运行期最终裁决以后端 CEL 计算结果为准

## 6.3 行为统一收口

- 页面事件最终都转成 RuntimeAction
- 页面不再散落自定义复杂行为
- 平台能力通过 ActionExecutor 暴露

## 6.4 数据统一绑定

- 页面不直接手拼业务 API
- 页面通过 binding 描述“需要什么数据”
- runtime 负责解析“怎么取、怎么写、怎么刷新”

## 6.5 运行态只面向已发布产物

- runtime 只消费已发布页面/已发布版本
- 草稿态与运行态严格隔离
- 设计器不直接影响线上 runtime 稳定性

---

## 7. 建议的目录蓝图

```text
src/frontend/apps/app-web/src/runtime/
  bootstrap/
    bootstrap-runtime.ts
    runtime-manifest-loader.ts

  context/
    runtime-context.ts
    runtime-context-store.ts
    runtime-context-provider.ts

  expressions/
    cel-preview-client.ts
    expression-types.ts

  actions/
    action-types.ts
    action-executor.ts
    action-registry.ts
    action-result.ts

  bindings/
    binding-types.ts
    binding-resolver.ts
    runtime-data-service.ts
    runtime-query-builder.ts

  lifecycle/
    lifecycle-types.ts
    page-lifecycle-runner.ts

  release/
    runtime-release.ts
    runtime-route.ts
    runtime-execution.ts

  audit/
    runtime-audit.ts

  adapters/
    amis-action-adapter.ts
    amis-binding-adapter.ts
    amis-event-bridge.ts

  hosts/
    RuntimePageHost.vue
    RuntimeDialogHost.vue
    RuntimeFlowHost.vue
```

---

## 8. 本阶段主线实施顺序

## 8.1 第一阶段：统一运行时入口

### 目标

先把页面运行入口统一成真正的 runtime host，而不是继续在页面组件里堆逻辑。

### 重点事项

- 建立 `bootstrap-runtime.ts`
- 新建 `RuntimePageHost.vue`
- 把现有页面运行逻辑从旧页面组件中抽离
- 建立统一 `RuntimeContextStore`
- 页面初始化时创建 `runtimeExecutionId`

### 阶段成果

运行时拥有自己的启动入口、上下文容器和生命周期起点。

---

## 8.2 第二阶段：统一动作执行链路

### 目标

把页面事件从页面配置里抽出来，统一转成 runtime action。

### 重点事项

- 定义 `RuntimeAction`
- 落地 `ActionExecutor`
- 建立 `AmisEventBridge`
- 支持导航、提交、刷新、流程触发等基础动作

### 阶段成果

页面事件不再直接散落在 schema 中，而是可以被 runtime 解释执行。

---

## 8.3 第三阶段：统一数据绑定

### 目标

让列表、详情、表单都运行在同一种 binding 体系上。

### 重点事项

- 定义 binding types
- 落地 binding resolver
- 把 CRUD/Form 自动 API 注入迁移为 binding 适配
- 把 record/list/form 模式纳入统一上下文

### 阶段成果

页面可以以声明式方式描述数据需求，而运行时负责解析和执行。

---

## 8.4 第四阶段：统一运行治理

### 目标

让运行时具备可追踪、可定位、可治理能力。

### 重点事项

- release 版本信息显式进入 runtime
- 页面运行时创建 execution
- 页面动作和异常上报
- 审计埋点与 trace 透传

### 阶段成果

平台具备基本线上治理能力，为后续扩展审批、AI、工作流提供保障。

---

## 9. 主线里程碑定义

## M1：运行时壳成型

### 定义

`app-web` 拥有统一启动入口、统一上下文、统一页面 host。

### 验收标准

- 页面进入时统一构造 runtime context
- 页面渲染入口不再直接耦合旧的页面实现
- 页面进入后可拿到 executionId

---

## M2：动作引擎成型

### 定义

页面事件全部通过统一动作协议进入 runtime。

### 验收标准

- 按钮点击、提交、跳转、刷新等动作不再散落处理
- 页面可调用统一动作执行链
- 动作执行过程可记录、可追踪

---

## M3：绑定引擎成型

### 定义

页面数据来源从“散落 API 配置”升级为“统一 binding”。

### 验收标准

- 列表、详情、表单具备统一 binding 模型
- 页面不再手工拼主业务接口地址
- record/list/form 上下文可被动作和表达式直接使用

---

## M4：运行治理成型

### 定义

runtime 与 release / execution / audit 建立闭环。

### 验收标准

- 线上页面运行可追踪到 release
- 用户操作可追踪到 execution
- 页面异常具备基础定位信息

---

## 10. 与后续设计器建设的关系

本方案不是否定设计器，而是明确设计器建设的前置条件。

### 正确关系应为

- 先有稳定运行协议
- 再有稳定 schema 协议
- 再让设计器生产这些协议对应的产物

也就是说，后续设计器应建立在以下稳定基础之上：

- RuntimeContext
- RuntimeAction
- DataBinding
- RuntimeManifest
- Release/Execution/Audit

这样设计器出来的页面、动作、绑定、流程配置，才能真正被 app-web 稳定消费。

---

## 11. 本阶段的组织建议

## 11.1 产品侧

产品侧不要把需求拆成“再做几个新组件”，而要按运行时能力推进：

- 页面运行
- 行为执行
- 数据绑定
- 发布治理

## 11.2 前端侧

前端侧核心任务不是页面堆砌，而是搭出 runtime 骨架，并逐步把旧逻辑收编进来。

## 11.3 后端侧

后端侧应配合补齐：

- manifest 拉取
- expression validate/evaluate
- release/runtime execution
- action flow / workflow / agent 触发入口
- entity metadata / query metadata

## 11.4 平台侧

平台侧应以“统一模型”收口，而不是允许不同业务线各自扩 schema。

---

## 12. 最终结论

下一阶段的主线方向应明确为：

> **围绕 `app-web` 建设统一应用运行时内核，而不是继续围绕单页面渲染和重设计器能力分散投入。**

这条主线的重点不是“页面更好看”，也不是“配置项更多”，而是：

- 页面能稳定运行
- 行为能统一编排
- 数据能统一绑定
- 表达式能统一裁决
- 流程/AI/审批能统一接入
- 发布与审计能形成闭环

当这条主线打通之后，`app-web` 才会真正从低代码页面容器，升级为面向复杂业务应用的平台运行时内核。

---

## 13. 下一步建议

建议下一份输出继续沿主线展开，优先产出以下文档之一：

1. **`app-web runtime 核心接口定义.md`**
   - 输出 `RuntimeContext / RuntimeAction / Binding / RuntimeManifest / RuntimeExecution` 的 TypeScript 协议

2. **`app-web runtime 首批迭代拆解.md`**
   - 把本方案拆成 2~4 周可执行任务包

3. **`app-web runtime 时序图.md`**
   - 把页面进入、按钮点击、表单提交、流程触发的执行链路画清楚

如果后续继续往下走，最建议先写第 1 份。
