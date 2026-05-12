# SecurityPlatform 微流前端 Code Wiki

> 生成日期: 2026-05-13
> 范围: 微流(Microflow)前端编辑器完整架构与实现细节
> 包路径: `src/frontend/packages/mendix/mendix-microflow`

---

## 一、微流编辑器总览

### 1.1 定位与功能

微流编辑器是 Atlas 平台中的可视化逻辑编排工具，采用 Mendix 风格的流程图设计范式，支持：

- **画布拖拽编辑**：节点拖放、连线、正交路由
- **属性面板**：节点/连线/文档级别的属性编辑
- **节点面板**：分类搜索、拖拽添加节点
- **内联编辑**：节点上直接编辑关键字段
- **表达式编辑器**：CodeMirror 驱动的 Mendix 表达式编辑
- **调试系统**：断点、单步、变量查看、调用栈、执行追踪
- **自动布局**：Dagre 驱动的图形自动排列
- **历史管理**：Undo/Redo 操作栈

### 1.2 技术栈

| 层面 | 技术 |
|------|------|
| UI 框架 | React 18 + TypeScript |
| 画布引擎 | `@flowgram-adapter/free-layout-editor` + `@flowgram-adapter/common` |
| 渲染适配 | `@coze-workflow/render` |
| UI 组件库 | Semi Design (`@douyinfe/semi-ui`) |
| 表达式编辑 | CodeMirror 6 (`@codemirror/*`) |
| DI 容器 | Inversify (`inversify`) |
| 图片导出 | `html-to-image` |
| 包管理 | pnpm workspace (monorepo) |

### 1.3 页面路由

```
/microflow                           → MicroflowDemoPage (微流列表/演示)
/microflow/:microflowId/editor       → MicroflowEditorPage (微流编辑器)
```

页面入口组件链：

```
MicroflowEditorPage (app-web)
  └── MendixMicroflowEditorPage
       └── MicroflowEditor (@atlas/microflow/editor)
            └── FlowGramMicroflowProvider (画布渲染上下文)
                 ├── FlowGramMicroflowNativeCanvas (画布)
                 ├── FlowGramMicroflowToolbar (工具栏)
                 ├── NodePanel (节点面板)
                 ├── PropertyPanel (属性面板)
                 └── DebugPanels (调试面板)
```

---

## 二、模块架构与目录结构

```
mendix-microflow/src/
├── adapters/                    # 数据适配层
│   ├── microflow-adapters.ts    # 核心适配器：对象集合处理、变量索引、编辑图生成
│   ├── authoring-operations.ts  # 编辑操作：对象/流程增删改、复制、位置调整
│   ├── parameter-operations.ts  # 参数操作：增删改
│   └── auto-layout.ts           # 自动布局入口
│
├── flowgram/                    # 画布引擎层 (FlowGram 适配)
│   ├── FlowGramMicroflowNativeCanvas.tsx   # 画布主组件
│   ├── FlowGramMicroflowNodeRenderer.tsx   # 节点渲染器
│   ├── FlowGramMicroflowLineRenderer.tsx   # 连线渲染器
│   ├── FlowGramMicroflowPortRenderer.tsx   # 端口渲染器
│   ├── FlowGramMicroflowProvider.tsx       # 渲染上下文 Provider
│   ├── FlowGramMicroflowPlugins.ts         # 画布插件注册
│   ├── FlowGramMicroflowToolbar.tsx        # 工具栏
│   ├── FlowGramMicroflowStatusStrip.tsx    # 状态条
│   ├── FlowGramMicroflowNodeRegistries.ts  # FlowGram 节点注册表
│   ├── FlowGramMicroflowTypes.ts           # 核心类型定义
│   ├── FlowGramMicroflowContext.ts          # 运行时上下文
│   ├── FlowGramMicroflowEvents.ts          # 画布事件定义
│   ├── FlowGramNodeEditor.tsx              # 节点编辑器
│   ├── FlowGramNodeToolbar.tsx             # 节点工具栏
│   ├── flowgram-native-schema.ts           # Schema 创建
│   ├── flowgram-design-edge-semantics.ts   # 边语义设计
│   ├── flowgram-canvas-interactions.ts     # 画布交互（缩放/平移）
│   ├── flowgram-node-geometry.ts           # 节点几何计算
│   ├── flowgram-node-drag.ts              # 节点拖拽
│   ├── microflow-orthogonal-line.ts       # 正交连线算法
│   ├── runtime-edge-state.ts              # 运行时边状态
│   ├── transient-workflow-state.ts        # 瞬态工作流状态
│   │
│   ├── adapters/                # FlowGram 数据适配
│   │   ├── authoring-to-flowgram.ts       # Authoring → FlowGram 转换
│   │   ├── flowgram-to-authoring-patch.ts # FlowGram → Authoring 补丁
│   │   ├── flowgram-node-factory.ts       # 节点工厂
│   │   ├── flowgram-edge-factory.ts       # 边工厂
│   │   ├── flowgram-port-factory.ts       # 端口工厂
│   │   ├── flowgram-edge-mapping.ts       # 边映射
│   │   ├── flowgram-case-options.ts       # Case 选项
│   │   ├── flowgram-coordinate.ts         # 坐标转换
│   │   ├── flowgram-identity.ts           # ID 映射
│   │   ├── flowgram-selection-sync.ts     # 选择状态同步
│   │   └── flowgram-validation-sync.ts    # 验证状态同步
│   │
│   ├── hooks/                   # FlowGram Hooks
│   │   ├── use-flowgram-selection-sync.ts
│   │   └── use-flowgram-validation-sync.ts
│   │
│   ├── inline/                  # 画布内联编辑
│   │   ├── MicroflowInlineNodeEditor.tsx  # 内联节点编辑器
│   │   ├── MicroflowInlineRuntimeSummary.tsx
│   │   ├── useFlowGramMicroflowContext.ts
│   │   ├── useInlineEditorDraft.ts
│   │   └── useNodeVariableScope.ts
│   │
│   └── styles/                  # 画布样式
│       ├── flowgram-microflow-canvas.css
│       ├── flowgram-microflow-line.css
│       ├── flowgram-microflow-node.css
│       └── flowgram-microflow-port.css
│
├── node-registry/               # 节点注册表
│   ├── registry.ts              # 节点注册表主文件（30+ 节点定义）
│   ├── action-registry.ts       # 动作注册表（40+ 动作类型）
│   ├── edge-registry.ts         # 边注册表（5 种边类型 + 连接验证）
│   ├── factories.ts             # 节点/边工厂函数
│   ├── default-node-config.ts   # 默认节点配置
│   └── drag-drop.ts             # 拖拽放置逻辑
│
├── node-panel/                  # 节点面板
│   └── index.tsx                # 节点面板组件（搜索/筛选/分组/拖拽）
│
├── property-panel/              # 属性面板
│   ├── index.tsx                # 属性面板主组件
│   ├── node-form-registry.ts    # 表单注册表
│   ├── node-forms.tsx           # 节点表单集合
│   ├── controls.tsx             # 通用表单控件
│   ├── design-protocol-model.ts # 设计协议模型
│   ├── panel-shared.tsx         # 共享组件
│   │
│   ├── common/                  # 公共表单组件
│   │   ├── ErrorHandlingEditor.tsx
│   │   ├── FieldError.tsx
│   │   ├── FieldRow.tsx
│   │   ├── OutputVariableEditor.tsx
│   │   ├── RequiredMark.tsx
│   │   ├── ValidationIssueList.tsx
│   │   └── VariableNameInput.tsx
│   │
│   ├── expression/              # 表达式编辑
│   │   ├── ExpressionEditor.tsx
│   │   ├── ExpressionDiagnostics.tsx
│   │   └── expression-token-insert.ts
│   │
│   └── forms/                   # 各类型节点表单
│       ├── action-activity-form.tsx       # 动作活动表单
│       ├── annotation-object-form.tsx     # 注释表单
│       ├── error-handler-form.tsx         # 错误处理器表单
│       ├── event-nodes-form.tsx           # 事件节点表单
│       ├── exclusive-split-form.tsx       # 决策节点表单
│       ├── flow-edge-form.tsx             # 连线属性表单
│       ├── inclusive-gateway-form.tsx     # 包含网关表单
│       ├── inheritance-split-form.tsx     # 对象类型决策表单
│       ├── loop-node-form.tsx             # 循环节点表单
│       ├── merge-node-form.tsx            # 合并节点表单
│       ├── microflow-document-properties-form.tsx # 文档属性表单
│       ├── object-base-form.tsx           # 对象基础表单
│       ├── object-panel.tsx               # 对象面板
│       ├── parallel-gateway-form.tsx      # 并行网关表单
│       ├── parameter-object-form.tsx      # 参数表单
│       └── try-catch-form.tsx             # Try/Catch 表单
│
├── node-inline/                 # 内联编辑配置
│   ├── derive-node-inline-config.ts       # 内联配置派生（按节点类型分发）
│   ├── action-node-inline.ts              # 动作节点内联字段
│   ├── decision-node-inline.ts            # 决策节点内联字段
│   ├── loop-node-inline.ts               # 循环节点内联字段
│   ├── start-node-inline.ts              # 开始节点内联字段
│   ├── end-node-inline.ts                # 结束节点内联字段
│   ├── annotation-node-inline.ts          # 注释节点内联字段
│   ├── approval-node-inline.ts            # 审批节点内联字段
│   ├── call-microflow-node-inline.ts      # 调用微流内联字段
│   ├── error-node-inline.ts              # 错误节点内联字段
│   ├── rest-node-inline.ts               # REST 节点内联字段
│   ├── try-catch-node-inline.ts           # Try/Catch 内联字段
│   ├── variable-node-inline.ts            # 变量节点内联字段
│   ├── inline-field-paths.ts             # 字段路径定义
│   ├── inline-formatters.ts              # 格式化工具
│   ├── inline-runtime.ts                 # 运行时内联
│   ├── inline-validation.ts              # 内联验证
│   └── inline-variable-options.ts        # 变量选项
│
├── inline-edit/                 # 内联编辑组件
│   ├── InlineNodeEditor.tsx               # 内联节点编辑器
│   ├── InlineConditionEditor.tsx          # 条件编辑器
│   ├── InlineBranchEditor.tsx             # 分支编辑器
│   ├── InlineAssignmentEditor.tsx         # 赋值编辑器
│   ├── InlineExpressionField.tsx          # 表达式字段
│   ├── InlineHttpEditor.tsx               # HTTP 编辑器
│   ├── InlineJsonEditor.tsx               # JSON 编辑器
│   ├── InlineMappingEditor.tsx            # 映射编辑器
│   ├── InlineOutputMappingsEditor.tsx     # 输出映射编辑器
│   ├── InlineRuntimePreview.tsx           # 运行时预览
│   ├── InlineQuickFix.tsx                 # 快速修复
│   ├── InlineErrorBlock.tsx               # 错误块
│   ├── InlineSection.tsx                  # 区段组件
│   ├── InlineEditableText.tsx             # 可编辑文本
│   ├── InlineEditableSelect.tsx           # 可编辑选择
│   ├── InlineVariableField.tsx            # 变量字段
│   └── shared/
│       ├── ConditionBuilder.tsx           # 条件构建器
│       └── ContextVariablePicker.tsx      # 上下文变量选择器
│
├── components/                  # 共享 UI 组件
│   ├── ActivityNode.tsx                   # 活动节点组件
│   ├── AnnotationNode.tsx                 # 注释节点组件
│   ├── MicroflowEdge.tsx                  # 微流连线组件
│   ├── VariablePicker.tsx                 # 变量选择器
│   ├── DebugBreakpointsPanel.tsx          # 断点面板
│   ├── DebugCallStackPanel.tsx            # 调用栈面板
│   └── DebugVariablesPanel.tsx            # 变量面板
│
├── debug/                       # 调试系统
│   ├── index.ts                           # 调试模块导出
│   ├── step-debug-api.ts                  # 调试 API（会话/断点/变量/表达式）
│   ├── step-debug-ui.tsx                  # 调试 UI 组件
│   ├── debug-session-routing.ts           # 调试会话路由
│   ├── debug-status.ts                    # 调试状态管理
│   ├── debug-run-selection.ts             # 调试运行选择
│   ├── debug-routing.ts                   # 调试路由
│   ├── run-input-model.ts                 # 运行输入模型
│   ├── runtime-error-codes.ts             # 运行时错误码
│   ├── runtime-value-view-model.ts        # 运行时值视图模型
│   ├── node-io-view-model.ts              # 节点 IO 视图模型
│   ├── test-run-samples-store.ts          # 测试运行样本存储
│   ├── trace-highlighting.ts              # 追踪高亮
│   ├── trace-history-utils.ts             # 追踪历史工具
│   ├── trace-types.ts                     # 追踪类型
│   ├── trace-utils.ts                     # 追踪工具
│   ├── MicroflowTestRunModal.tsx          # 测试运行弹窗
│   ├── MicroflowTracePanel.tsx            # 追踪面板
│   └── MicroflowRunHistoryPanel.tsx       # 运行历史面板
│
├── editor/                      # 编辑器入口
│   ├── index.tsx                          # 编辑器主入口组件
│   ├── export-image.ts                    # 导出图片
│   ├── normalizer-issues.ts               # 规范化问题
│   ├── problem-quick-fixes.ts             # 问题快速修复
│   ├── runtime-command-consumer.ts        # 运行时命令消费
│   ├── ws-runtime-trace.ts                # WebSocket 运行时追踪
│   └── shortcuts/                         # 快捷键
│       ├── useMicroflowShortcuts.ts       # 快捷键 Hook
│       └── shortcut-utils.ts              # 快捷键工具
│
├── expression-editor/           # 表达式编辑器
│   └── index.tsx                          # CodeMirror 表达式编辑器
│
├── expressions/                 # 表达式引擎
│   ├── expression-ast.ts                  # AST 定义
│   ├── expression-parser.ts               # 解析器
│   ├── expression-tokenizer.ts            # 词法分析
│   ├── expression-validator.ts            # 验证器
│   ├── expression-type-inference.ts       # 类型推断
│   ├── expression-format.ts               # 格式化
│   ├── expression-reference-parser.ts     # 引用解析
│   ├── expression-types.ts                # 类型定义
│   └── expression-utils.ts                # 工具函数
│
├── history/                     # 历史管理
│   ├── microflow-history-manager.ts       # 历史管理器
│   ├── use-microflow-history.ts           # 历史 Hook
│   ├── history-types.ts                   # 历史类型
│   └── history-utils.ts                   # 历史工具
│
├── layout/                      # 自动布局
│   ├── auto-layout-engine.ts              # Dagre 布局引擎
│   ├── apply-auto-layout.ts               # 布局应用
│   └── auto-layout-types.ts               # 布局类型
│
├── metadata/                    # 元数据系统
│   ├── metadata-provider.tsx              # 元数据 Provider
│   ├── metadata-adapter.ts                # 元数据适配器
│   ├── metadata-hooks.ts                  # 元数据 Hooks
│   ├── metadata-catalog.ts                # 元数据目录
│   ├── metadata-query.ts                  # 元数据查询
│   ├── entity-catalog.ts                  # 实体目录
│   ├── enumeration-catalog.ts             # 枚举目录
│   ├── association-catalog.ts             # 关联目录
│   ├── microflow-catalog.ts               # 微流目录
│   ├── page-catalog.ts                    # 页面目录
│   ├── workflow-catalog.ts                # 工作流目录
│   └── mock-metadata.ts                   # Mock 元数据
│
├── performance/                 # 性能优化
│   ├── graph-index.ts                     # 图索引（加速查询）
│   ├── useDebouncedMicroflowValidation.ts # 防抖验证
│   └── large-sample-generator.ts          # 大规模样本生成
│
├── mendix-compat/               # Mendix 兼容层
│   └── index.ts
│
├── i18n/                        # 国际化
│   └── copy.ts                            # 文案定义
│
└── hooks/                       # 通用 Hooks
    ├── use-debug-ws.ts                    # 调试 WebSocket Hook
    └── use-debug-ws.spec.tsx
```

---

## 三、画布系统

### 3.1 画布主组件

**文件**: [FlowGramMicroflowNativeCanvas.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.tsx)

画布是微流编辑器的核心交互区域，基于 `@flowgram-adapter/free-layout-editor` 引擎实现。

```typescript
interface FlowGramMicroflowNativeCanvasProps {
  schema: MicroflowSchema;              // 微流数据模型
  validationIssues?: MicroflowValidationIssue[];  // 验证问题
  runtimeTrace?: MicroflowRuntimeTrace;  // 运行时追踪
  nodeViewModes?: Record<string, MicroflowNodeViewMode>;  // 节点视图模式
  onSchemaChange?: (schema: MicroflowSchema, reason: FlowGramMicroflowChangeReason) => void;
  onSelectionChange?: (selection: FlowGramMicroflowSelection) => void;
  onPendingLineChange?: (line: FlowGramMicroflowPendingLine | null) => void;
  readOnly?: boolean;
}
```

**核心职责**:
- 渲染 FlowGram 画布引擎
- 管理节点/边的增删改操作
- 处理画布交互（缩放、平移、框选）
- 同步选择状态和验证状态

### 3.2 画布交互

**文件**: [flowgram-canvas-interactions.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/flowgram-canvas-interactions.ts)

| 功能 | 说明 |
|------|------|
| 缩放 | 鼠标滚轮缩放，支持 Ctrl+滚轮精确缩放 |
| 平移 | 鼠标拖拽画布空白区域平移 |
| 框选 | 鼠标拖拽选择多个节点 |
| 适应画布 | 自动调整视口以显示所有节点 |
| 导出图片 | 将画布导出为 PNG |

### 3.3 画布 Provider

**文件**: [FlowGramMicroflowProvider.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowProvider.tsx)

通过 `WorkflowRenderProvider` 提供画布渲染上下文，注册 `FlowGramMicroflowContainerModule` 和 `createMicroflowFlowGramPreset` 插件。

### 3.4 画布事件

**文件**: [FlowGramMicroflowEvents.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowEvents.ts)

定义画布交互事件，包含 `canAddLine` 方法判断是否允许创建连线（基于源/目标端口禁用状态、节点 ID、父子关系等条件）。

### 3.5 正交连线

**文件**: [microflow-orthogonal-line.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/microflow-orthogonal-line.ts)

所有连线强制使用正交模式 (`lineKind: "orthogonal"`)，通过 `forceOrthogonalLineKind` 和 `canonicalizeFlowLine` 函数确保连线按正交方式绘制。

---

## 四、节点系统

### 4.1 节点类型总览

微流支持以下节点类型，每种类型有对应的 `MicroflowObjectKind` 和 Mendix 官方类型：

| 注册类型 | ObjectKind | 官方类型 | 中文名 | 分组 | 形状 |
|---------|-----------|---------|-------|------|------|
| `startEvent` | `startEvent` | `Microflows$StartEvent` | 开始事件 | Events | event (圆形) |
| `endEvent` | `endEvent` | `Microflows$EndEvent` | 结束事件 | Events | event (圆形) |
| `errorEvent` | `errorEvent` | `Microflows$ErrorEvent` | 错误事件 | Events | event (圆形) |
| `breakEvent` | `breakEvent` | `Microflows$BreakEvent` | 中断事件 | Events | event (圆形) |
| `continueEvent` | `continueEvent` | `Microflows$ContinueEvent` | 继续事件 | Events | event (圆形) |
| `decision` | `exclusiveSplit` | `Microflows$ExclusiveSplit` | 决策 | Decisions | diamond (菱形) |
| `objectTypeDecision` | `inheritanceSplit` | `Microflows$InheritanceSplit` | 对象类型决策 | Decisions | diamond (菱形) |
| `merge` | `exclusiveMerge` | `Microflows$ExclusiveMerge` | 合并 | Decisions | diamond (菱形) |
| `parallelGateway` | `parallelGateway` | `Microflows$ParallelGateway` | 并行网关 | Decisions | diamond (菱形) |
| `inclusiveGateway` | `inclusiveGateway` | `Microflows$InclusiveGateway` | 包含网关 | Decisions | diamond (菱形) |
| `loop` | `loopedActivity` | `Microflows$LoopedActivity` | 循环 | Loop | loop (矩形+标记) |
| `parameter` | `parameterObject` | `Microflows$MicroflowParameterObject` | 输入参数 | Parameters | roundedRect |
| `annotation` | `annotation` | `Microflows$Annotation` | 注释 | Annotations | annotation |
| `activity` | `actionActivity` | `Microflows$ActionActivity` | 动作活动 | Activities | roundedRect |
| `tryCatch` | `tryCatch` | `Microflows$TryCatch` | 捕获异常 | Activities | roundedRect |
| `errorHandler` | `errorHandler` | `Microflows$ErrorHandler` | 错误处理器 | Activities | roundedRect |

### 4.2 动作活动类型 (Activity Types)

动作活动是最丰富的节点类型，按类别分组：

| 类别 | 动作类型 | 中文名 | 引擎支持 |
|------|---------|-------|---------|
| **对象操作** | `objectCreate` | 创建对象 | ✅ supported |
| | `objectChange` | 修改对象 | ✅ supported |
| | `objectCommit` | 提交对象 | ✅ supported |
| | `objectDelete` | 删除对象 | ✅ supported |
| | `objectRetrieve` | 检索对象 | ✅ supported |
| | `objectRollback` | 回滚对象 | ✅ supported |
| | `objectCast` | 转换对象 | ✅ supported |
| **列表操作** | `listCreate` | 创建列表 | ✅ supported |
| | `listChange` | 修改列表 | ✅ supported |
| | `listAggregate` | 列表聚合 | ✅ supported |
| | `listOperation` | 列表操作 | ✅ supported |
| **变量操作** | `variableCreate` | 创建变量 | ✅ supported |
| | `variableChange` | 修改变量 | ✅ supported |
| **调用操作** | `callMicroflow` | 调用微流 | ✅ supported |
| | `callJavaAction` | 调用 Java 动作 | ⚠️ partial |
| **集成操作** | `callRest` | 调用 REST 服务 | ⚠️ partial |
| | `callWebService` | 调用 Web Service | ⚠️ partial |
| | `importWithMapping` | 使用映射导入 | ⚠️ partial |
| | `exportWithMapping` | 使用映射导出 | ⚠️ partial |
| **客户端操作** | `showPage` | 显示页面 | ✅ supported (无错误处理) |
| | `showMessage` | 显示消息 | ✅ supported (无错误处理) |
| | `closePage` | 关闭页面 | ✅ supported (无错误处理) |
| | `downloadFile` | 下载文件 | ✅ supported (无错误处理) |
| **日志** | `logMessage` | 记录日志 | ✅ supported (无错误处理) |
| **工作流** | `callWorkflow` | 调用工作流 | ⚠️ partial |
| | `completeUserTask` | 完成用户任务 | ⚠️ partial |
| **ML** | `callMlModel` | 调用 ML 模型 | ⚠️ partial |
| **指标** | `counter` / `incrementCounter` / `gauge` | 计数器/仪表 | ✅ supported (无错误处理) |

### 4.3 节点注册表

**文件**: [registry.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts)

核心数据结构：

```typescript
interface MicroflowNodeRegistryEntry<TConfig = Record<string, unknown>> {
  key: string;                              // 唯一标识
  type: MicroflowRegistryNodeType;          // 注册类型
  kind: MicroflowRegistryNodeKind;          // 节点种类
  objectKind: MicroflowObjectKind;          // 对象类型
  officialType: string;                     // Mendix 官方类型
  title: string;                            // 英文标题
  titleZh: string;                          // 中文标题
  description: string;                      // 描述
  category: MicroflowNodeCategory;          // 分类
  group: "Events" | "Decisions" | "Activities" | "Loop" | "Parameters" | "Annotations";
  iconKey: string;                          // 图标键
  availability: MicroflowNodeAvailability;  // 可用性
  ports: MicroflowPort[];                   // 端口列表
  defaultConfig: TConfig;                   // 默认配置
  supportsErrorHandling: boolean;           // 是否支持错误处理
  supportedErrorHandlingTypes: MicroflowErrorHandlingType[];
  engineSupport: MicroflowEngineSupport;    // 引擎支持等级
  propertyTabs: MicroflowPropertyTabKey[];  // 属性面板标签页
  render: MicroflowRenderMetadata;          // 渲染元数据
  propertyForm: MicroflowPropertyFormMetadata;  // 属性表单元数据
  validate: (node) => MicroflowValidationIssue[];
  toRuntimeDto: (node) => MicroflowRuntimeNodeDto;
}
```

**引擎支持等级**:

| 等级 | 含义 |
|------|------|
| `supported` | 运行时引擎已对接具体 executor，testRun 可真实执行 |
| `partial` | 需要 connector 或受限模式工作，运行时可能返回错误 |
| `unsupported` | 运行时引擎显式不支持，testRun 抛 `RUNTIME_UNSUPPORTED_ACTION` |

### 4.4 节点端口定义

每种节点类型定义了标准端口：

```typescript
// 通用端口
const sequenceIn  = { id: "in",  label: "In",  direction: "input",  kind: "sequenceIn",  cardinality: "one" }
const sequenceOut = { id: "out", label: "Out", direction: "output", kind: "sequenceOut", cardinality: "one" }

// 决策端口
const decisionTrue  = { id: "true",  label: "True",  direction: "output", kind: "decisionOut", cardinality: "one" }
const decisionFalse = { id: "false", label: "False", direction: "output", kind: "decisionOut", cardinality: "one" }

// 对象类型端口
const objectTypeOut = { id: "objectType", label: "Object Type", direction: "output", kind: "objectTypeOut", cardinality: "oneOrMore" }

// 错误端口
const errorOut = { id: "error", label: "Error", direction: "output", kind: "errorOut", cardinality: "zeroOrOne" }

// 注释端口
const annotationOut = { id: "note",   label: "Note",   direction: "output", kind: "annotation", cardinality: "zeroOrMore" }
const annotationIn  = { id: "noteIn", label: "Note",   direction: "input",  kind: "annotation", cardinality: "zeroOrMore" }
```

**各节点端口配置**:

| 节点 | 输入端口 | 输出端口 |
|------|---------|---------|
| StartEvent | - | sequenceOut |
| EndEvent | sequenceIn | - |
| ErrorEvent | sequenceIn | - |
| BreakEvent | sequenceIn | - |
| ContinueEvent | sequenceIn | - |
| Decision | sequenceIn | decisionTrue, decisionFalse, errorOut |
| ObjectTypeDecision | sequenceIn | objectTypeOut, errorOut |
| Merge | sequenceIn | sequenceOut |
| Loop | sequenceIn | sequenceOut, loopBodyIn(output), loopBodyOut(input), errorOut |
| Parameter | annotationIn | annotationOut |
| Annotation | annotationIn | annotationOut |
| ActionActivity | sequenceIn | sequenceOut, errorOut(可选) |
| ParallelGateway | sequenceIn | branch(sequenceOut, oneOrMore) |
| InclusiveGateway | sequenceIn | branch(decisionCondition, oneOrMore) |
| TryCatch | sequenceIn | try(sequenceOut), catch(errorOut), finally(sequenceOut) |
| ErrorHandler | sequenceIn | sequenceOut, errorOut |

### 4.5 节点渲染器

**文件**: [FlowGramMicroflowNodeRenderer.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNodeRenderer.tsx)

`FlowGramMicroflowNodeRendererInner` 函数根据节点类型渲染不同的视觉样式：

- **事件节点** (startEvent/endEvent/errorEvent/breakEvent/continueEvent)：圆形，不同色调
- **决策节点** (exclusiveSplit/inheritanceSplit/exclusiveMerge/gateway)：菱形
- **活动节点** (actionActivity)：圆角矩形，支持背景色自定义
- **循环节点** (loopedActivity)：带循环标记的容器
- **参数节点** (parameterObject)：带类型标签的圆角矩形
- **注释节点** (annotation)：无边框文本区域

渲染状态：
- `idle`：默认状态
- `success`：执行成功（绿色标记）
- `visited`：已访问（浅色标记）
- `running`：正在执行（动画脉冲）
- `paused`：调试暂停（黄色边框）
- `failed`：执行失败（红色标记）
- `skipped`：跳过（灰色标记）

### 4.6 节点视图模式

```typescript
type MicroflowNodeViewMode =
  | "compact"          // 紧凑模式：仅显示标题和图标
  | "expanded"         // 展开模式：显示摘要行
  | "editing"          // 编辑模式：显示内联编辑表单
  | "running"          // 运行模式：显示运行时状态
  | "inspectingError"  // 错误检查模式
  | "inspectingRuntime" // 运行时检查模式
```

---

## 五、连线系统

### 5.1 边类型定义

**文件**: [edge-registry.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/node-registry/edge-registry.ts)

微流定义了 5 种边类型：

| 边类型 | 中文名 | 官方类型 | 线型 | 颜色 | 箭头 | 标签模式 | 运行时效果 |
|--------|-------|---------|------|------|------|---------|-----------|
| `sequence` | 顺序流 | `Microflows$SequenceFlow` | 实线 | `#4e5969` | ✅ | none | 控制流 |
| `decisionCondition` | 决策条件流 | `Microflows$SequenceFlow` | 实线 | `#165dff` | ✅ | condition | 控制流 |
| `objectTypeCondition` | 对象类型条件流 | `Microflows$SequenceFlow` | 实线 | `#722ed1` | ✅ | condition | 控制流 |
| `errorHandler` | 错误处理流 | `Microflows$SequenceFlow` | 虚线 | `#f93920` | ✅ | auto | 错误流 |
| `annotation` | 注释流 | `Microflows$AnnotationFlow` | 虚线 | `#86909c` | ❌ | editable | 仅注释 |

### 5.2 连线规则

#### 端口兼容性矩阵

| 边类型 | 源端口类型 | 目标端口类型 |
|--------|-----------|-----------|
| 顺序流 | `sequenceOut`, `loopOut` | `sequenceIn`, `loopIn` |
| 决策条件流 | `decisionOut` | `sequenceIn`, `loopIn` |
| 对象类型条件流 | `objectTypeOut` | `sequenceIn`, `loopIn` |
| 错误处理流 | `errorOut` | `sequenceIn` |
| 注释流 | `annotation` | `annotation`, `sequenceIn`, `sequenceOut` |

#### 连接验证规则

`canConnectPortsV2` 函数执行以下验证（按顺序）：

| 错误码 | 规则 | 说明 |
|--------|------|------|
| `MF_CONNECT_SOURCE_DIRECTION` | 源端口必须是输出端口 | direction === "output" |
| `MF_CONNECT_TARGET_DIRECTION` | 目标端口必须是输入端口 | direction === "input" |
| `MF_CONNECT_SELF_LOOP` | 禁止自连接 | 源和目标不能是同一节点 |
| `MF_CONNECT_OBJECT_MISSING` | 对象必须存在 | 源/目标节点在 schema 中必须存在 |
| `MF_CONNECT_LOOP_BOUNDARY` | 禁止跨循环边界连线 | 流不能直接跨越 Loop objectCollection 边界 |
| `MF_CONNECT_LOOP_BODY_RETURN_UNSUPPORTED` | 禁止循环体返回连线 | 循环体必须通过 Break/Continue 退出 |
| `MF_CONNECT_START_TARGET` | StartEvent 不能有入边 | 开始事件不能作为连线目标 |
| `MF_CONNECT_TERMINAL_SOURCE` | 终端事件不能有出边 | endEvent/errorEvent/breakEvent/continueEvent 不能作为连线源 |
| `MF_CONNECT_PARAMETER_SEQUENCE` | 参数对象不能参与执行流 | ParameterObject 只能使用注释流 |
| `MF_CONNECT_ANNOTATION_ENDPOINT` | 注释流至少一端必须是注释 | AnnotationFlow 需要连接 Annotation |
| `MF_CONNECT_ANNOTATION_SEQUENCE` | 注释只能使用注释流 | Annotation 节点不能使用执行流 |
| `MF_CONNECT_ERROR_EVENT_TARGET` | ErrorEvent 只能通过错误处理流到达 | 错误事件只能从 errorHandler 边到达 |
| `MF_CONNECT_ERROR_UNSUPPORTED` | 源对象不支持错误处理 | 只有 actionActivity/loopedActivity/exclusiveSplit/inheritanceSplit 支持 |
| `MF_CONNECT_ERROR_DUPLICATED` | 每个源对象最多一条错误处理流 | P0 阶段限制 |
| `MF_CONNECT_DECISION_SOURCE` | 决策条件流必须从 ExclusiveSplit 开始 | 只有决策节点能产生决策条件流 |
| `MF_CONNECT_OBJECT_TYPE_SOURCE` | 对象类型条件流必须从 InheritanceSplit 开始 | 只有对象类型决策节点能产生对象类型条件流 |
| `MF_CONNECT_SOURCE_CARDINALITY` | 源端口基数限制 | cardinality === "one" 时只能有一条出边 |
| `MF_CONNECT_TARGET_CARDINALITY` | 目标端口基数限制 | cardinality === "one" 时只能有一条入边 |
| `MF_CONNECT_DECISION_CASE_DUPLICATED` | 决策分支去重 | 同一决策节点的 True/False 分支不能重复 |
| `MF_CONNECT_OBJECT_TYPE_CASE_DUPLICATED` | 对象类型分支去重 | 同一继承分支不能重复 |

#### 边类型推断

`inferEdgeKindFromPorts` 函数根据源节点和端口类型自动推断边类型：

```
源端口为 annotation → annotation
源端口为 errorOut → errorHandler
源节点为 loopedActivity 且端口为 loopBodyIn → loopBody
源节点为 exclusiveSplit 或端口为 decisionOut → decisionCondition
源节点为 inheritanceSplit 或端口为 objectTypeOut → objectTypeCondition
其他 → sequence
```

### 5.3 边数据结构

```typescript
interface FlowGramMicroflowEdgeData {
  flowId: string;                    // 流 ID
  flowKind: "sequence" | "annotation";  // 流种类
  edgeKind: MicroflowDerivedEdgeKind;   // 推断的边类型
  isErrorHandler: boolean;           // 是否为错误处理流
  caseValues: MicroflowCaseValue[];  // 分支条件值
  lineKind?: "orthogonal";           // 线型（强制正交）
  label?: string;                    // 标签文本
  branchOrder?: number;              // 分支顺序
  runtimeState?: "idle" | "visited" | "running" | "failed" | "skipped" | "errorHandlerVisited" | "selectedCase";
  validationState: "valid" | "warning" | "error";
  sourceNodeId?: string;
  sourceObjectKind?: MicroflowObjectKind;
  sourcePortId?: string;
  targetNodeId?: string;
  targetObjectKind?: MicroflowObjectKind;
  targetPortId?: string;
}
```

### 5.4 连线渲染器

**文件**: [FlowGramMicroflowLineRenderer.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowLineRenderer.tsx)

- 根据边类型渲染不同颜色和线型
- 支持标签编辑（决策条件流的 True/False/else 标签）
- 运行时状态高亮（visited/running/failed）
- 点击选中进入属性面板编辑

---

## 六、节点面板

### 6.1 面板组件

**文件**: [node-panel/index.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx)

节点面板提供节点的搜索、筛选、分组和拖拽添加功能。

### 6.2 节点分类

```typescript
type MicroflowNodePanelCategoryKey =
  | "events"        // 事件
  | "inputs"        // 输入
  | "flowControl"   // 流程控制
  | "loops"         // 循环
  | "variables"     // 变量
  | "objects"       // 对象
  | "lists"         // 列表/集合
  | "integration"   // 集成
  | "documentation" // 文档
  | "other"         // 其他
```

### 6.3 面板状态

```typescript
interface MicroflowNodePanelState {
  activeTab: "nodes" | "components" | "templates";  // 活动标签页
  keyword: string;                                    // 搜索关键词
  filterKey: MicroflowNodeFilterKey;                  // 筛选键
  expandedCategories: string[];                       // 展开的分类
  expandedGroups: string[];                           // 展开的分组
}
```

### 6.4 拖拽机制

**文件**: [drag-drop.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/node-registry/drag-drop.ts)

```typescript
interface MicroflowNodeDragPayload {
  dragType: "microflow-node";
  source: "node-panel";
  registryKind: "node" | "action";
  registryId?: string;
  nodeType: MicroflowRegistryNodeType;
  objectKind: MicroflowObjectKind;
  activityType?: MicroflowActivityType;
  actionKind?: MicroflowActionKind;
  registryKey: string;
  title: string;
  titleZh?: string;
  availability: MicroflowNodeAvailability;
}
```

拖拽流程：
1. 从节点面板拖出节点 → 生成 `MicroflowNodeDragPayload`
2. 拖到画布上释放 → 调用 `createObject` 创建节点实例
3. 根据注册表项的 `defaultConfig` 初始化节点配置
4. 自动连接到最近的可用端口

---

## 七、属性面板

### 7.1 面板主组件

**文件**: [property-panel/index.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/property-panel/index.tsx)

属性面板根据选择对象类型显示不同的表单：

- **未选中任何对象** → 显示微流文档属性 (`MicroflowDocumentPropertiesForm`)
- **选中连线** → 显示连线属性 (`FlowEdgeForm`)
- **选中节点** → 显示节点属性 (`ObjectPanel`)

### 7.2 表单注册表

**文件**: [node-form-registry.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/property-panel/node-form-registry.ts)

```typescript
// 表单 Key 生成规则
function getMicroflowNodeFormKey(object: MicroflowObject): string {
  return object.kind === "actionActivity"
    ? `activity:${object.action.kind}`  // 动作节点按 actionKind 区分
    : object.kind;                       // 其他节点按 objectKind
}

// 注册表 API
registerMicroflowNodeForm(key, item, options?)  // 注册表单
unregisterMicroflowNodeForm(key)                 // 注销表单
getMicroflowNodeFormForObject(object)            // 获取表单
```

### 7.3 表单类型一览

| 表单组件 | 对应节点 | 文件 |
|---------|---------|------|
| `ActionActivityForm` | 动作活动节点 | `action-activity-form.tsx` |
| `EventNodesForm` | 事件节点 (start/end/error/break/continue) | `event-nodes-form.tsx` |
| `ExclusiveSplitForm` | 决策节点 | `exclusive-split-form.tsx` |
| `InheritanceSplitForm` | 对象类型决策节点 | `inheritance-split-form.tsx` |
| `MergeNodeForm` | 合并节点 | `merge-node-form.tsx` |
| `LoopNodeForm` | 循环节点 | `loop-node-form.tsx` |
| `ParameterObjectForm` | 参数节点 | `parameter-object-form.tsx` |
| `AnnotationObjectForm` | 注释节点 | `annotation-object-form.tsx` |
| `ParallelGatewayForm` | 并行网关 | `parallel-gateway-form.tsx` |
| `InclusiveGatewayForm` | 包含网关 | `inclusive-gateway-form.tsx` |
| `TryCatchForm` | Try/Catch 节点 | `try-catch-form.tsx` |
| `ErrorHandlerForm` | 错误处理器节点 | `error-handler-form.tsx` |
| `FlowEdgeForm` | 连线 | `flow-edge-form.tsx` |
| `MicroflowDocumentPropertiesForm` | 微流文档 | `microflow-document-properties-form.tsx` |
| `ObjectBaseForm` | 对象基础属性 | `object-base-form.tsx` |
| `GenericActionFieldsForm` | 通用动作字段 | `generic-action-fields-form.tsx` |

### 7.4 通用表单控件

**文件**: [controls.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/property-panel/controls.tsx)

| 控件 | 说明 |
|------|------|
| `ExpressionEditor` | Mendix 表达式编辑器 |
| `FieldLabel` | 字段标签（含必填标记） |
| `FieldRow` | 字段行布局 |
| `KeyValueEditor` | 键值对编辑器 |
| `VariableNameInput` | 变量名输入框 |
| `ErrorHandlingEditor` | 错误处理策略编辑器 |
| `OutputVariableEditor` | 输出变量编辑器 |
| `ValidationIssueList` | 验证问题列表 |

### 7.5 设计协议模型

**文件**: [design-protocol-model.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/property-panel/design-protocol-model.ts)

提供属性面板操作的核心数据逻辑：

| 函数 | 说明 |
|------|------|
| `buildDesignPropertyPanelModel` | 构建属性面板模型 |
| `applyDesignObjectPatch` | 应用节点属性补丁 |
| `applyDesignFlowPatch` | 应用连线属性补丁 |
| `applyDesignDocumentSchema` | 应用文档属性补丁 |
| `deleteDesignObject` | 删除节点 |
| `deleteDesignFlow` | 删除连线 |
| `duplicateDesignObject` | 复制节点 |

---

## 八、内联编辑系统

### 8.1 内联编辑配置

**文件**: [derive-node-inline-config.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/node-inline/derive-node-inline-config.ts)

根据节点类型派生内联编辑配置，每种节点类型定义了不同的内联字段：

| 节点类型 | 内联字段 |
|---------|---------|
| StartEvent | 输入参数（名称、类型、是否必需） |
| EndEvent | 返回值表达式、状态信息 |
| Decision | 条件表达式、逻辑操作符、分支标签 |
| Loop | 集合变量、迭代器变量、索引变量、分支标签 |
| ActionActivity | 输入表达式、输入映射、输出变量、错误变量 |
| Annotation | 注释文本 |
| TryCatch | Try/Catch/Finally 分支、错误变量 |
| CallMicroflow | 目标微流、参数映射 |
| RestCall | URL、HTTP 方法、请求/响应映射 |

### 8.2 内联编辑类型

```typescript
type MicroflowInlineEditType =
  | "text"           // 文本输入
  | "select"         // 下拉选择
  | "variable"       // 变量选择
  | "expression"     // 表达式编辑
  | "condition"      // 条件构建
  | "http"           // HTTP 请求编辑
  | "assignment"     // 赋值编辑
  | "branch"         // 分支编辑
  | "json"           // JSON 编辑
  | "mapping"        // 映射编辑
  | "approval"       // 审批编辑
  | "loop"           // 循环编辑
  | "outputMappings" // 输出映射编辑
```

### 8.3 内联区段

```typescript
interface MicroflowInlineSection {
  id: string;
  title: string;
  kind: "inputs" | "outputs" | "conditions" | "branches" | "variables"
      | "http" | "approval" | "loop" | "runtime" | "errors" | "advanced";
  collapsed?: boolean;
  maxVisibleRows?: number;
  fields: MicroflowInlineEditableField[];
}
```

---

## 九、表达式引擎

### 9.1 表达式编辑器

**文件**: [expression-editor/index.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/expression-editor/index.tsx)

基于 CodeMirror 6 的 Mendix 表达式编辑器，支持语法高亮、自动补全、错误诊断。

```typescript
interface MicroflowExpressionEditorProps {
  value: string;                    // 表达式值
  expectedType?: string;            // 期望类型
  readOnly?: boolean;               // 只读模式
  onChange?: (value: string) => void;  // 变更回调
  variables?: MicroflowVariableSymbol[];  // 可用变量列表
}
```

### 9.2 表达式引擎模块

**文件**: [expressions/](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/expressions/)

| 模块 | 说明 |
|------|------|
| `expression-tokenizer.ts` | 词法分析：将表达式文本拆分为 Token 流 |
| `expression-parser.ts` | 语法分析：将 Token 流构建为 AST |
| `expression-ast.ts` | AST 节点定义 |
| `expression-validator.ts` | 语义验证：类型检查、变量引用检查 |
| `expression-type-inference.ts` | 类型推断：推断表达式的返回类型 |
| `expression-format.ts` | 格式化：表达式文本的美化输出 |
| `expression-reference-parser.ts` | 引用解析：提取表达式中引用的变量 |
| `expression-types.ts` | 类型系统定义 |
| `expression-utils.ts` | 工具函数 |

---

## 十、调试系统

### 10.1 调试 API

**文件**: [step-debug-api.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/debug/step-debug-api.ts)

| 方法 | 说明 |
|------|------|
| `createSession` | 创建调试会话 |
| `sendCommand` | 发送调试命令（继续/单步/停止） |
| `upsertBreakpoint` | 添加/更新断点 |
| `removeBreakpoint` | 移除断点 |
| `getVariables` | 获取当前变量快照 |
| `evaluateExpression` | 在调试上下文中评估表达式 |
| `getCallStack` | 获取调用栈 |

### 10.2 调试面板

| 组件 | 说明 |
|------|------|
| `DebugBreakpointsPanel` | 断点管理面板 |
| `DebugCallStackPanel` | 调用栈面板 |
| `DebugVariablesPanel` | 变量查看面板 |
| `MicroflowTestRunModal` | 测试运行弹窗 |
| `MicroflowTracePanel` | 执行追踪面板 |
| `MicroflowRunHistoryPanel` | 运行历史面板 |

### 10.3 运行时错误码

**文件**: [runtime-error-codes.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/debug/runtime-error-codes.ts)

定义微流运行时可能返回的错误码，用于 UI 错误展示和快速修复建议。

---

## 十一、数据适配层

### 11.1 核心适配器

**文件**: [microflow-adapters.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/adapters/microflow-adapters.ts)

| 函数 | 说明 |
|------|------|
| `buildVariableIndex` | 构建变量索引（名称→变量定义映射） |
| `buildObjectTypeMap` | 构建对象类型映射 |
| `computeObjectPositions` | 计算对象位置 |
| `findObjectById` | 按 ID 查找对象 |
| `generateEditGraph` | 生成编辑图（用于差异比较） |
| `applyPatch` | 应用补丁到 Schema |
| `toMendixCompat` | 转换为 Mendix 兼容格式 |
| `fromMendixCompat` | 从 Mendix 兼容格式转换 |

### 11.2 编辑操作

**文件**: [authoring-operations.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts)

| 函数 | 说明 |
|------|------|
| `addObject` | 添加对象到 Schema |
| `deleteObject` | 从 Schema 删除对象 |
| `updateObject` | 更新对象属性 |
| `copyObject` | 复制对象 |
| `addFlow` | 添加连线 |
| `deleteFlow` | 删除连线 |
| `updateFlow` | 更新连线属性 |
| `adjustPosition` | 调整对象位置 |
| `addParameter` | 添加参数 |
| `renameParameter` | 重命名参数 |
| `deleteParameter` | 删除参数 |
| `refreshVariableIndex` | 刷新变量索引 |
| `updateView` | 更新视图状态 |

### 11.3 FlowGram 适配器

| 文件 | 说明 |
|------|------|
| `authoring-to-flowgram.ts` | 将 Authoring Schema 转换为 FlowGram 渲染数据 |
| `flowgram-to-authoring-patch.ts` | 将 FlowGram 变更转换为 Authoring 补丁 |
| `flowgram-node-factory.ts` | 创建 FlowGram 节点（Start/End/Decision/Loop/Annotation 等） |
| `flowgram-edge-factory.ts` | 创建 FlowGram 边（序列流/注释流等） |
| `flowgram-port-factory.ts` | 创建 FlowGram 端口描述符 |
| `flowgram-edge-mapping.ts` | FlowGram 边到微流边的映射 |
| `flowgram-selection-sync.ts` | 选择状态双向同步 |
| `flowgram-validation-sync.ts` | 验证状态双向同步 |

---

## 十二、历史管理与自动布局

### 12.1 历史管理

**文件**: [microflow-history-manager.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/history/microflow-history-manager.ts)

支持 Undo/Redo 操作栈，记录每次 Schema 变更：

| 方法 | 说明 |
|------|------|
| `push` | 推入新的历史记录 |
| `undo` | 撤销上一步操作 |
| `redo` | 重做已撤销的操作 |
| `canUndo` | 是否可以撤销 |
| `canRedo` | 是否可以重做 |

### 12.2 自动布局

**文件**: [auto-layout-engine.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/layout/auto-layout-engine.ts)

基于 Dagre 算法的自动布局引擎：

| 方法 | 说明 |
|------|------|
| `layout` | 执行自动布局计算 |
| `applyAutoLayout` | 应用布局结果到 Schema |

布局参数：
- 方向：从上到下 (TB)
- 节点间距：水平 60px，垂直 80px
- 边间距：40px

---

## 十三、元数据系统

### 13.1 元数据 Provider

**文件**: [metadata-provider.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/metadata/metadata-provider.tsx)

```typescript
interface MicroflowMetadataContextValue {
  loading: boolean;
  error?: Error;
  refresh: () => Promise<void>;
  entities: EntityCatalog;
  enumerations: EnumerationCatalog;
  associations: AssociationCatalog;
  microflows: MicroflowCatalog;
  pages: PageCatalog;
  workflows: WorkflowCatalog;
}
```

### 13.2 元数据目录

| 目录 | 说明 |
|------|------|
| `entity-catalog.ts` | 实体定义目录（属性、类型、关联） |
| `enumeration-catalog.ts` | 枚举定义目录（值列表） |
| `association-catalog.ts` | 关联定义目录（实体间关系） |
| `microflow-catalog.ts` | 微流目录（可调用的微流列表） |
| `page-catalog.ts` | 页面目录（可打开的页面列表） |
| `workflow-catalog.ts` | 工作流目录（可调用的工作流列表） |

元数据通过后端 API 获取，为属性面板和表达式编辑器提供自动补全数据源。

---

## 十四、后端 API 契约

### 14.1 微流资源 API

**控制器**: `MicroflowResourceController`
**路由前缀**: `/api/v1/microflows`

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/v1/microflows` | GET | 获取微流列表 |
| `/api/v1/microflows/{id}` | GET | 获取微流详情 |
| `/api/v1/microflows` | POST | 创建微流 |
| `/api/v1/microflows/{id}` | PUT | 更新微流 |
| `/api/v1/microflows/{id}` | DELETE | 删除微流 |
| `/api/v1/microflows/{id}/publish` | POST | 发布微流 |
| `/api/v1/microflows/{id}/validate` | POST | 验证微流 |
| `/api/v1/microflows/{id}/references` | GET | 获取引用分析 |

### 14.2 微流调试 API

**控制器**: `MicroflowDebugController`
**路由前缀**: `/api/v1/microflows/debug`

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/v1/microflows/{id}/debug/session` | POST | 创建调试会话 |
| `/api/v1/microflows/{id}/debug/run` | POST | 启动测试运行 |
| `/api/v1/microflows/{id}/debug/step` | POST | 单步执行 |
| `/api/v1/microflows/{id}/debug/continue` | POST | 继续执行 |
| `/api/v1/microflows/{id}/debug/stop` | POST | 停止调试 |
| `/api/v1/microflows/{id}/debug/breakpoints` | PUT | 设置断点 |
| `/api/v1/microflows/{id}/debug/variables` | GET | 获取变量 |
| `/api/v1/microflows/{id}/debug/evaluate` | POST | 评估表达式 |

### 14.3 微流元数据 API

**控制器**: `MicroflowMetadataController`
**路由前缀**: `/api/v1/microflows/metadata`

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/v1/microflows/metadata/catalogs` | GET | 获取元数据目录 |
| `/api/v1/microflows/metadata/entities` | GET | 获取实体列表 |
| `/api/v1/microflows/metadata/enumerations` | GET | 获取枚举列表 |
| `/api/v1/microflows/metadata/microflow-references` | GET | 获取微流引用 |

### 14.4 微流表达式 API

**控制器**: `MicroflowExpressionsController`
**路由前缀**: `/api/v1/microflows/expressions`

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/v1/microflows/expressions/parse` | POST | 解析表达式 |
| `/api/v1/microflows/expressions/validate` | POST | 验证表达式 |
| `/api/v1/microflows/expressions/infer-type` | POST | 推断表达式类型 |

### 14.5 微流文件夹 API

**控制器**: `MicroflowFoldersController`
**路由前缀**: `/api/v1/microflows/folders`

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/v1/microflows/folders` | GET | 获取文件夹树 |
| `/api/v1/microflows/folders` | POST | 创建文件夹 |
| `/api/v1/microflows/folders/{id}` | PUT | 更新文件夹 |
| `/api/v1/microflows/folders/{id}` | DELETE | 删除文件夹 |

### 14.6 Mendix 域模型 API

**控制器**: `MendixDomainModelController`
**路由前缀**: `/api/v1/mendix/domain-model`

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/v1/mendix/domain-model` | GET | 获取域模型 |
| `/api/v1/mendix/domain-model` | PUT | 保存域模型 |
| `/api/v1/mendix/domain-model/import` | POST | 导入域模型 |
| `/api/v1/mendix/domain-model/sync` | POST | 同步域模型 |

### 14.7 后端分层架构

```
Controllers (Atlas.AppHost/Microflows/Controllers/)
  │
  ├── MicroflowResourceController      → 微流资源 CRUD
  ├── MicroflowDebugController          → 调试会话管理
  ├── MicroflowMetadataController       → 元数据查询
  ├── MicroflowExpressionsController    → 表达式解析/验证
  ├── MicroflowFoldersController        → 文件夹管理
  ├── MicroflowRuntimeMetadataController → 运行时元数据
  ├── MendixDomainModelController       → 域模型管理
  └── MicroflowAppAssetsController      → 应用资产查询
  │
  ▼
Services (Atlas.Application.Microflows/Services/)
  │
  ├── MicroflowValidationService        → 微流验证
  ├── MicroflowTestRunService           → 测试运行
  └── MicroflowExecutionPlanServices    → 执行计划
  │
  ▼
Runtime (Atlas.Application.Microflows/Runtime/)
  │
  ├── MicroflowRuntimeEngine            → 运行时引擎核心
  ├── MicroflowVariableStore            → 变量存储
  ├── MicroflowExpressionEvaluator      → 表达式求值
  ├── MicroflowLoopExecutor             → 循环执行器
  ├── MicroflowCallStackService         → 调用栈管理
  ├── MicroflowErrorHandlingService     → 错误处理
  ├── MicroflowUnitOfWork               → 事务管理
  ├── Debug/MicroflowDebugCoordinator   → 调试协调器
  ├── Actions/MicroflowActionExecutorRegistry → 动作执行器注册
  ├── Branches/MicroflowBranchRuntimeModels  → 分支运行时模型
  └── Expressions/MicroflowExpressionParser  → 表达式解析
  │
  ▼
Repositories (Atlas.Infrastructure/Repositories/Microflows/)
  │
  └── MicroflowRepositories             → 数据访问实现
  │
  ▼
Domain (Atlas.Domain.Microflows/Entities/)
  │
  └── MicroflowEntities                 → 领域实体定义
```

---

## 十五、依赖关系

### 15.1 包依赖图

```
@atlas/microflow
  ├── @coze-workflow/render              # 画布渲染引擎
  ├── @flowgram-adapter/common           # FlowGram 通用适配
  ├── @flowgram-adapter/free-layout-editor  # FlowGram 自由布局编辑器
  ├── @douyinfe/semi-ui ^2.82.0          # Semi Design UI 组件
  ├── @douyinfe/semi-icons ^2.82.0       # Semi Design 图标
  ├── @codemirror/autocomplete ^6.20.1   # CodeMirror 自动补全
  ├── @codemirror/commands ^6.3.3        # CodeMirror 命令
  ├── @codemirror/language ^6.12.3       # CodeMirror 语言支持
  ├── @codemirror/lint ^6.9.5            # CodeMirror 代码检查
  ├── @codemirror/state ^6.4.1           # CodeMirror 状态管理
  ├── @codemirror/view ^6.34.1           # CodeMirror 视图
  ├── inversify ^6.2.2                   # DI 容器
  ├── html-to-image ^1.11.13             # 图片导出
  ├── react ^18.2.0 (peer)               # React
  └── react-dom ^18.2.0 (peer)           # React DOM
```

### 15.2 包导出映射

```json
{
  ".": "./src/index.ts",                        # 主入口
  "./schema": "./src/schema/index.ts",           # Schema 定义
  "./schema/types": "./src/schema/types.ts",     # Schema 类型
  "./schema/authoring": "./src/schema/authoring/index.ts",  # Authoring Schema
  "./editor": "./src/editor/index.tsx",          # 编辑器入口
  "./node-registry": "./src/node-registry/index.ts",  # 节点注册表
  "./node-panel": "./src/node-panel/index.tsx",  # 节点面板
  "./property-panel": "./src/property-panel/index.tsx",  # 属性面板
  "./property-forms": "./src/property-forms/index.tsx",  # 属性表单
  "./flowgram": "./src/flowgram/index.ts",       # FlowGram 画布
  "./adapters": "./src/adapters/index.ts",       # 数据适配器
  "./history": "./src/history/index.ts",         # 历史管理
  "./layout": "./src/layout/index.ts",           # 自动布局
  "./metadata": "./src/metadata/index.ts",       # 元数据
  "./expressions": "./src/expressions/index.ts", # 表达式引擎
  "./expression-editor": "./src/expression-editor/index.tsx",  # 表达式编辑器
  "./debug": "./src/debug/index.ts",             # 调试系统
  "./performance": "./src/performance/index.ts", # 性能优化
  "./mendix-compat": "./src/mendix-compat/index.ts"  # Mendix 兼容
}
```

### 15.3 前端调用链

```
MicroflowEditorPage (app-web)
  │
  ├── @atlas/microflow/editor
  │     ├── MicroflowEditor 组件
  │     │     ├── FlowGramMicroflowProvider
  │     │     │     └── WorkflowRenderProvider (from @coze-workflow/render)
  │     │     │           └── FlowGramMicroflowContainerModule (Inversify DI)
  │     │     │
  │     │     ├── FlowGramMicroflowNativeCanvas
  │     │     │     ├── FlowGramMicroflowNodeRenderer
  │     │     │     ├── FlowGramMicroflowLineRenderer
  │     │     │     └── FlowGramMicroflowPortRenderer
  │     │     │
  │     │     ├── FlowGramMicroflowToolbar
  │     │     ├── FlowGramMicroflowStatusStrip
  │     │     ├── NodePanel
  │     │     └── PropertyPanel
  │     │
  │     └── @atlas/microflow/adapters
  │           ├── authoring-operations → Schema CRUD
  │           └── microflow-adapters → 数据转换
  │
  ├── @atlas/microflow/debug
  │     └── step-debug-api → WebSocket 调试通信
  │
  └── @atlas/microflow/metadata
        └── metadata-provider → 后端元数据 API
```

---

## 十六、项目运行方式

### 16.1 环境要求

| 工具 | 版本 |
|------|------|
| .NET SDK | 10.0+ |
| Node.js | 18+ (推荐 20+) |
| pnpm | 10.0+ (推荐 10.33.0) |

### 16.2 后端启动

```powershell
# 还原与构建
dotnet restore Atlas.SecurityPlatform.slnx
dotnet build Atlas.SecurityPlatform.slnx

# 启动后端 (默认端口 5002)
dotnet run -c Debug --project src/backend/Atlas.AppHost/Atlas.AppHost.csproj

# 确认监听
Get-NetTCPConnection -LocalPort 5002 -State Listen
```

### 16.3 前端启动

```powershell
cd src/frontend

# 安装依赖
pnpm install

# 启动开发服务器 (app-web)
pnpm --filter app-web dev

# 或使用快捷命令
pnpm run dev:app-web
```

### 16.4 联调启动

```powershell
# 一键启动 AppHost + AppWeb
powershell -ExecutionPolicy Bypass -File .\scripts\dev-start-app-direct.ps1
```

### 16.5 测试运行

```powershell
# 前端单元测试
cd src/frontend
pnpm run test:unit

# 微流包类型检查
cd src/frontend/packages/mendix/mendix-microflow
pnpm run typecheck

# 微流包 lint
pnpm run lint

# E2E 测试
pnpm exec playwright test -c playwright.app.config.ts e2e/app/workflow-collab.spec.ts
```

### 16.6 构建生产版本

```powershell
# 后端发布
dotnet publish src/backend/Atlas.AppHost/Atlas.AppHost.csproj -c Release

# 前端构建
cd src/frontend
pnpm run build:app-web
```

---

## 十七、关键类与函数速查

### 前端核心类/函数

| 名称 | 文件 | 说明 |
|------|------|------|
| `FlowGramMicroflowNativeCanvas` | `flowgram/FlowGramMicroflowNativeCanvas.tsx` | 画布主组件 |
| `FlowGramMicroflowNodeRenderer` | `flowgram/FlowGramMicroflowNodeRenderer.tsx` | 节点渲染器 |
| `FlowGramMicroflowLineRenderer` | `flowgram/FlowGramMicroflowLineRenderer.tsx` | 连线渲染器 |
| `FlowGramMicroflowPortRenderer` | `flowgram/FlowGramMicroflowPortRenderer.tsx` | 端口渲染器 |
| `FlowGramMicroflowProvider` | `flowgram/FlowGramMicroflowProvider.tsx` | 画布渲染上下文 |
| `FlowGramMicroflowToolbar` | `flowgram/FlowGramMicroflowToolbar.tsx` | 工具栏 |
| `FlowGramMicroflowStatusStrip` | `flowgram/FlowGramMicroflowStatusStrip.tsx` | 状态条 |
| `createFlowGramMicroflowNodeRegistries` | `flowgram/FlowGramMicroflowNodeRegistries.ts` | 创建 FlowGram 节点注册表 |
| `createMicroflowDesignSchema` | `flowgram/flowgram-native-schema.ts` | 创建微流设计 Schema |
| `canConnectPortsV2` | `node-registry/edge-registry.ts` | 连接验证核心函数 |
| `inferEdgeKindFromPorts` | `node-registry/edge-registry.ts` | 边类型推断 |
| `edgeStyleByKind` | `node-registry/edge-registry.ts` | 边样式查询 |
| `defaultMicroflowNodeRegistry` | `node-registry/registry.ts` | 默认节点注册表 |
| `engineSupportFromAction` | `node-registry/registry.ts` | 引擎支持等级判断 |
| `objectKindFromRegistryItem` | `node-registry/registry.ts` | 注册类型→ObjectKind 映射 |
| `flowGramPortsForObjectKind` | `flowgram/adapters/flowgram-port-factory.ts` | 端口工厂 |
| `authoringToFlowgram` | `flowgram/adapters/authoring-to-flowgram.ts` | Authoring→FlowGram 转换 |
| `flowgramToAuthoringPatch` | `flowgram/adapters/flowgram-to-authoring-patch.ts` | FlowGram→Authoring 补丁 |
| `MicroflowHistoryManager` | `history/microflow-history-manager.ts` | 历史管理器 |
| `AutoLayoutEngine` | `layout/auto-layout-engine.ts` | 自动布局引擎 |
| `MicroflowMetadataProvider` | `metadata/metadata-provider.tsx` | 元数据 Provider |
| `registerMicroflowNodeForm` | `property-panel/node-form-registry.ts` | 注册属性表单 |
| `buildDesignPropertyPanelModel` | `property-panel/design-protocol-model.ts` | 构建属性面板模型 |
| `deriveNodeInlineConfig` | `node-inline/derive-node-inline-config.ts` | 派生内联编辑配置 |
| `StepDebugApi` | `debug/step-debug-api.ts` | 调试 API |
| `ExpressionEditor` | `expression-editor/index.tsx` | 表达式编辑器 |
| `forceOrthogonalLineKind` | `flowgram/FlowGramMicroflowTypes.ts` | 强制正交连线 |

### 后端核心类/函数

| 名称 | 文件 | 说明 |
|------|------|------|
| `MicroflowResourceController` | `Atlas.AppHost/Microflows/Controllers/` | 微流资源 API |
| `MicroflowDebugController` | `Atlas.AppHost/Microflows/Controllers/` | 调试 API |
| `MicroflowMetadataController` | `Atlas.AppHost/Microflows/Controllers/` | 元数据 API |
| `MicroflowExpressionsController` | `Atlas.AppHost/Microflows/Controllers/` | 表达式 API |
| `MicroflowFoldersController` | `Atlas.AppHost/Microflows/Controllers/` | 文件夹 API |
| `MicroflowRuntimeEngine` | `Atlas.Application.Microflows/Runtime/` | 运行时引擎 |
| `MicroflowVariableStore` | `Atlas.Application.Microflows/Runtime/` | 变量存储 |
| `MicroflowExpressionEvaluator` | `Atlas.Application.Microflows/Runtime/Expressions/` | 表达式求值 |
| `MicroflowLoopExecutor` | `Atlas.Application.Microflows/Runtime/Loops/` | 循环执行器 |
| `MicroflowCallStackService` | `Atlas.Application.Microflows/Runtime/Calls/` | 调用栈管理 |
| `MicroflowErrorHandlingService` | `Atlas.Application.Microflows/Runtime/ErrorHandling/` | 错误处理 |
| `MicroflowDebugCoordinator` | `Atlas.Application.Microflows/Runtime/Debug/` | 调试协调器 |
| `MicroflowActionExecutorRegistry` | `Atlas.Application.Microflows/Runtime/Actions/` | 动作执行器注册 |
| `MicroflowValidationService` | `Atlas.Application.Microflows/Services/` | 验证服务 |
| `MicroflowTestRunService` | `Atlas.Application.Microflows/Services/` | 测试运行服务 |
| `MicroflowExecutionPlanServices` | `Atlas.Application.Microflows/Services/` | 执行计划服务 |
| `MicroflowRepositories` | `Atlas.Infrastructure/Repositories/Microflows/` | 数据访问实现 |
| `MicroflowEntities` | `Atlas.Domain.Microflows/Entities/` | 领域实体 |

---

> **文档维护**: 本文档应在微流编辑器架构发生重大变更时同步更新。最后更新: 2026-05-13
