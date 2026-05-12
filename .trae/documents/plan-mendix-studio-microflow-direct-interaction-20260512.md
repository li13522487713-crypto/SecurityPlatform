# Mendix Studio 微流画布直达交互优化计划

## Summary

- 目标页面：`/space/:workspaceId/mendix-studio/:appId`
- 优化目标：在保持 `Mendix` 风格心智模型的前提下，把微流设计器从“功能可用但路径分散”优化为“高频建模更快、更多在画布上直接完成、属性面板保持一等入口”。
- 用户已确认的决策：
  - 优先目标：高频建模更快
  - 改造幅度：较大重构
  - 交互风格：画布直达
  - 风格基线：Mendix 相似优先
  - 必须保留的一等入口：属性面板
- 本计划默认不删除现有能力，只重排主路径：右键、命令面板、快捷键、底部调试/问题面板仍保留，但不再作为高频建模的主入口。

## Current State Analysis

### 1. 页面与宿主现状

- 路由入口已稳定：`src/frontend/apps/app-web/src/app/pages/mendix-studio-route.tsx` 将 `workspaceId/appId/currentUser/adapterConfig` 注入 `MendixStudioApp`。
- Studio 主壳已具备真实工作台结构：`src/frontend/packages/mendix/mendix-studio-core/src/index.tsx`
  - 左侧 `App Explorer`
  - 中部 `WorkbenchTabs`
  - 顶部 `MicroflowWorkbenchToolbar`
  - 中部 `MicroflowResourceEditorHost`
  - 微流编辑器内部再管理节点面板、画布、属性面板、底部问题/调试面板
- 微流编辑器能力已相对完整：`src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`
  - 节点拖入/拖动/复制/删除
  - 画布缩放/平移/自动布局
  - 属性面板
  - Problems / Debug / Trace / Run
  - 快捷键与命令式工具栏调用

### 2. 当前核心交互阻力

#### A. 高价值动作过于分散

- 文档级动作在 `mendix-studio-core/src/components/microflow-workbench-toolbar.tsx`
- 视口级动作在 `mendix-microflow/src/flowgram/FlowGramMicroflowToolbar.tsx`
- 结构级动作散落于：
  - 右键菜单
  - 节点工具箱
  - 命令面板 `mendix-studio-core/src/components/workbench-command-palette.tsx`
  - 快捷键 `mendix-microflow/src/editor/shortcuts/useMicroflowShortcuts.ts`
- 结果：会做的人知道入口很多，不熟的人不知道先点哪里；高频操作路径过长。

#### B. 属性面板不是“默认主路径”

- 现有 E2E 明确反映：节点选中后，属性面板默认并不直接出现；需要通过右键菜单里的“属性/Properties”打开。
- 证据：
  - `src/frontend/e2e/app/mendix-studio-microflow-layout.spec.ts`
  - `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` 中大量 `openPropertiesPanel()` 调用主要发生在问题定位、调试定位、上下文行为，而不是普通选中主链路。
- 这与“属性面板是一等入口”的目标相冲突。

#### C. 画布缺少“直达式创建/编辑”主链路

- 当前新增节点的主路径还是左侧节点面板拖拽或双击添加：
  - `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx`
- 画布具备空白点击、节点右键、空白右键等事件承接能力，但没有围绕这些事件建立画布优先的“快速插入/快速编辑”入口：
  - `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.tsx`
- 结果：用户在“看图建模”时，频繁在左侧面板、顶部工具栏、右侧属性之间来回切换。

#### D. 工具栏负载偏高，但信息层次不够清晰

- `MicroflowWorkbenchToolbar` 同时承载保存、运行、调试运行、校验、发布、撤销/重做、引用、节点工具箱、更多菜单。
- `FlowGramMicroflowToolbar` 又承载平移、缩放、适应视图、网格、小地图、自动排版、状态标签。
- 两套工具栏都合理，但在主界面上叠加后，形成“动作多、认知重、上下文不够聚焦”的问题。

#### E. 左侧资源树对建模过程的打断较大

- `ExplorerSplitLayout` 在微流设计器模式下默认折叠，但仍以“完整资源树容器”思维存在：
  - `src/frontend/packages/mendix/mendix-studio-core/src/components/explorer-split-layout.tsx`
- `AppExplorerTree` 仍是典型树状浏览模型，适合找资源，不适合高频画布建模过程：
  - `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerTree.tsx`
- 搜索、刷新、树展开都偏资产管理，不是建模主路径。

### 3. 已有能力可直接复用

- 画布事件基础已足够：
  - 空白点击/空白右键
  - 节点上下文菜单
  - viewport 平移/缩放
  - selection change
- 属性面板能力已较完整，不需要重做表单体系：
  - `src/frontend/packages/mendix/mendix-microflow/src/property-panel`
- 快捷键、命令总线、工作台状态都已可复用：
  - `mendix-studio-core/src/microflow/workbench/microflow-workbench-command-bus.ts`
  - `mendix-microflow/src/editor/shortcuts/useMicroflowShortcuts.ts`
- 现有 E2E 覆盖已能承接交互重构后的回归：
  - `src/frontend/e2e/app/mendix-studio-microflow-layout.spec.ts`
  - `src/frontend/e2e/app/mendix-studio-microflow-create-save.spec.ts`

### 4. 运行时观察限制

- 对用户给出的本地 URL 做只读访问时，页面被登录守卫重定向到 `/sign`，因此本轮计划的页面体验判断主要依据：
  - 当前代码实现
  - 已有 E2E 用例
  - 仓库内设计/差距文档
- 该限制不影响方案设计，但实施后需要在已登录态下补做真实页面验证。

## Proposed Changes

### 方案总原则

- 保留 Mendix 风格分区：左资源、中央画布、右属性、下问题/调试。
- 重新定义主路径：高频建模优先使用“画布 + 右属性面板”，而不是“左工具箱 + 右键菜单 + 多处工具栏”。
- 不删除专家路径：快捷键、命令面板、右键菜单保留，但退居二级入口。
- 以“减少跨区往返”为核心验收标准，而不是单纯增加功能。

### 改动包 1：把微流编辑器改成真正的“画布优先、属性常驻上下文”

#### 文件

- `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`

#### 变更内容

- 把“节点选中后自动打开属性面板”改成默认主行为，而不是右键后的次级行为。
- 当未选中任何节点时，不再直接隐藏右侧区域；改为显示轻量空态：
  - 最近可执行动作
  - 当前微流摘要
  - 常见快捷提示
  - “选择节点开始编辑”引导
- 当选中节点/连线时：
  - 右侧属性面板自动切换到对应表单
  - 画布保持主体面积，不弹大面积遮挡层
- 双击节点时不再主要依赖右键菜单，而是触发“主编辑动作”：
  - 对可命名节点优先进入标题/名称快速编辑
  - 对动作节点优先打开首要配置字段

#### 为什么这样改

- 属性面板是用户要求保留的一等入口，最合理的方式不是“保留按钮”，而是“让它自然伴随选择出现”。
- 这样保留 Mendix 风格，同时把“选中 -> 看属性 -> 改配置”压缩成自然单链路。

#### 实现方式

- 调整编辑器内部布局状态机：
  - 选中对象时自动 `openPropertiesPanel()`
  - 清空选择时切换到“右侧空态”而不是完全关闭右侧区域
- 为节点双击增加主动作分发：
  - 命名类节点走 inline rename / 标题编辑
  - 结构类节点直接聚焦右侧首字段

### 改动包 2：把新增节点主路径从“左侧拖拽”升级为“画布就地插入”

#### 文件

- `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.tsx`
- `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`
- `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx`

#### 变更内容

- 新增三类画布直达入口：
  1. 空白画布点击后的“快速添加”入口
  2. 选中连线后的“在线插入节点”入口
  3. 节点 hover/选中时的邻接添加入口
- 新增统一“Quick Insert”面板：
  - 以搜索优先，而不是完整树优先
  - 默认显示最近使用 / 收藏 / 当前上下文推荐节点
  - 仍复用现有 registry，不新建桥接层
- 左侧 `Node Panel` 继续保留，但角色调整为“完整目录/浏览器”，不是高频主入口。

#### 为什么这样改

- 高频建模的瓶颈不是“没有节点”，而是“每次添加节点都要离开画布去找节点”。
- 画布上就地插入最符合“画图建模”的动作预期，也最符合你选择的“画布直达”方向。

#### 实现方式

- 基于 `FlowGramMicroflowNativeCanvas` 已有的：
  - `onCanvasBlankClick`
  - `onCanvasBlankContextMenu`
  - `onNodeContextMenu`
  - selection / edge / node position
- 在 `editor/index.tsx` 中新增统一的 quick insert 状态与提交逻辑，调用现有 `onAddNode` / `splitFlowWithObject` / `createObjectFromRegistry` 流程。
- `node-panel/index.tsx` 提炼搜索、收藏、推荐列表为可复用的“插入列表视图”，供左面板和画布 quick insert 共用，避免重复维护节点目录。

### 改动包 3：重新分工两层工具栏，降低认知负担

#### 文件

- `src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-workbench-toolbar.tsx`
- `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowToolbar.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx`

#### 变更内容

- 外层工作台工具栏只保留“文档级主动作”：
  - 保存
  - 运行
  - 调试运行
  - 发布
  - 撤销/重做
  - 文档状态
- 视口/布局动作全部下沉为画布内浮动控制：
  - 缩放
  - 适应视图
  - 小地图
  - 网格
  - 平移模式
  - 自动排版
- 将“节点工具箱”“引用”等从高权重标签降为二级入口，放入更多菜单或上下文入口。

#### 为什么这样改

- 当前不是功能缺，而是动作层次混杂。
- 一眼可见的主按钮越少，用户越快进入“编辑状态”。
- 文档级和视口级分层后，更接近成熟设计器的操作节奏。

#### 实现方式

- `mendix-studio-core` 负责文档与生命周期动作。
- `mendix-microflow` 负责画布视口动作。
- `index.tsx` 重新整理顶部栏和编辑器 body 的空间分配，避免用户感知为“双工具栏抢注意力”。

### 改动包 4：把左侧资源树变成“轻打扰、可召回”的辅助区

#### 文件

- `src/frontend/packages/mendix/mendix-studio-core/src/components/explorer-split-layout.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerTree.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerContainer.tsx`

#### 变更内容

- 微流设计器模式下，左侧默认保持图标栏式折叠。
- 增加“临时展开”体验：
  - 点击资源图标展开
  - 失焦后可按策略收起
  - 保留手动固定展开
- `App Explorer` 顶部头部弱化，把“刷新/搜索/资源树”改为更紧凑的信息密度。
- 搜索结果更偏“快速跳转资源”，而不是大树内查找。

#### 为什么这样改

- 左侧区域对建模是辅助，不应长期占据主注意力。
- 资源树要“随叫随到”，而不是“始终争夺空间”。

#### 实现方式

- 保留现有 `ExplorerSplitLayout` 本地持久化逻辑，新增“固定/自动收起”状态。
- `AppExplorerTree` 的搜索行为继续保留 debounce，但界面改为更紧凑，并把搜索结果与最近打开资源优先呈现。

### 改动包 5：保留专家路径，但下沉为增强层

#### 文件

- `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-command-palette.tsx`
- `src/frontend/packages/mendix/mendix-studio-core/src/microflow/workbench/microflow-workbench-command-bus.ts`
- `src/frontend/packages/mendix/mendix-microflow/src/editor/shortcuts/useMicroflowShortcuts.ts`

#### 变更内容

- 保留 `Ctrl/Cmd+K`、快捷键、右键菜单，但定位改为：
  - 专业用户增强入口
  - 不再承载新手或主流程的核心可发现性
- 命令面板条目根据新主路径调整命名与分组：
  - 文档动作
  - 画布动作
  - 面板动作
  - 跳转动作

#### 为什么这样改

- 不能为了直观而牺牲熟练用户效率。
- 但也不能再让主路径依赖“知道命令的人”。

#### 实现方式

- 不新增额外命令层，继续使用既有 command bus。
- 仅调整入口权重、命名、分组与触发点。

### 改动包 6：补齐交互回归与验收

#### 文件

- `src/frontend/e2e/app/mendix-studio-microflow-layout.spec.ts`
- `src/frontend/e2e/app/mendix-studio-microflow-create-save.spec.ts`
- `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.interaction.spec.ts`
- `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts`
- 如有必要：`src/frontend/packages/mendix/mendix-studio-core/src/components/*` 对应组件测试

#### 变更内容

- 新增或调整以下验收场景：
  - 单击节点自动出现属性主入口
  - 双击节点触发主编辑动作
  - 空白画布可直接快速添加节点
  - 连线上可直接插入节点
  - 左侧资源树可折叠、临时展开且不打断画布建模
  - 工具栏分层后，主要动作仍可完成保存/运行/发布
  - 快捷键/命令面板仍然可用

#### 为什么这样改

- 当前 E2E 更多在证明“能做”，下一轮必须同时证明“主路径变短且仍无回归”。

## Assumptions & Decisions

### 已锁定决策

- 以高频建模效率优先，而不是先做调试/资源管理优化。
- 接受较大交互重构，但不改底层架构分层，不新增桥接业务层。
- 保持 Mendix 相似风格，不做完全 Atlas 风格重造。
- 属性面板必须继续是一等入口，并上升为默认主路径之一。

### 关键实现决策

- 不新建新的业务适配层，所有交互重构直接落在现有：
  - `mendix-studio-core`
  - `mendix-microflow`
  - 既有 store / command bus / registry / editor handle
- Quick Insert 复用现有节点注册表与创建逻辑，不再做并行节点目录体系。
- Explorer、Command Palette、Shortcut 都保留，但从“主入口”下调为“辅助入口/专家入口”。

### 当前假设

- 本轮主要面向桌面端鼠标 + 键盘场景，不单独扩展触屏交互。
- 当前登录态、API、微流 schema、metadata 链路无需改协议即可承接此次前端交互重构。
- 因只读观察时命中登录页，实施后需要在真实登录态下补做页面级体验验证。

## Verification Steps

### 代码与静态验证

- 变更前后对照阅读以下关键文件，确认入口与职责重排符合计划：
  - `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx`
  - `src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-workbench-toolbar.tsx`
  - `src/frontend/packages/mendix/mendix-studio-core/src/components/explorer-split-layout.tsx`
  - `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerTree.tsx`
  - `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`
  - `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.tsx`
  - `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx`

### 必做命令

- 前端构建：
  - `pnpm --dir src/frontend run build:app-web`
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
  2. 在画布空白处 1 到 2 步内完成新增节点
  3. 在已存在连线中 1 到 2 步内完成插入节点
  4. 修改节点关键属性时，用户视线不需要频繁在左树、顶部栏、右键菜单间跳转
  5. 左侧资源树不会长期侵占画布，但可随时找回资源
  6. 保存、运行、发布、撤销/重做不丢失
  7. 快捷键和命令面板仍能完成专家操作

### 验收标准

- 常见建模闭环缩短为：
  - 选中 -> 右侧改属性
  - 画布点击 -> 快速加节点
  - 连线处插入 -> 自动接线/半自动接线
- “属性”“新增节点”不再依赖右键菜单作为唯一主路径。
- 左侧资源树与顶部工具栏从“持续打断”降为“按需辅助”。
