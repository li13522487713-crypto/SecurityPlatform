---
name: Mendix MVP Studio
overview: 在 `packages/mendix/`（相对 `src/frontend/packages/`，见下文）下建立完全独立的 Mendix 风格低代码平台 MVP，迁入 `@atlas/microflow`，包含 7 个核心包，挂载到工作空间左导航 + 资源中心微流 Tab 入口，实现采购审批完整闭环。
todos:
  - id: r00-move-microflow
    content: 轮次0：迁移 @atlas/microflow 到 packages/mendix/mendix-microflow/，更新所有引用路径
    status: pending
  - id: r01-schema
    content: 轮次1：建立 mendix-schema 包（28类 Schema + Zod + Guards）
    status: pending
  - id: r02-validator
    content: 轮次2：建立 mendix-validator 包（15条校验规则引擎）
    status: pending
  - id: r03-expression
    content: 轮次3：建立 mendix-expression 包（AST + parser + evaluator）
    status: pending
  - id: r04-runtime
    content: 轮次4：建立 mendix-runtime 包（RuntimeRenderer + ActionExecutor）
    status: pending
  - id: r05-debug
    content: 轮次5：建立 mendix-debug 包（DebugTracePanel + 模拟Trace）
    status: pending
  - id: r06-shell
    content: 轮次6：建立 mendix-studio-core Shell骨架（5区布局 + Zustand + AppExplorer）+ 工作空间左导航入口 + 资源中心微流Tab联动
    status: pending
  - id: r07-domain
    content: 轮次7：Domain Model Designer（Entity/Attribute/Association/Enumeration编辑）
    status: pending
  - id: r08-page
    content: 轮次8：Page Builder（Widget Toolbox + 组件树 + Properties Pane + Runtime Preview）
    status: pending
  - id: r09-microflow
    content: 轮次9：Microflow Designer（集成迁移后的 @atlas/microflow MicroflowEditor + 属性面板）
    status: pending
  - id: r10-workflow
    content: 轮次10：Workflow Designer（7种节点 + User Task outcomes + 采购审批流程展示）
    status: pending
  - id: r11-security
    content: 轮次11：Security Editor（Role矩阵 + Page/MF/Entity Access）
    status: pending
  - id: r12-runtime-preview
    content: 轮次12：Runtime Preview + Action执行闭环（提交→MF执行→Trace）
    status: pending
  - id: r13-sample
    content: 轮次13：采购审批示例数据（SAMPLE_PROCUREMENT_APP完整Schema）
    status: pending
  - id: r14-tests
    content: 轮次14：测试补充（Schema/Validator/Expression/Runtime各包单测）
    status: pending
  - id: r15-docs
    content: 轮次15：文档+构建验证（docs/lowcode-mendix-mvp/ + 运行pnpm build/test/lint）
    status: pending
isProject: false
---

# Mendix 低代码平台 MVP 实施计划

## 核心定位

- 独立于现有 `@atlas/lowcode-*` 体系（Coze 向）
- 所有 Mendix 代码集中在 `packages/mendix/`（物理路径 `src/frontend/packages/mendix/`，下文中凡写 `packages/…` 均指相对 `src/frontend/packages`）
- 现有 `@atlas/microflow` 迁入此目录，物理目录为 `mendix-microflow/`（与其它子包 `mendix-*` 命名一致）
- 包名前缀：`@atlas/mendix-*`（microflow 包保留原名 `@atlas/microflow` 以避免大范围 import 改动）
- pnpm workspace 已含 `packages/**`，目录迁移后自动识别，无需改 workspace 配置

## 最终目录结构

**路径约定**：`src/frontend/packages` 为前端子仓的 packages 根；下文目录树以 **`packages/mendix/`** 表示，即 `src/frontend/packages/mendix/`。

```
packages/mendix/
├── mendix-microflow/     # @atlas/microflow  ← 迁移进来（原 packages/microflow/）
├── mendix-schema/        # @atlas/mendix-schema      核心协议 + Zod 校验（28类 Schema）
├── mendix-validator/     # @atlas/mendix-validator   模型校验引擎（15条规则）
├── mendix-expression/    # @atlas/mendix-expression  表达式引擎（AST + evaluator）
├── mendix-runtime/       # @atlas/mendix-runtime     运行态渲染器 + 动作执行
├── mendix-debug/         # @atlas/mendix-debug       调试追踪面板
└── mendix-studio-core/   # @atlas/mendix-studio-core Studio UI 全部设计器（含微流）
```

## @atlas/microflow 迁移说明

**迁移路径**：`packages/microflow/` → `packages/mendix/mendix-microflow/`

**包名保持不变**：`@atlas/microflow`（pnpm workspace 按 package.json name 解析，物理路径变更不影响消费者）

**需要检查引用的文件**（探查期已确认，均在 app-web 内）：
- `apps/app-web/package.json` — 依赖声明，值为 `workspace:*`，无需改动
- `apps/app-web/src/app/pages/microflow-resource-tab.tsx` — import `@atlas/microflow` 路径不变
- `apps/app-web/src/app/pages/microflow-demo-page.tsx` — import 不变
- `apps/app-web/src/app/pages/microflow-editor-page.tsx` — import 不变
- `apps/lowcode-studio-web/package.json` — 如有依赖，不变

**迁移操作**：仅物理移动目录（`git mv`），不修改任何 import 语句。

## 路由集成架构

### 新增路由（在 SpaceShellLayout children 内）

```
/space/:space_id/mendix-studio           → MendixStudioIndexPage（应用列表/入口）
/space/:space_id/mendix-studio/:appId    → MendixStudioApp（完整 Studio Shell）
```

### 工作空间左导航接入

修改 [`workspace-shell.tsx`](src/frontend/apps/app-web/src/app/layouts/workspace-shell.tsx) 的 `buildAllSpaceLinks`，在 "library" 之后添加：

```
{
  key: "mendix-studio",
  label: "Mendix Studio",
  path: `/space/${workspaceId}/mendix-studio`,
  icon: <IconBox />,
  testId: "app-sidebar-item-mendix-studio"
}
```

### 资源中心微流 Tab 联动

在现有 `MicroflowResourceTab`（`microflow-resource-tab.tsx`）顶部工具栏添加"在 Mendix Studio 中打开"按钮，导航到 `/space/:space_id/mendix-studio`，使微流 Tab 与 Studio 之间形成双向跳转入口。

点击 Studio 左侧 AppExplorer 中的某条微流，直接打开 `@atlas/microflow` 的 `MicroflowEditor` 在中间工作区渲染。

## 包依赖关系

```
mendix-schema (zod 唯一外部依赖)
    ↑              ↑
mendix-validator  mendix-expression
    ↑              ↑
mendix-runtime (依赖 mendix-schema + mendix-expression)
    ↑
mendix-debug (依赖 mendix-schema + mendix-runtime)
    ↑
mendix-studio-core (依赖以上全部 + @atlas/microflow + semi-ui + reactflow + zustand)
    ↑
app-web (路由接入 mendix-studio-core，左导航新增入口)
```

## 技术选型

- UI：`@douyinfe/semi-ui`（强制，所有 UI 组件）
- 状态：`zustand`（Studio 全局 store）
- Schema 校验：`zod`
- 微流画布：`@atlas/microflow` 自带 FlowGram 画布（迁移后直接复用，无需 React Flow）
- 工作流画布：`reactflow`（新增到 mendix-studio-core，仅用于 Workflow Designer）
- 测试：`vitest`

## 实施轮次（16 轮闭环）

### 轮次 0：迁移 @atlas/microflow
- 在仓库根目录：`git mv src/frontend/packages/microflow src/frontend/packages/mendix/mendix-microflow`（或先 `cd src/frontend` 后 `git mv packages/microflow packages/mendix/mendix-microflow`）
- 验证 pnpm workspace 仍能识别（`packages/**` 通配）
- 验证 app-web 中现有 3 个引用文件编译不变
- 验证 `microflow-resource-tab.tsx` 和 `microflow-demo-page.tsx` 正常工作

### 轮次 1：mendix-schema 包
- 28 类 Schema 类型（discriminated union 设计，见需求第五章）
- Zod 运行时校验器（每类 Schema 对应 zod schema）
- 类型守卫（guards）
- 文件：`types/`, `zod/`, `guards/`, `index.ts`

### 轮次 2：mendix-validator 包
- 15 条验证规则的 `ValidatorEngine`
- `ValidationError` 结构（severity / code / target.kind / target.id / target.path / quickFixes）
- 文件：`engine.ts`, `rules/`, `index.ts`

### 轮次 3：mendix-expression 包
- AST 定义（字面量 / 变量引用 / 属性访问 / 布尔 / 比较 / if-else / 枚举值 / contains）
- `parseExpression` / `inferExpressionType` / `evaluateExpression` MVP
- `collectExpressionDependencies` / `validateExpression`
- 文件：`ast.ts`, `parser.ts`, `infer.ts`, `evaluate.ts`, `deps.ts`, `index.ts`

### 轮次 4：mendix-runtime 包
- `RuntimeRenderer` React 组件（10 个 Widget 类型）
- `RuntimeActionExecutor` 模拟执行器（callMicroflow 闭环）
- `RuntimeContext` / `useRuntimeContext`
- 文件：`renderer/`, `executor/`, `context/`, `index.ts`

### 轮次 5：mendix-debug 包
- `DebugTracePanel` React 组件（展示 Trace steps + expressionResults + uiCommands）
- `FlowExecutionTrace` 数据结构（完整 26 字段）
- 模拟 Trace 生成工具
- 文件：`trace-panel.tsx`, `trace-types.ts`, `mock-trace.ts`, `index.ts`

### 轮次 6：mendix-studio-core - Shell + 路由接入
- `MendixStudioApp` 5 区布局（顶栏 / AppExplorer / 中央工作区 / 属性面板 / 底部错误栏）
- `MendixStudioIndexPage` 应用列表入口页
- `useStudioStore` Zustand store（当前 appSchema / 选中节点 / 激活设计器类型）
- `AppExplorer` 左侧树（Modules / Domain Model / Pages / Microflows / Workflows / Security）
- 修改 `workspace-shell.tsx` 添加左导航"Mendix Studio"入口
- 修改 `app.tsx` 添加 `/space/:space_id/mendix-studio` 嵌套路由
- 修改 `microflow-resource-tab.tsx` 添加"在 Mendix Studio 中打开"按钮
- 文件：`shell/`, `stores/`, `explorer/`, `index.tsx`

### 轮次 7：Domain Model Designer
- Entity 卡片列表 + Attribute 行内编辑
- 新增 Entity / Attribute / Association / Enumeration
- 删除前引用检查（调用 mendix-validator）
- Entity Access 摘要展示
- 文件：`designers/domain-model/`

### 轮次 8：Page Builder
- Widget Toolbox（左侧拖拽板） + 组件树 + Properties Pane（右侧）
- DataView 绑定 Entity / Page Parameter
- TextBox / NumberInput / DropDown / Button 属性绑定
- 调用 `mendix-runtime` RuntimeRenderer 做运行时预览
- 文件：`designers/page-builder/`

### 轮次 9：Microflow Designer（集成 @atlas/microflow）
- 直接嵌入 `@atlas/microflow` 的 `MicroflowEditor` 组件
- Studio AppExplorer 点击微流 → 中央工作区渲染 MicroflowEditor
- Studio 属性面板与 MicroflowEditor 的 `onSchemaChange` 联动（同步 mendix-schema 的 MicroflowSchema）
- 采购审批示例微流 `MF_SubmitPurchaseRequest` 在 Studio 中可加载、可编辑
- 文件：`designers/microflow/microflow-designer.tsx`（薄封装，核心在 @atlas/microflow）

### 轮次 10：Workflow Designer
- 基于 `reactflow` 的画布（7 种节点类型）
- User Task outcomes / Decision decisionOutcome edges
- 采购审批 `WF_PurchaseApproval` 可视化展示
- 文件：`designers/workflow/`

### 轮次 11：Security Editor
- User Role / Module Role 列表（可增删）
- User Role → Module Role 映射矩阵
- Page Access / Microflow Access / Entity Access 矩阵（勾选控制）
- Attribute member access 列表 + XPath Constraint 文本框
- 安全模型说明横幅（Entity Access 是数据安全核心）
- 文件：`designers/security/`

### 轮次 12：Runtime Preview + Action 执行闭环
- 顶栏"预览"按钮打开 `RuntimeRenderer` 侧抽屉或全屏
- 点击"提交"触发 `MF_SubmitPurchaseRequest` 模拟执行链路
- showMessage + refreshObject + validationFeedback 命令展示
- Debug Trace Drawer 联动（显示本次执行的 Trace）
- 文件：`panels/runtime-preview-panel.tsx`, `panels/debug-trace-drawer.tsx`

### 轮次 13：采购审批示例数据
- `SAMPLE_PROCUREMENT_APP`：完整 `LowCodeAppSchema`
- Entity：PurchaseRequest / Department / Account / ApprovalComment
- Enumeration：PurchaseStatus（Draft / Submitted / NeedManagerApproval / NeedFinanceApproval / Approved / Rejected）
- Page：`PurchaseRequest_EditPage`（含 DataView + NumberInput + TextArea + DropDown + Button）
- Microflow：`MF_SubmitPurchaseRequest`（Amount 校验 → Status 设置 → Workflow 触发）
- Workflow：`WF_PurchaseApproval`（Start → Decision → Finance/Manager UserTask → End）
- Security：Requester / Manager / Finance / Admin 四角色
- 文件：`mendix-studio-core/src/samples/procurement.ts`

### 轮次 14：测试补充
- 每个包 `*.spec.ts` 覆盖核心路径
- Schema Zod 校验冒烟测试
- Validator 15 条规则单测
- Expression parser / evaluator 单测
- Runtime executor MF 执行链路测试
- 示例 App Schema 经 Validator 校验通过测试
- `@atlas/microflow` 迁移后编译回归（不修改任何实现）

### 轮次 15：文档 + 构建验证
- 创建 `docs/lowcode-mendix-mvp/` 下 6 份文档
- 更新 `docs/contracts.md`（新增 Mendix Studio 专章）
- 运行 `pnpm run build:app-web` / `pnpm run test:unit` / `pnpm run lint:app-web`

## 修改的文件清单

**迁移**
- `packages/microflow/` → `packages/mendix/mendix-microflow/`（git mv；完整路径为 `src/frontend/packages/…`）

**新增**（全部在 `packages/mendix/`，约 130-160 个 `.ts`/`.tsx` 文件）
- 6 个新建包：`mendix-schema` / `mendix-validator` / `mendix-expression` / `mendix-runtime` / `mendix-debug` / `mendix-studio-core`（另见上文 **迁移** `mendix-microflow/`）

**修改 app-web（最小侵入）**
- `app.tsx` — 新增 `/space/:space_id/mendix-studio` 嵌套路由
- `route-handles.ts` — 新增 `MENDIX_STUDIO_ROUTE_HANDLE`
- `layouts/workspace-shell.tsx` — `buildAllSpaceLinks` 新增 Mendix Studio 左导航项
- `pages/microflow-resource-tab.tsx` — 顶部工具栏新增"在 Mendix Studio 中打开"按钮
- `package.json` — 新增 `@atlas/mendix-studio-core: workspace:*`
- `tsconfig.json` — 新增 `@atlas/mendix-*` 路径别名

**修改文档**
- `docs/contracts.md` — 新增 Mendix Studio 专章
- `docs/lowcode-mendix-mvp/`（新建 6 份）

## 关键约束

- 所有 UI：Semi Design（无其他组件库）
- 所有类型：strict TypeScript，0 errors 0 warnings
- 所有 Schema：Zod 运行时校验
- 迁移 `@atlas/microflow` 时：不修改任何现有实现逻辑，只移动目录
- i18n：Studio 文案用 `copy.ts` 常量集中存放（含英文 key），不硬编码 CJK 到 JSX
- 不修改任何已有 `@atlas/lowcode-*` 包

## 完成标准

满足需求原文 21 条：Studio 可打开、示例可加载、各设计器可编辑、微流 Tab 有 Studio 跳转入口、Runtime Preview 可运行、Debug Trace 可查看、Validator 可输出错误、构建通过。