# Coze Studio — 工作流画布交互系统 Code Wiki

## 目录

1. [项目概述](#1-项目概述)
2. [整体架构](#2-整体架构)
3. [核心模块职责](#3-核心模块职责)
4. [关键类型定义](#4-关键类型定义)
5. [连接线系统详解](#5-连接线系统详解)
6. [节点系统详解](#6-节点系统详解)
7. [端口系统详解](#7-端口系统详解)
8. [画布渲染层架构](#8-画布渲染层架构)
9. [状态管理与数据流](#9-状态管理与数据流)
10. [依赖关系](#10-依赖关系)
11. [验证系统](#11-验证系统)
12. [拖拽交互系统](#12-拖拽交互系统)
13. [历史与撤销系统](#13-历史与撤销系统)
14. [封装与子画布](#14-封装与子画布)
15. [后端 Canvas 验证](#15-后端-canvas-验证)
16. [扩展与二次开发](#16-扩展与二次开发)

---

## 1. 项目概述

Coze Studio 是字节跳动开源的 AI Agent 开发平台。其工作流画布系统允许用户通过可视化拖拽的方式创建、编辑和管理 AI 工作流。

### 1.1 核心能力

| 能力 | 描述 |
|------|------|
| **节点管理** | 添加、删除、复制、拖拽节点，支持多种节点类型（LLM、API、代码、条件分支、循环、批量等） |
| **连接线交互** | 从输出端口拖拽至输入端口创建连接，支持贝塞尔曲线和折线两种样式 |
| **画布操作** | 缩放、平移、框选、撤销/重做、快捷键 |
| **子画布/封装** | 支持将多个节点封装为子流程，支持 Loop/Batch 等复合节点的嵌套画布 |
| **实时验证** | 前端表单验证 + 后端 Schema 验证，检测环路、断连、类型不匹配等问题 |
| **运行调试** | 工作流试运行、Trace 日志追踪、节点级调试 |

### 1.2 技术栈

- **前端框架**: React 18 + TypeScript
- **状态管理**: Inversify (DI) + Zustand
- **拖拽引擎**: react-dnd (HTML5 Backend)
- **手势交互**: @use-gesture/vanilla
- **表单系统**: 自定义 FormModel v1/v2
- **构建工具**: Rush + pnpm monorepo
- **后端**: Go (Hertz)

---

## 2. 整体架构

### 2.1 分层架构

```
┌─────────────────────────────────────────────────────────┐
│                    用户交互层                             │
│  ┌──────────┐  ┌──────────┐  ┌───────────────────────┐  │
│  │ 节点面板  │  │ 浮动面板  │  │  画布操作 (快捷键/手势) │  │
│  └──────────┘  └──────────┘  └───────────────────────┘  │
├─────────────────────────────────────────────────────────┤
│                    业务服务层                             │
│  ┌──────────────┐ ┌─────────────┐ ┌──────────────────┐  │
│  │ WorkflowEdit │ │ WorkflowLine│ │ WorkflowDrag     │  │
│  │ Service      │ │ Service     │ │ Service          │  │
│  └──────────────┘ └─────────────┘ └──────────────────┘  │
│  ┌──────────────┐ ┌─────────────┐ ┌──────────────────┐  │
│  │ Workflow     │ │ Validation  │ │ FloatLayout      │  │
│  │ Run Service  │ │ Service     │ │ Service          │  │
│  └──────────────┘ └─────────────┘ └──────────────────┘  │
├─────────────────────────────────────────────────────────┤
│                    编辑器核心层                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │ @flowgram-adapter/free-layout-editor              │   │
│  │ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ │   │
│  │ │ Workflow│ │ Workflow│ │ Workflow│ │ Workflow│ │   │
│  │ │ Document│ │ Lines   │ │ Select  │ │ Hover   │ │   │
│  │ │         │ │ Manager │ │ Service │ │ Service │ │   │
│  │ └─────────┘ └─────────┘ └─────────┘ └─────────┘ │   │
│  │ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ │   │
│  │ │ FlowNode│ │ FlowNode│ │ FlowNode│ │ FlowNode│ │   │
│  │ │ Entity  │ │ FormData│ │ Error   │ │ Trans   │ │   │
│  │ └─────────┘ └─────────┘ │ Data    │ │ Data    │ │   │
│  │ ┌─────────┐ ┌─────────┐ └─────────┘ └─────────┘ │   │
│  │ │ Workflow│ │ Workflow│                           │   │
│  │ │ Port    │ │ Line    │                           │   │
│  │ │ Entity  │ │ Entity  │                           │   │
│  │ └─────────┘ └─────────┘                           │   │
│  └──────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│                    渲染层                                │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐  │
│  │ Background│ │ Nodes    │ │ Lines    │ │ Shortcuts  │  │
│  │ Layer    │ │ Layer    │ │ Layer    │ │ Layer      │  │
│  └──────────┘ └──────────┘ └──────────┘ └────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐  │
│  │ Hover    │ │ Selector │ │ Transform│ │ ScrollBar  │  │
│  │ Layer    │ │ Box      │ │ Layer    │ │ Layer      │  │
│  └──────────┘ └──────────┘ └──────────┘ └────────────┘  │
├─────────────────────────────────────────────────────────┤
│                    数据层                                │
│  ┌──────────────┐ ┌─────────────┐ ┌──────────────────┐  │
│  │ WorkflowJSON │ │ NodeJSON    │ │ EdgeJSON         │  │
│  │ (序列化)      │ │             │ │                  │  │
│  └──────────────┘ └─────────────┘ └──────────────────┘  │
├─────────────────────────────────────────────────────────┤
│                    后端服务层                             │
│  ┌──────────────┐ ┌─────────────┐ ┌──────────────────┐  │
│  │ Canvas       │ │ Node       │ │ Workflow         │  │
│  │ Validator    │ │ Adaptor    │ │ Compose/Run      │  │
│  └──────────────┘ └─────────────┘ └──────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 2.2 Monorepo 包结构

```
frontend/packages/workflow/
├── base/                    # 基础类型、实体、工具
│   ├── entities/            # WorkflowNode 业务实体
│   ├── types/               # 类型定义 (WorkflowJSON, NodeJSON, Registry)
│   ├── services/            # 基础服务接口
│   ├── utils/               # 工具函数
│   └── constants/           # 常量
├── playground/              # 画布编辑器主应用
│   ├── services/            # 编辑、连接、拖拽、验证等服务
│   ├── components/          # 画布容器、浮动布局、节点面板等
│   ├── nodes-v2/            # V2 节点注册实现
│   ├── node-registries/     # 各类节点的注册配置
│   ├── hooks/               # React Hooks
│   ├── entities/            # 状态实体
│   └── shortcuts/           # 快捷键贡献
├── render/                  # 画布渲染引擎
│   ├── layer/               # 各渲染层 (背景/节点/线/悬浮/快捷)
│   └── components/          # 连接线渲染 (贝塞尔/折线)
├── nodes/                   # 节点公共服务
│   ├── service/             # WorkflowNodesService
│   └── entity-datas/        # 节点数据实体
├── variable/                # 变量系统
├── history/                 # 历史/撤销系统
│   ├── operation-metas/     # 操作元数据 (添加线/节点等)
│   └── services/            # 历史报告服务
├── feature-encapsulate/     # 节点封装功能
│   ├── encapsulate/         # 封装服务 (线/节点/变量)
│   ├── validators/          # 封装验证器
│   └── render/              # 封装 UI 渲染
├── fabric-canvas/           # Fabric.js 画布 (图片编辑)
└── components/              # 共享组件 (Modal, Edit 等)
```

---

## 3. 核心模块职责

### 3.1 WorkflowPlayground

**入口组件**: [WorkflowPlayground](file:///D:/Code/coze-studio-main/frontend/packages/workflow/playground/src/workflow-playground.tsx)

```tsx
<WorkflowPlayground
  spaceId="xxx"
  parentContainer={container}
  // ...props
/>
```

**职责**:
- 初始化空间上下文 (SpaceStore)
- 注入 DI 容器模块: `WorkflowNodesContainerModule`, `WorkflowPageContainerModule`, `WorkflowHistoryContainerModule`
- 提供 `DndProvider` (react-dnd) 和 `QueryClientProvider` (react-query)
- 通过 `WorkflowRenderProvider` 启动渲染引擎

### 3.2 WorkflowPlaygroundContext

**文件**: [WorkflowPlaygroundContext](file:///D:/Code/coze-studio-main/frontend/packages/workflow/playground/src/workflow-playground-context.ts)

**职责**:
- 全局上下文管理器，实现 `PlaygroundContext` 接口
- 管理节点模板映射 (`nodeTemplateMap`)、插件 API 映射
- 提供 `document` 访问器 (通过 `WorkflowDocumentProvider`)
- 管理变量服务、批处理服务、节点服务
- 加载服务端节点分类和模板数据 (`loadNodeInfos`)

**核心属性/方法**:
```typescript
class WorkflowPlaygroundContext implements PlaygroundContext {
  variableService: WorkflowVariableService;
  batchService: WorkflowBatchService;
  nodesService: WorkflowNodesService;
  document: WorkflowDocument;
  
  loadNodeInfos(locale: string): Promise<void>;
  getNodeTemplateInfoByType(type: StandardNodeType): NodeTemplateInfo;
  getTemplateList(types: StandardNodeType[]): NodeTemplate[];
  getTemplateCategoryList(enabledTypes: StandardNodeType[]): NodeCategory[];
}
```

### 3.3 WorkflowDocument

**来源**: `@flowgram-adapter/free-layout-editor`

**职责**:
- 画布文档管理，是编辑器核心数据模型
- 管理所有节点 (`WorkflowNodeEntity`)、连接线、端口
- 提供序列化/反序列化 (`toJSON`, `fromJSON`)
- 提供节点创建、复制、删除方法
- 管理节点注册表

### 3.4 WorkflowLinesManager

**来源**: `@flowgram-adapter/free-layout-editor`

**职责**:
- 连接线生命周期管理
- `createLine(options)` — 创建连接线
- `replaceLine(oldInfo, newInfo)` — 替换连接
- `deleteLine(line)` — 删除连接
- `getAllLines()` — 获取所有连接
- `onForceUpdate()` — 强制更新事件
- 注册线类型贡献 (`WorkflowBezierLineContribution`, `WorkflowFoldLineContribution`)

### 3.5 WorkflowSelectService

**来源**: `@flowgram-adapter/free-layout-editor`

**职责**:
- 节点选择管理
- `selectNodeAndFocus(node)` — 选中并聚焦
- `isSelected(id)` — 判断是否选中
- `onSelectionChanged(callback)` — 选择变化监听

### 3.6 WorkflowHoverService

**来源**: `@flowgram-adapter/free-layout-editor`

**职责**:
- 悬浮状态管理
- `isHovered(id)` — 判断是否悬浮
- `onHoveredChange(callback)` — 悬浮变化监听

---

## 4. 关键类型定义

### 4.1 WorkflowJSON

**文件**: [node.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/base/src/types/node.ts#L35-L38)

```typescript
interface WorkflowJSON {
  nodes: WorkflowNodeJSON[];
  edges: WorkflowEdgeJSON[];
}
```

整个画布的序列化结构。

### 4.2 WorkflowNodeJSON

**文件**: [node.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/base/src/types/node.ts#L25-L33)

```typescript
interface WorkflowNodeJSON<T = Record<string, unknown>> {
  id: string;
  type: StandardNodeType | FlowNodeBaseType | string;
  meta?: WorkflowNodeMeta;
  data: T;
  version?: string;
  blocks?: WorkflowNodeJSON[];  // 子节点 (用于复合节点)
  edges?: WorkflowEdgeJSON[];   // 内部连接 (用于子画布)
}
```

### 4.3 WorkflowEdgeJSON

**来源**: `@flowgram-adapter/free-layout-editor`

```typescript
interface WorkflowEdgeJSON {
  id?: string;
  sourceNodeID: string;
  targetNodeID: string;
  sourcePortID?: string;
  targetPortID?: string;
}
```

### 4.4 WorkflowNodeRegistry

**文件**: [registry.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/base/src/types/registry.ts#L125-L183)

```typescript
interface WorkflowNodeRegistry extends WorkflowOriginNodeRegistry {
  variablesMeta?: WorkflowNodeVariablesMeta;
  meta: NodeMeta;
  
  getNodeInputParameters?: (node: FlowNodeEntity) => InputValueVO[];
  getNodeOutputs?: (node: FlowNodeEntity) => OutputValueVO[];
  getHeaderExtraOperation?: (formValues: any, node: FlowNodeEntity) => ReactNode;
  beforeNodeSubmit?: (node: WorkflowNodeJSON) => WorkflowNodeJSON;
  onInit?: (nodeJSON: WorkflowNodeJSON, context: any) => Promise<void>;
  checkError?: (nodeJSON: WorkflowNodeJSON, context: any) => string;
  onDispose?: (nodeJSON: WorkflowNodeJSON, context: any) => void;
}
```

### 4.5 NodeMeta

**文件**: [registry.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/base/src/types/registry.ts#L55-L123)

```typescript
interface NodeMeta {
  isStart?: boolean;
  isNodeEnd?: boolean;
  deleteDisable?: boolean;
  copyDisable?: boolean;
  nodeDTOType: StandardNodeType;
  size?: { width: number; height: number };
  defaultPorts?: Array<any>;
  useDynamicPort?: boolean;
  subCanvas?: (node: WorkflowNodeEntity) => WorkflowSubCanvas | undefined;
  showTrigger?: (props: { projectId?: string }) => boolean;
  hideTest?: boolean;
  getInputVariableTag?: (variableName, input, extra) => VariableTagProps;
  enableCopilotGenerateTestNodeForm?: boolean;
  headerReadonly?: boolean;
  helpLink?: string | ((props: { apiName: string }) => string);
  test?: NodeTest;
}
```

### 4.6 StandardNodeType

节点类型枚举，包含:
- `Start` — 开始节点
- `End` — 结束节点
- `LLM` — 大语言模型节点
- `Api` — API/插件节点
- `Code` — 代码节点
- `Condition` — 条件分支节点
- `Loop` — 循环节点
- `Batch` — 批量处理节点
- `Break` / `Continue` — 循环控制节点
- `SetVariable` — 变量赋值节点
- `SubWorkflow` — 子工作流节点
- `VariableAssigner` — 变量聚合器
- 等其他类型

---

## 5. 连接线系统详解

### 5.1 核心类: WorkflowLinesService

**文件**: [workflow-line-service.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/playground/src/services/workflow-line-service.ts)

```typescript
@injectable()
class WorkflowLinesService {
  linesManager: WorkflowLinesManager;
  validationService: ValidationService;
  nodePanelService: WorkflowNodePanelService;
  dragService: WorkflowCustomDragService;

  // 获取指定连接线
  getLine(fromId?: string, toId?: string): WorkflowLineEntity | undefined;
  
  // 获取所有连接线
  getAllLines(): WorkflowLineEntity[];
  
  // 创建连接线
  createLine(options: WorkflowLinePortInfo): WorkflowLineEntity;
  
  // 检查连接是否错误
  isError(fromId?: string, toId?: string): boolean;
  
  // 验证单条线
  validateLine(fromId: string, toId: string): void;
  
  // 验证所有线
  validateAllLine(): void;
  
  // 交换两个端口的连接
  replaceLineByPort(oldPortInfo, newPortInfo): void;
}
```

### 5.2 连接线创建流程

```
用户从输出端口按下鼠标
    │
    ▼
WorkflowPortRender.onMouseDown
    │
    ▼
WorkflowDragService.startDrawingLine(entity, event)
    │
    ▼
进入绘制状态 (line.drawingTo = true)
    │
    ▼
鼠标移动到目标输入端口
    │
    ▼
释放鼠标
    │
    ▼
linesManager.createLine({ from, fromPort, to, toPort })
    │
    ▼
WorkflowLineEntity 创建
    │
    ▼
LinesLayer 渲染更新
```

### 5.3 WorkflowLineEntity

**来源**: `@flowgram-adapter/free-layout-editor`

```typescript
class WorkflowLineEntity extends BaseEntity {
  id: string;
  from: WorkflowNodeEntity;       // 源节点
  to: WorkflowNodeEntity;         // 目标节点
  fromPort: WorkflowPortEntity;   // 源端口
  toPort: WorkflowPortEntity;     // 目标端口
  position: { from: IPoint; to: IPoint };
  
  isDrawing: boolean;             // 是否正在绘制
  drawingTo: boolean;             // 是否有绘制目标
  hasError: boolean;              // 是否有错误
  isHidden: boolean;              // 是否隐藏
  highlightColor?: string;        // 高亮颜色
  processing: boolean;            // 处理中状态
  
  validate(): void;
  dispose(): void;
}
```

### 5.4 连接线类型

系统支持两种连接线样式，通过 `LineType` 控制:

| 类型 | 枚举值 | 渲染组件 | 描述 |
|------|--------|----------|------|
| 贝塞尔曲线 | `LineType.BEZIER` | `BezierLineRender` | 平滑的贝塞尔曲线，带渐变色彩 |
| 折线 | `LineType.LINE_CHART` | `FoldLineRender` | 直角折线 |

### 5.5 BezierLineRender

**文件**: [bezier-line/index.tsx](file:///D:/Code/coze-studio-main/frontend/packages/workflow/render/src/components/lines/bezier-line/index.tsx)

```tsx
interface BezierLineProps {
  fromColor?: string;     // 起始颜色
  toColor?: string;       // 终止颜色
  color?: string;         // 高亮颜色 (最高优先级)
  selected?: boolean;     // 是否选中
  line: WorkflowLineEntity;
  version: string;        // 控制 memo 刷新
}
```

**渲染逻辑**:
- 使用 SVG `<path>` 渲染贝塞尔曲线
- 使用 `<linearGradient>` 实现渐变效果
- 带箭头渲染 (`ArrowRenderer`)
- `processing` 状态有特殊样式

### 5.6 LinesLayer

**文件**: [lines-layer.tsx](file:///D:/Code/coze-studio-main/frontend/packages/workflow/render/src/layer/lines-layer.tsx)

```typescript
@injectable()
class LinesLayer extends Layer {
  static type = 'WorkflowLinesLayer';
  
  // 观察实体
  @observeEntities(WorkflowLineEntity) readonly lines: WorkflowLineEntity[];
  @observeEntities(WorkflowPortEntity) readonly ports: WorkflowPortEntity[];
  
  // 颜色优先级
  getLineColor(line: WorkflowLineEntity): string {
    // 1. hidden > highlightColor
    // 2. hasError > selected/hovered → errorActiveColor, otherwise ERROR
    // 3. highlightColor
    // 4. drawingTo → DRAWING
    // 5. selected/hovered → HOVER
    // 6. DEFAULT
  }
  
  // 前后分层
  isFrontLine(line): boolean  // 悬浮/选中/绘制中的线在前层
  renderBackLines(): ReactNode
  renderFrontLines(): ReactNode
}
```

**颜色常量** (`LineColors`):
- `DEFUALT` — 默认颜色
- `HOVER` — 悬浮/选中颜色
- `DRAWING` — 绘制中颜色
- `ERROR` — 错误颜色 (`#FF5DC8`)

### 5.7 连接线上添加节点

**文件**: [line-add-button/index.tsx](file:///D:/Code/coze-studio-main/frontend/packages/workflow/playground/src/components/line-add-button/index.tsx)

在连接线悬浮/选中时，线中点显示 `+` 按钮，点击后:
1. 弹出节点面板 (`nodePanelService.call`)
2. 面板定位在线的中点
3. 选择节点后插入到当前连接中间
4. 原连接线被 `dispose`，创建两条新连接

---

## 6. 节点系统详解

### 6.1 WorkflowNode 业务实体

**文件**: [workflow-node.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/base/src/entities/workflow-node.ts)

```typescript
class WorkflowNode {
  private node: FlowNodeEntity;
  
  get registry(): WorkflowNodeRegistry;
  get inputParameters(): InputValueVO[];
  get outputs(): OutputValueVO[];
  get type(): StandardNodeType;
  get isError(): boolean;
  get error(): Error;
  get isInitialized(): boolean;
  get data(): any;
  get icon(): string;
  get title(): string;
  get description(): string;
  
  setError(error: Error): void;
  setData(data: any): void;
  getValueByPath<T>(pathname: string): T;
}
```

### 6.2 WorkflowEditService

**文件**: [workflow-edit-service.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/playground/src/services/workflow-edit-service.ts)

```typescript
@injectable()
class WorkflowEditService {
  // 添加节点
  addNode = async (
    type: StandardNodeType,
    nodeJson?: Partial<WorkflowNodeJSON>,
    event?: { clientX, clientY },
    isDrag?: boolean,
  ): Promise<WorkflowNodeEntity | undefined>;
  
  // 复制节点
  copyNode = async (node: WorkflowNodeEntity): Promise<WorkflowNodeEntity>;
  
  // 删除节点 (带确认弹窗)
  deleteNode = (node: WorkflowNodeEntity, noConfirm?: boolean): void;
  
  // 聚焦节点
  focusNode(node?: WorkflowNodeEntity): void;
  
  // 内部方法
  private getDropNode(): WorkflowNodeEntity;  // 确定放置目标容器
  private disposeNode(node: WorkflowNodeEntity);  // 销毁节点
  recreateNodeJSON(nodeJSON, titleCache, shouldReplaceId): WorkflowNodeJSON;
}
```

**addNode 流程**:
```
1. 检查只读状态
2. API 节点触发风险提示
3. 生成唯一标题 (createUniqTitle)
4. 调用节点注册的 onInit 钩子
5. 判断是拖拽还是点击添加
6. 拖拽: dragService.dropCard()
7. 点击: workflowDocument.createWorkflowNodeByType()
8. 防重叠定位 (getAntiOverlapPosition)
9. 确定放置容器 (getDropNode)
10. 结束拖拽状态，聚焦新节点
```

### 6.3 节点销毁

```typescript
private disposeNode(node: WorkflowNodeEntity) {
  // V2 节点: 调用注册的 onDispose 钩子
  if (isNodeV2(node)) {
    const formModel = node.getData<FlowNodeFormData>(FlowNodeFormData)
      .getFormModel<FormModelV2>();
    node.getNodeRegister()?.onDispose?.(formModel.getValues(), this.context);
  }
  node.dispose();  // 底层销毁
}
```

---

## 7. 端口系统详解

### 7.1 WorkflowPortRender

**文件**: [workflow-port-render/index.tsx](file:///D:/Code/coze-studio-main/frontend/packages/workflow/render/src/components/workflow-port-render/index.tsx)

```typescript
interface WorkflowPortRenderProps {
  entity: WorkflowPortEntity;
  onClick?: (event, port) => void;
}
```

**核心交互**:

```typescript
const onMouseDown = useCallback((e: React.MouseEvent) => {
  // 仅输出端口可拖拽
  if (portType === 'input' || disabled || e.button === 1) return;
  e.stopPropagation();
  e.preventDefault();
  dragService.startDrawingLine(entity, e);  // 开始绘制连接线
}, [dragService, portType, portID]);
```

**状态监听**:
```typescript
// 监听实体变化 → 更新位置
props.entity.onEntityChange(() => { updatePosX/Y(...) });

// 监听悬浮变化
hoverService.onHoveredChange(id => { setHovered(...) });

// 监听错误变化
props.entity.onErrorChanged(() => { setHasError(...) });

// 监听可用连接变化
linesManager.onAvailableLinesChange(() => { setLinked(...) });
```

**视觉状态**:
- `hovered` — 悬浮高亮
- `linked` — 已有连接 (深蓝色点)
- `hasError` — 错误状态 (显示警告图标 + Tooltip)

### 7.2 WorkflowPortEntity

**来源**: `@flowgram-adapter/free-layout-editor`

```typescript
class WorkflowPortEntity extends BaseEntity {
  id: string;
  portID: string;              // 端口标识
  portType: 'input' | 'output';
  disabled: boolean;
  relativePosition: IPoint;    // 相对位置
  targetElement?: HTMLElement; // 门户目标元素
  lines: WorkflowLineEntity[]; // 关联的连接
  allLines: WorkflowLineEntity[];
  hasError: boolean;
  errorMessage?: string;
  
  validate(): void;
  onEntityChange(callback): Disposable;
  onErrorChanged(callback): Disposable;
}
```

---

## 8. 画布渲染层架构

### 8.1 WorkflowRenderContribution

**文件**: [workflow-render-contribution.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/render/src/workflow-render-contribution.ts)

```typescript
@injectable()
class WorkflowRenderContribution implements FlowRendererContribution, PlaygroundContribution {
  registerRenderer(registry: FlowRendererRegistry): void {
    // 1. 基础画布层 (缩放、手势)
    registry.registerLayer(PlaygroundLayer);
    
    // 2. 节点内容层
    registry.registerLayers(FlowNodesContentLayer);
    
    // 3. 滚动条层
    registry.registerLayer(FlowScrollBarLayer);
    
    // 4. 悬浮控制层
    registry.registerLayer(HoverLayer);
    
    // 5. 快捷键配置层
    registry.registerLayer(ShortcutsLayer);
    
    // 6. 连接线层
    registry.registerLayer(WorkflowLinesLayer);
    
    // 7. 节点变换层 (位置)
    registry.registerLayer(FlowNodesTransformLayer);
    
    // 8. 选择框层
    registry.registerLayer(FlowSelectorBoundsLayer, { disableBackground: true });
    registry.registerLayer(FlowSelectorBoxLayer, { canSelect: ... });
    
    // 9. 调试层 (playground_debug 参数启用)
    if (location.search.match('playground_debug')) {
      registry.registerLayers(FlowDebugLayer);
    }
    
    // 10. 背景层 (最后插入)
    registry.registerLayer(BackgroundLayer);
  }
  
  onReady(): void {
    // 注册贝塞尔线和折线贡献
    this.linesManager
      .registerContribution(WorkflowBezierLineContribution)
      .registerContribution(WorkflowFoldLineContribution);
    
    // 阻止 body 缩放手势
    document.documentElement.style.overscrollBehavior = 'none';
    document.body.style.overscrollBehavior = 'none';
  }
}
```

### 8.2 框选逻辑 (FlowSelectorBoxLayer)

```typescript
canSelect: (event, entity) => {
  // 1. 仅鼠标左键
  if (event.button !== 0) return false;
  
  // 2. 检查目标元素
  if (element.closest('[data-flow-editor-selectable="true"]')) return true;
  if (element.closest('[data-flow-editor-selectable="false"]')) return false;
  
  // 3. 悬浮到节点/线时不触发
  if (hoverService.isSomeHovered()) return false;
  
  // 4. 仅在画布空白区域/连接线上触发
  if (!element.classList.contains('gedit-playground-layer') &&
      !element.classList.contains('gedit-flow-background-layer') &&
      !element.closest('.gedit-flow-activity-edge')) return false;
  
  return true;
}
```

### 8.3 层叠上下文管理

使用 `StackingContextManager` 管理 DOM 渲染顺序，确保节点、连接线、悬浮层等正确叠加。

---

## 9. 状态管理与数据流

### 9.1 DI 容器架构

项目使用 **Inversify** 作为依赖注入框架:

```typescript
// 入口
<WorkflowRenderProvider
  containerModules={[
    WorkflowNodesContainerModule,
    WorkflowPageContainerModule,
    WorkflowHistoryContainerModule,
  ]}
  preset={preset}
>
```

**容器模块注册**:

```typescript
// 示例: WorkflowPageContainerModule
class WorkflowPageContainerModule extends ContainerModule {
  constructor() {
    super(bind => {
      bind(WorkflowEditService).toSelf().inSingletonScope();
      bind(WorkflowLinesService).toSelf().inSingletonScope();
      bind(WorkflowDragService).to(WorkflowCustomDragService).inSingletonScope();
      bind(WorkflowValidationService).toSelf().inSingletonScope();
      bind(WorkflowFloatLayoutService).toSelf().inSingletonScope();
      bind(WorkflowGlobalStateEntity).toSelf().inSingletonScope();
      // ...
    });
  }
}
```

### 9.2 WorkflowGlobalStateEntity

```typescript
@injectable()
class WorkflowGlobalStateEntity {
  readonly: boolean;          // 只读模式
  spaceId: string;            // 空间 ID
  workflowId: string;         // 工作流 ID
  flowMode: WorkflowMode;     // 工作流模式
  
  config: { /* 配置 */ };
}
```

### 9.3 实体管理器

```typescript
class WorkflowPlaygroundContext {
  @inject(EntityManager) public entityManager: EntityManager;
  
  get globalState(): WorkflowGlobalStateEntity {
    return this.entityManager.getEntity(WorkflowGlobalStateEntity);
  }
}
```

### 9.4 Zustand Store

验证状态使用 Zustand 管理:

```typescript
const createStore = () =>
  createWithEqualityFn<ValidationState>(
    () => ({
      errors: {},      // V1 错误
      errorsV2: {},    // V2 错误
      validating: false,
    }),
    shallow,
  );
```

---

## 10. 依赖关系

### 10.1 外部依赖

```
@flowgram-adapter/free-layout-editor   # 编辑器核心 (节点/线/端口/文档)
@flowgram-adapter/common               # 通用工具 (Disposable, Emitter, 几何计算)
inversify                              # 依赖注入
react-dnd + react-dnd-html5-backend    # 拖拽引擎
@use-gesture/vanilla                   # 手势库
zustand                                # 轻量状态管理
@tanstack/react-query                  # 数据请求
lodash-es                              # 工具库
ahooks                                 # React Hooks
```

### 10.2 内部依赖

```
@coze-workflow/base          → 基础类型、常量、API
@coze-workflow/nodes         → 节点服务、节点数据
@coze-workflow/variable      → 变量系统
@coze-workflow/history       → 历史撤销
@coze-workflow/render        → 渲染引擎
@coze-arch/i18n              → 国际化
@coze-arch/coze-design       → UI 组件库
@coze-arch/bot-api           → 后端 API 封装
```

### 10.3 依赖图

```
WorkflowPlayground
    ├── WorkflowRenderProvider
    │   ├── WorkflowRenderContribution
    │   │   ├── LinesLayer → WorkflowLinesManager → WorkflowLineEntity
    │   │   ├── HoverLayer → WorkflowHoverService
    │   │   ├── BackgroundLayer
    │   │   └── ShortcutsLayer
    │   └── FlowNodesContentLayer → WorkflowNodeEntity
    │
    ├── WorkflowPageContainerModule
    │   ├── WorkflowEditService
    │   ├── WorkflowLinesService
    │   ├── WorkflowCustomDragService → WorkflowDragService
    │   ├── WorkflowValidationService
    │   └── WorkflowFloatLayoutService
    │
    ├── WorkflowNodesContainerModule
    │   └── WorkflowNodesService
    │
    └── WorkflowHistoryContainerModule
        └── addLineOperationMeta → linesManager.createLine()
```

---

## 11. 验证系统

### 11.1 WorkflowValidationService

**文件**: [workflow-validation-service.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/playground/src/services/workflow-validation-service.ts)

```typescript
@injectable()
class WorkflowValidationService implements ValidationService {
  // 验证单个节点
  async validateNode(node: WorkflowNodeEntity): Promise<ValidateResult>;
  
  // 验证整个工作流
  async validateWorkflow(): Promise<ValidateResult>;
  
  // 验证后端 Schema
  async validateSchema(): Promise<ValidateResult>;
  async validateSchemaV2(): Promise<ValidateResult>;
  
  // 检查连接线错误
  isLineError(fromId: string, toId?: string): boolean;
}
```

### 11.2 节点验证流程

```
validateNode(node)
    │
    ├── validateNodeError(node)          → 检查 FlowNodeErrorData
    ├── validateForm(node)               → 表单验证 (Zod)
    ├── validateSubCanvasPort(node)      → 子画布端口连接检查
    └── validateSettingOnErrorPort(node) → 异常处理端口检查
    │
    └── mergeValidateResult(...)         → 合并所有结果
```

### 11.3 子画布端口验证

验证 Loop/Batch 等复合节点的内部连接:
- 检查 `*-function-inline-output` 端口是否有输入
- 检查 `*-function-inline-input` 端口是否有输出
- 所有叶节点是否为结束节点

### 11.4 ValidateError 类型

```typescript
interface ValidateError {
  nodeId: string;           // 节点 ID
  targetNodeId?: string;    // 目标节点 (连接线错误)
  errorType: 'node' | 'line';
  errorInfo: string;
  errorLevel: 'error' | 'warning';
}
```

---

## 12. 拖拽交互系统

### 12.1 WorkflowCustomDragService

**文件**: [workflow-drag-service.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/playground/src/services/workflow-drag-service.ts)

```typescript
@injectable()
class WorkflowCustomDragService extends WorkflowDragService {
  state: {
    isDragging: boolean;
    dragNode?: { type: StandardNodeType; json?: WorkflowNodeJSON };
    transforms?: FlowNodeTransformData[];  // 容器变换数据
    dropNode?: WorkflowNodeEntity;          // 当前放置目标
  };
  
  // 开始拖拽
  startDrag(dragNode): void;
  
  // 结束拖拽
  endDrag(): void;
  
  // 检查坐标是否可以放置
  canDrop(params: { coord: XYCoord; dragNode }): boolean;
  
  // 检查是否可以放入节点
  canDropToNode(params: { dragNodeType; dropNode }): {
    allowDrop: boolean;
    message?: string;
    dropNode?: WorkflowNodeEntity;
  };
}
```

### 12.2 拖拽放置规则

| 拖拽节点 | 放置目标 | 允许 | 说明 |
|----------|----------|------|------|
| Start/End | 任何容器 | ❌ | 首尾节点不允许放入容器 |
| Loop/Batch | Loop/Batch 容器 | ❌ | 不允许嵌套循环/批量 |
| Break/Continue/SetVariable | Loop 的子画布 | ✅ | 仅限循环内部 |
| Break/Continue/SetVariable | 其他位置 | ❌ | 仅限循环使用 |
| 普通节点 | ROOT/SUB_CANVAS/容器 | ✅ | 正常放置 |

### 12.3 碰撞检测

```typescript
private getCollisionTransform(params): FlowNodeTransformData | undefined {
  const draggingRect = new Rectangle(position.x, position.y, 200, 30);
  const collisionTransform = transforms.find(transform => {
    const padding = this.document.layout.getPadding(entity);
    const transformRect = new Rectangle(
      bounds.x + padding.left + padding.right,
      bounds.y, bounds.width, bounds.height,
    );
    return Rectangle.intersects(draggingRect, transformRect);
  });
  return collisionTransform;
}
```

### 12.4 拖拽生成节点的流程

```
1. 从节点面板拖拽卡片
   ↓
2. WorkflowCustomDragService.startDrag()
   ↓
3. 拖动过程中 computeCanDrop() 实时检测放置位置
   ↓
4. dropCard() 放置在目标位置
   ↓
5. WorkflowEditService.addNode(type, json, event, true)
   ↓
6. workflowDocument.createWorkflowNodeByType()
   ↓
7. endDrag() 结束拖拽
   ↓
8. focusNode() 聚焦新节点
```

---

## 13. 历史与撤销系统

### 13.1 addLineOperationMeta

**文件**: [add-line.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/history/src/operation-metas/add-line.ts)

```typescript
export const addLineOperationMeta: OperationMeta<
  AddOrDeleteLineOperationValue,
  PluginContext,
  void
> = {
  type: FreeOperationType.addLine,
  
  // 逆操作 (撤销)
  inverse: op => ({
    ...op,
    type: FreeOperationType.deleteLine,
  }),
  
  // 应用操作
  apply: (operation, ctx: PluginContext) => {
    const linesManager = ctx.get<WorkflowLinesManager>(WorkflowLinesManager);
    const document = ctx.get<WorkflowDocument>(WorkflowDocument);
    
    if (!operation.value.to || !document.getNode(operation.value.to)) return;
    
    linesManager.createLine({
      ...operation.value,
      key: operation.value.id,
    });
  },
  
  // 是否合并操作
  shouldMerge,
};
```

### 13.2 操作类型

- `FreeOperationType.addLine` — 添加连接线
- `FreeOperationType.deleteLine` — 删除连接线
- `FreeOperationType.addNode` — 添加节点
- `FreeOperationType.deleteNode` — 删除节点
- `FreeOperationType.moveNode` — 移动节点

---

## 14. 封装与子画布

### 14.1 EncapsulateLinesService

**文件**: [encapsulate-lines-service.ts](file:///D:/Code/coze-studio-main/frontend/packages/workflow/feature-encapsulate/src/encapsulate/encapsulate-lines-service.ts)

**核心功能**:
- 获取封装节点的输入/输出连接线
- 验证封装连接规则
- 创建封装连接线
- 解封装 (Decapsulate) 时恢复连接

### 14.2 封装连接规则

```typescript
validateEncapsulateLines(lines: WorkflowLineEntity[]): boolean {
  // 输入线: 所有线必须来自同一个源端口
  // 输出线: 所有线必须去往同一个目标端口
  const isFromPortUniq = uniq(lines.map(l => l.fromPort)).length === 1;
  const isToPortUniq = uniq(lines.map(l => l.toPort)).length === 1;
  return isFromPortUniq || isToPortUniq;
}
```

### 14.3 解封装连接恢复

```typescript
createDecapsulateLines(options: {
  node: WorkflowNodeEntity;     // 封装节点
  workflowJSON: WorkflowJSON;   // 内部工作流 JSON
  startNodeId: string;          // 内部开始节点 ID
  endNodeId: string;            // 内部结束节点 ID
  idsMap: Map<string, string>;  // ID 映射 (旧 → 新)
})
```

恢复三类连接:
1. **Internal Lines** — 内部节点间的连接
2. **Input Lines** — 外部 → 内部的输入连接
3. **Output Lines** — 内部 → 外部的输出连接

### 14.4 子画布特殊连接

以下连接被视为子画布特殊连接:

| 源端口 | 目标端口 | 说明 |
|--------|----------|------|
| `loop-function-inline-output` | 任意 | Loop 内部输出 |
| 任意 | `loop-function-inline-input` | Loop 内部输入 |
| `batch-function-inline-output` | 任意 | Batch 内部输出 |
| 任意 | `batch-function-inline-input` | Batch 内部输入 |
| `loop-output-to-function` | `loop-function-input` | Loop 反馈连接 |
| `batch-output-to-function` | `batch-function-input` | Batch 反馈连接 |

---

## 15. 后端 Canvas 验证

### 15.1 CanvasValidator

**文件**: [canvas_validate.go](file:///D:/Code/coze-studio-main/backend/domain/workflow/internal/canvas/validate/canvas_validate.go)

```go
type CanvasValidator struct {
  cfg          *Config
  reachability *reachability
}

// 检测方法
func (cv *CanvasValidator) DetectCycles(ctx context.Context) ([]*Issue, error)
func (cv *CanvasValidator) ValidateConnections(ctx context.Context) ([]*Issue, error)
func (cv *CanvasValidator) CheckRefVariable(ctx context.Context) ([]*Issue, error)
func (cv *CanvasValidator) ValidateNestedFlows(ctx context.Context) ([]*Issue, error)
func (cv *CanvasValidator) CheckGlobalVariables(ctx context.Context) ([]*Issue, error)
func (cv *CanvasValidator) CheckSubWorkFlowTerminatePlanType(ctx context.Context) ([]*Issue, error)
```

### 15.2 环路检测

使用 DFS 检测控制流环路:

```go
func detectCycles(nodes []string, controlSuccessors map[string][]string) [][]string {
  visited := map[string]bool{}
  var dfs func(path []string) [][]string
  // DFS 遍历，检测回到已访问节点的情况
}
```

### 15.3 可达性分析

```go
func performReachabilityAnalysis(
  nodeMap map[string]*vo.Node,
  edgeMap map[string][]string,
  startNode *vo.Node,
) (map[string]*vo.Node, error) {
  // BFS 从 Start 节点出发，计算所有可达节点
}
```

### 15.4 连接验证

```go
func validateConnections(ctx context.Context, c *vo.Canvas) ([]*Issue, error) {
  // 验证:
  // 1. Start 节点必须有输出
  // 2. 分支节点 (Selector) 的每个端口必须有连接
  // 3. 非特殊节点必须有输出
  // 4. 递归验证子画布
}
```

### 15.5 Issue 类型

```go
type Issue struct {
  NodeErr *NodeErr  // 节点错误
  PathErr *PathErr  // 路径错误 (连接线)
  Message string     // 错误信息
}

type NodeErr struct {
  NodeID   string
  NodeName string
}

type PathErr struct {
  StartNode string  // 连接起始节点
  EndNode   string  // 连接终止节点
}
```

### 15.6 后端验证 API

```
POST /api/v1/workflow/validate_schema
  Body: { schema: JSON.stringify(workflowJSON), bind_project_id / bind_bot_id }

POST /api/v1/workflow/validate_tree
  Body: { schema, workflow_id, bind_project_id / bind_bot_id }
```

---

## 16. 扩展与二次开发

### 16.1 注册新节点类型

1. 在 `StandardNodeType` 枚举中添加类型
2. 在 `node-registries/` 创建节点目录
3. 实现 `WorkflowNodeRegistry`:

```typescript
const myNodeRegistry: WorkflowNodeRegistry = {
  meta: {
    nodeDTOType: StandardNodeType.MyNode,
    size: { width: 400, height: 300 },
    subCanvas: (node) => undefined,
    // ...
  },
  onInit: async (nodeJSON, context) => {
    // 初始化逻辑
  },
  getNodeInputParameters: (node) => { /* ... */ },
  getNodeOutputs: (node) => { /* ... */ },
};
```

### 16.2 自定义连接线样式

注册新的 `LineContribution`:

```typescript
linesManager.registerContribution({
  type: LineType.CUSTOM,
  render: (line, props) => <CustomLineRenderer />,
  pathCalculator: (from, to) => { /* 计算路径 */ },
});
```

### 16.3 自定义渲染层

实现 `Layer` 接口:

```typescript
@injectable()
class MyCustomLayer extends Layer {
  static type = 'MyCustomLayer';
  
  onReady(): void { /* 初始化 */ }
  render(): JSX.Element { /* 渲染内容 */ }
  onZoom(scale: number): void { /* 缩放处理 */ }
}

// 注册
registry.registerLayer(MyCustomLayer);
```

### 16.4 快捷键扩展

通过 `ShortcutsLayer` 贡献新的快捷键:

```typescript
class MyShortcutsContribution implements ShortcutsContribution {
  registerShortcuts(registry: ShortcutsRegistry): void {
    registry.registerShortcut({
      id: 'my-action',
      keybinding: 'mod+shift+k',
      when: 'playgroundFocused',
      handler: () => { /* ... */ },
    });
  }
}
```

---

## 附录: 关键文件索引

| 文件 | 路径 | 说明 |
|------|------|------|
| 入口组件 | `playground/src/workflow-playground.tsx` | 画布入口 |
| 上下文 | `playground/src/workflow-playground-context.ts` | 全局上下文 |
| 编辑服务 | `playground/src/services/workflow-edit-service.ts` | 节点编辑 |
| 连接服务 | `playground/src/services/workflow-line-service.ts` | 连接线管理 |
| 拖拽服务 | `playground/src/services/workflow-drag-service.ts` | 拖拽交互 |
| 验证服务 | `playground/src/services/workflow-validation-service.ts` | 验证逻辑 |
| 渲染贡献 | `render/src/workflow-render-contribution.ts` | 渲染注册 |
| 连接线层 | `render/src/layer/lines-layer.tsx` | 线渲染层 |
| 贝塞尔线 | `render/src/components/lines/bezier-line/index.tsx` | 贝塞尔曲线 |
| 端口渲染 | `render/src/components/workflow-port-render/index.tsx` | 端口交互 |
| 节点实体 | `base/src/entities/workflow-node.ts` | 业务节点 |
| 类型定义 | `base/src/types/node.ts` | JSON 类型 |
| 注册类型 | `base/src/types/registry.ts` | 注册表类型 |
| 封装线服务 | `feature-encapsulate/src/encapsulate/encapsulate-lines-service.ts` | 封装连接 |
| 添加线操作 | `history/src/operation-metas/add-line.ts` | 撤销操作 |
| 后端验证 | `backend/domain/workflow/internal/canvas/validate/canvas_validate.go` | Go 验证 |
