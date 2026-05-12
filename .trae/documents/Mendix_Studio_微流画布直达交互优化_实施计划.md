# Mendix Studio 微流画布直达交互优化 - 实施计划

## Summary

- **目标页面**：`/space/:workspaceId/mendix-studio/:appId`
- **优化目标**：在保持 Mendix 风格心智模型的前提下，把微流设计器从"功能可用但路径分散"优化为"高频建模更快、更多在画布上直接完成、属性面板保持一等入口"
- **执行顺序**：按改动包 1→6 顺序执行
- **核心原则**：不删除现有能力，只重排主路径；禁止新增桥接层；所有改动直接落在现有架构分层中

## Current State Analysis

### 关键文件确认

所有计划涉及的关键文件均已确认存在：
- `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` - Studio 主壳入口
- `src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-workbench-toolbar.tsx` - 外层工具栏
- `src/frontend/packages/mendix/mendix-studio-core/src/components/explorer-split-layout.tsx` - 左侧资源树布局
- `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerTree.tsx` - 资源树组件
- `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` - 微流编辑器主入口
- `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.tsx` - 画布核心组件
- `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx` - 节点面板
- `src/frontend/e2e/app/mendix-studio-microflow-layout.spec.ts` - E2E 测试
- `src/frontend/e2e/app/mendix-studio-microflow-create-save.spec.ts` - 创建保存测试

### 当前问题

1. 高价值动作过于分散（右键、命令面板、快捷键、多处工具栏）
2. 属性面板不是默认主路径（需右键打开）
3. 画布缺少"直达式创建/编辑"主链路
4. 双工具栏认知负担重
5. 左侧资源树对建模过程打断较大

## Proposed Changes

### 改动包 1：把微流编辑器改成"画布优先、属性常驻上下文"

**文件**：`src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`

**变更内容**：
- 节点选中后自动打开属性面板（默认主行为）
- 未选中任何节点时显示轻量空态（最近可执行动作、当前微流摘要、常见快捷提示）
- 双击节点触发"主编辑动作"（命名类节点进入标题快速编辑，动作节点打开首要配置字段）

**实现方式**：
- 调整编辑器内部布局状态机
- 选中对象时自动调用 `openPropertiesPanel()`
- 清空选择时切换到"右侧空态"而非完全关闭右侧区域
- 为节点双击增加主动作分发逻辑

**验收标准**：
- 单击节点后属性面板自动出现
- 双击节点触发对应的主编辑动作
- 未选中节点时右侧显示空态引导

---

### 改动包 2：把新增节点主路径从"左侧拖拽"升级为"画布就地插入"

**文件**：
- `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.tsx`
- `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`
- `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx`

**变更内容**：
- 空白画布点击后显示"快速添加"入口
- 选中连线后显示"在线插入节点"入口
- 节点 hover/选中时显示邻接添加入口
- 新增统一"Quick Insert"面板（搜索优先，显示最近使用/收藏/推荐节点）
- 左侧 Node Panel 保留但降级为"完整目录浏览器"

**实现方式**：
- 基于 `FlowGramMicroflowNativeCanvas` 已有的 `onCanvasBlankClick`、`onCanvasBlankContextMenu`、`onNodeContextMenu` 等事件
- 在 `editor/index.tsx` 中新增统一的 quick insert 状态与提交逻辑
- 调用现有 `onAddNode` / `splitFlowWithObject` / `createObjectFromRegistry` 流程
- `node-panel/index.tsx` 提炼搜索、收藏、推荐列表为可复用的"插入列表视图"

**验收标准**：
- 空白画布 1-2 步内完成新增节点
- 连线上 1-2 步内完成插入节点
- Quick Insert 面板显示合理推荐

---

### 改动包 3：重新分工两层工具栏，降低认知负担

**文件**：
- `src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-workbench-toolbar.tsx`
- `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowToolbar.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx`

**变更内容**：
- 外层工作台工具栏只保留"文档级主动作"：保存、运行、调试运行、发布、撤销/重做、文档状态
- 视口/布局动作全部下沉为画布内浮动控制：缩放、适应视图、小地图、网格、平移模式、自动排版
- "节点工具箱""引用"等降为二级入口

**实现方式**：
- `mendix-studio-core` 负责文档与生命周期动作
- `mendix-microflow` 负责画布视口动作
- `index.tsx` 重新整理顶部栏和编辑器 body 的空间分配

**验收标准**：
- 顶部工具栏按钮数量减少，层次清晰
- 视口控制在画布内可访问
- 主要动作（保存/运行/发布）仍可完成

---

### 改动包 4：把左侧资源树变成"轻打扰、可召回"的辅助区

**文件**：
- `src/frontend/packages/mendix/mendix-studio-core/src/components/explorer-split-layout.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerTree.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerContainer.tsx`

**变更内容**：
- 微流设计器模式下，左侧默认保持图标栏式折叠
- 增加"临时展开"体验（点击展开、失焦后按策略收起、保留手动固定展开）
- `App Explorer` 顶部头部弱化，搜索/刷新更紧凑
- 搜索结果更偏"快速跳转资源"

**实现方式**：
- 保留现有 `ExplorerSplitLayout` 本地持久化逻辑
- 新增"固定/自动收起"状态
- `AppExplorerTree` 搜索行为继续保留 debounce，界面改为更紧凑

**验收标准**：
- 左侧资源树默认折叠不打断画布
- 可随时展开且不失焦后自动收起
- 资源搜索和跳转仍可用

---

### 改动包 5：保留专家路径，但下沉为增强层

**文件**：
- `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-command-palette.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/microflow/workbench/microflow-workbench-command-bus.ts`
- `src/frontend/packages/mendix/mendix-microflow/src/editor/shortcuts/useMicroflowShortcuts.ts`

**变更内容**：
- 保留 `Ctrl/Cmd+K`、快捷键、右键菜单，但定位改为专业用户增强入口
- 命令面板条目根据新主路径调整命名与分组
- 不再承载新手或主流程的核心可发现性

**实现方式**：
- 不新增额外命令层，继续使用既有 command bus
- 仅调整入口权重、命名、分组与触发点

**验收标准**：
- 快捷键和命令面板仍能完成专家操作
- 命令分组更合理（文档动作/画布动作/面板动作/跳转动作）

---

### 改动包 6：补齐交互回归与验收

**文件**：
- `src/frontend/e2e/app/mendix-studio-microflow-layout.spec.ts`
- `src/frontend/e2e/app/mendix-studio-microflow-create-save.spec.ts`
- `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.interaction.spec.ts`
- `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts`

**变更内容**：
- 新增或调整验收场景：
  - 单击节点自动出现属性主入口
  - 双击节点触发主编辑动作
  - 空白画布可直接快速添加节点
  - 连线上可直接插入节点
  - 左侧资源树可折叠、临时展开且不打断画布建模
  - 工具栏分层后，主要动作仍可完成
  - 快捷键/命令面板仍然可用

**验收标准**：
- 常见建模闭环缩短为：选中→右侧改属性、画布点击→快速加节点、连线处插入→自动/半自动接线
- "属性""新增节点"不再依赖右键菜单作为唯一主路径
- 左侧资源树与顶部工具栏从"持续打断"降为"按需辅助"

## Assumptions & Decisions

### 已锁定决策

- 以高频建模效率优先，而不是先做调试/资源管理优化
- 接受较大交互重构，但不改底层架构分层，不新增桥接业务层
- 保持 Mendix 相似风格，不做完全 Atlas 风格重造
- 属性面板必须继续是一等入口，并上升为默认主路径之一

### 关键实现决策

- 不新建新的业务适配层，所有交互重构直接落在现有 `mendix-studio-core`、`mendix-microflow`、既有 store/command bus/registry/editor handle
- Quick Insert 复用现有节点注册表与创建逻辑，不再做并行节点目录体系
- Explorer、Command Palette、Shortcut 都保留，但从"主入口"下调为"辅助入口/专家入口"

### 当前假设

- 本轮主要面向桌面端鼠标 + 键盘场景，不单独扩展触屏交互
- 当前登录态、API、微流 schema、metadata 链路无需改协议即可承接此次前端交互重构
- 因只读观察时命中登录页，实施后需要在真实登录态下补做页面级体验验证

## Verification Steps

### 代码与静态验证

- 变更前后对照阅读关键文件，确认入口与职责重排符合计划

### 必做命令

- 前端构建：`pnpm --dir src/frontend run build:app-web`
- 定向单测：
  - `pnpm --dir src/frontend exec vitest run src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.interaction.spec.ts`
  - `pnpm --dir src/frontend exec vitest run src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts`
  - 如涉及工作台壳组件，再补跑对应 `mendix-studio-core` 组件测试

### 必做 E2E

- `pnpm --dir src/frontend exec playwright test -c playwright.app.config.ts e2e/app/mendix-studio-microflow-layout.spec.ts`
- 如 create/save 主路径受影响，再补：
  - `pnpm --dir src/frontend exec playwright test -c playwright.app.config.ts e2e/app/mendix-studio-microflow-create-save.spec.ts`

### 必做人机验收

- 在真实登录态打开目标页面后，按以下路径实测：
  1. 打开微流后无需右键，即可通过选中节点进入属性主入口
  2. 在画布空白处 1-2 步内完成新增节点
  3. 在已存在连线中 1-2 步内完成插入节点
  4. 修改节点关键属性时，用户视线不需要频繁在左树、顶部栏、右键菜单间跳转
  5. 左侧资源树不会长期侵占画布，但可随时找回资源
  6. 保存、运行、发布、撤销/重做不丢失
  7. 快捷键和命令面板仍能完成专家操作
