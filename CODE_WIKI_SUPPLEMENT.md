# Code Wiki 补充文档：变量作用域、属性面板与节点面板

> 生成日期: 2026-05-13
> 关联主文档: `CODE_WIKI.md`
> 范围: useNodeVariableScope 完整实现、变量作用域规则、MicroflowVariableSymbol 类型、属性面板约定、节点面板卡片渲染规则

---

## 一、useNodeVariableScope 完整实现逻辑

### 1.1 Hook 签名

**文件**: [useNodeVariableScope.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/inline/useNodeVariableScope.ts)

```typescript
function useNodeVariableScope(
  objectId: string,
  options?: MicroflowVariableQueryOptions,
): MicroflowVariableSymbol[]
```

### 1.2 调用链

```
useNodeVariableScope(objectId, options)
  │
  └── useFlowGramMicroflowContext()
        │   ← FlowGramMicroflowRuntimeContext
        │   ← 必须在 FlowGramMicroflowNativeCanvas 内使用
        │
        └── context.getVariablesForNode(objectId, options)
              │
              └── getVariablesBeforeObject(schema, variableIndex, objectId, options)
                    │   ← 来自 variables/variable-scope-engine.ts
                    │
                    └── normalizeSymbols(schema, index, objectId, includeCurrentObject=false, options)
                          │
                          ├── getVariableSymbols(index)
                          │     → 获取所有变量符号
                          │
                          ├── isVariableVisibleAtObject(schema, symbol, objectId, includeCurrentObject)
                          │     → 逐个判断变量可见性
                          │
                          ├── getVariableVisibilityAtObject(schema, index, name, objectId)
                          │     → 计算最终可见性 (definite/maybe/unavailable)
                          │
                          └── filterByOptions(symbols, options)
                                → 按查询选项过滤
```

### 1.3 完整实现源码

```typescript
import { useMemo } from "react";
import type { MicroflowVariableSymbol } from "../../schema";
import type { MicroflowVariableQueryOptions } from "../../variables/variable-scope-engine";
import { useFlowGramMicroflowContext } from "./useFlowGramMicroflowContext";

export function useNodeVariableScope(
  objectId: string,
  options?: MicroflowVariableQueryOptions,
): MicroflowVariableSymbol[] {
  const { getVariablesForNode } = useFlowGramMicroflowContext();
  return useMemo(
    () => getVariablesForNode(objectId, options),
    [getVariablesForNode, objectId, JSON.stringify(options)]
  );
}
```

### 1.4 Context 提供方

**文件**: [FlowGramMicroflowContext.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowContext.ts)

```typescript
interface FlowGramMicroflowRuntimeContextValue {
  schema: MicroflowDesignSchema;
  variableIndex: MicroflowVariableIndex;
  getVariablesForNode: (objectId: string, options?: MicroflowVariableQueryOptions) => MicroflowVariableSymbol[];
  runtimeTraceByObjectId: Map<string, MicroflowTraceFrame>;
  expandedObjectId: string | null;
  onExpandChange: (objectId: string | null) => void;
  onSchemaChange: (next: MicroflowDesignSchema, reason: string) => void;
  readonly: boolean;
}
```

在 `editor/index.tsx` 中，`getVariablesForNode` 的实际绑定：

```typescript
getVariablesForNode: (objectId, options) =>
  getVariablesBeforeObject(schema, propertyInlineVariableIndex, objectId, options)
```

### 1.5 查询选项

```typescript
interface MicroflowVariableQueryOptions {
  includeMaybe?: boolean;          // 是否包含 maybe 可见性变量，默认 true
  includeUnavailable?: boolean;    // 是否包含不可用变量，默认 false
  includeSystem?: boolean;         // 是否包含系统变量 ($currentUser 等)，默认 true
  includeErrorContext?: boolean;   // 是否包含错误上下文变量 ($latestError 等)，默认 true
  allowedTypeKinds?: MicroflowDataType["kind"][];  // 按数据类型种类过滤
  allowedTypes?: MicroflowDataType[];               // 按具体数据类型过滤
  readonlyOnly?: boolean;          // 仅返回只读变量
  writableOnly?: boolean;          // 仅返回可写变量
  collectionId?: string;           // 限定在特定 ObjectCollection 内
}
```

---

## 二、变量作用域规则

### 2.1 变量可见性三级模型

```typescript
type MicroflowVariableVisibility = "definite" | "maybe" | "unavailable";
```

| 可见性 | 含义 | UI 表现 |
|--------|------|---------|
| `definite` | 变量在当前节点的**所有正常路径**上都被赋值 | 正常显示，无警告 |
| `maybe` | 变量仅在**部分路径**上被赋值（如决策分支后） | 显示为斜体/带警告图标 |
| `unavailable` | 变量在当前节点不可见 | 不在变量列表中显示 |

### 2.2 变量作用域类型

```typescript
interface MicroflowVariableScope {
  kind?: "global" | "objectCollection" | "downstream" | "branch" | "loop" | "errorHandler" | "system" | "collection";
  collectionId: string;            // 所属 ObjectCollection ID
  startObjectId?: string;          // 变量诞生节点 ID
  endObjectId?: string;            // 变量消亡节点 ID
  loopObjectId?: string;           // 所属循环节点 ID（仅 loop 类型）
  errorHandlerFlowId?: string;     // 所属错误处理流 ID（仅 errorHandler 类型）
  branchSourceObjectId?: string;   // 分支源节点 ID（仅 branch 类型）
  branchFlowId?: string;           // 分支流 ID（仅 branch 类型）
}
```

### 2.3 作用域规则详解

#### 规则 1: Global 作用域

- **参数变量** (`$currentUser`, `$currentSession`, 微流输入参数) 具有 `global` 作用域
- 在微流所有节点上都可见，可见性为 `definite`

#### 规则 2: Downstream 作用域

- 动作活动的输出变量具有 `downstream` 作用域
- `startObjectId` 为产生该变量的节点 ID
- 变量从 `startObjectId` 开始，沿正常流方向向下游传播
- 判断逻辑：`isReachableByNormalFlow(schema, startObjectId, targetObjectId)`

#### 规则 3: Loop 作用域

- 循环迭代器变量 (`$currentIndex`, iterator 变量) 具有 `loop` 作用域
- `loopObjectId` 为所属循环节点 ID
- 变量仅在循环体内部可见
- 判断逻辑：`loopAncestorsForObject(schema, objectId).includes(symbol.scope.loopObjectId)`

#### 规则 4: ErrorHandler 作用域

- 错误上下文变量 (`$latestError`, `$latestHttpResponse`, `$latestSoapFault`) 具有 `errorHandler` 作用域
- `errorHandlerFlowId` 为触发错误处理的流 ID
- 变量仅在错误处理流的目标节点及其下游可见
- 判断逻辑：`isReachableByErrorHandlerFlow(schema, flowId, objectId)`

#### 规则 5: Branch 作用域

- 决策分支或对象类型分支中产生的变量具有 `branch` 作用域
- `branchSourceObjectId` 为分支源节点 ID
- `branchFlowId` 为分支流 ID

### 2.4 可见性判断核心算法

**文件**: [variable-scope-engine.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/variables/variable-scope-engine.ts)

```
isVariableVisibleAtObject(schema, symbol, objectId, includeCurrentObject):
  1. visibility === "unavailable" → false
  2. loopObjectId 存在且 objectId 不在循环祖先链中 → false
  3. errorHandler 作用域且不在错误处理流可达范围内 → false
  4. 非 errorHandler 作用域但有 errorHandlerFlowId → false
     (正常流变量不能在错误处理流的作用域内使用)
  5. startObjectId 不存在 → true (全局变量)
  6. includeCurrentObject === false 且 startObjectId === objectId → false
     (变量在产生自己的节点上不可用，仅在下游可用)
  7. errorHandler 作用域 → isInErrorScope 判断
  8. 其他 → isReachableByNormalFlow 判断
```

### 2.5 Merge 节点的交集语义（⚠️ 高 Bug 风险区）

Merge 节点 (`exclusiveMerge`) 是变量作用域中最容易出 bug 的地方，核心原因是**多分支汇聚时的变量可见性需要做交集计算**。

#### 问题场景

```
Start → Decision → [True branch: CreateVariable X] → Merge → Use X ???
                  → [False branch: (no X)]          ↗
```

在上述场景中：
- True 分支创建了变量 X
- False 分支没有创建变量 X
- Merge 节点之后，X 的可见性为 `maybe`（因为不是所有路径都赋值了 X）

#### 交集计算算法

`getVariableVisibilityAtObject` 函数的核心逻辑：

```typescript
function getVariableVisibilityAtObject(schema, index, variableName, objectId): MicroflowVariableVisibility {
  // 1. 找到所有同名变量中在当前 objectId 可见的
  const symbols = (index.byName?.[variableName] ?? [])
    .filter(symbol => isVariableVisibleAtObject(schema, symbol, objectId, true));

  if (!symbols.length) return "unavailable";

  // 2. 如果任一 symbol 的 visibility 为 "maybe"，
  //    或者产生变量的节点不是当前节点的支配节点，
  //    则可见性降级为 "maybe"
  return symbols.some(symbol =>
    symbol.visibility === "maybe" ||
    !isDominatingApprox(schema, symbol.scope.startObjectId, objectId)
  ) ? "maybe" : "definite";
}
```

#### 支配节点 (Dominating Node) 判断

`isDominatingApprox` 使用 `getDominatingObjectsApprox` 近似计算支配节点：

```typescript
function getDominatingObjectsApprox(schema, objectId, collectionId): string[] {
  // 1. 获取从 Start 到 objectId 的所有路径
  const paths = getAllPathsToObject(schema, objectId, collectionId);

  // 2. 取所有路径的交集（支配节点 = 每条路径都经过的节点）
  return paths[0].filter(candidate =>
    paths.every(path => path.includes(candidate))
  );
}
```

#### Merge 场景的可见性矩阵

| 场景 | 变量 X 在 True 分支创建 | 变量 X 在 False 分支创建 | Merge 后 X 可见性 |
|------|------------------------|------------------------|------------------|
| 两分支都创建 | ✅ | ✅ | `definite` |
| 仅 True 创建 | ✅ | ❌ | `maybe` |
| 仅 False 创建 | ❌ | ✅ | `maybe` |
| 两分支都不创建 | ❌ | ❌ | `unavailable` |

#### ⚠️ 常见 Bug 模式

1. **Merge 后直接使用 maybe 变量**：用户在 Merge 后直接使用 `maybe` 变量，运行时可能为空
2. **ParallelGateway 的变量泄漏**：并行网关的每个分支独立创建同名变量，汇聚后类型可能不一致
3. **嵌套 Merge 的支配节点误判**：`getDominatingObjectsApprox` 是近似算法，嵌套分支可能导致支配节点计算不准确
4. **错误处理流中的变量遮蔽**：正常流变量和错误处理流变量同名时，作用域判断可能混淆

#### `maybe` 变量的 `maybeReason`

当变量可见性为 `maybe` 时，系统自动生成原因描述：

```typescript
maybeReason: "Variable is not definitely assigned on every normal path to this object."
```

### 2.6 变量索引构建流程

**文件**: [variable-index.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/variables/variable-index.ts)

```
buildVariableIndex(schema, metadata):
  1. 创建空索引
  2. 添加系统变量 ($currentUser, $currentSession) → global 作用域
  3. 遍历 schema.parameters → 添加参数变量 → global 作用域
  4. 遍历 flattenObjects(schema.objectCollection):
     - loopedActivity → addLoopVariables (iterator, $currentIndex) → loop 作用域
     - actionActivity → addActionOutputs → downstream 作用域
  5. 遍历错误处理流 → addErrorContextVariables → errorHandler 作用域
  6. finalizeDiagnostics → 检查变量名冲突、保留字、重复
```

### 2.7 图可达性分析

**文件**: [microflow-graph-analysis.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/variables/microflow-graph-analysis.ts)

| 函数 | 说明 |
|------|------|
| `buildMicroflowGraph` | 构建微流有向图（节点+边） |
| `buildVariableGraphAnalysis` | 构建全量图分析（含嵌套循环） |
| `isReachableByNormalFlow` | BFS 判断正常流可达性 |
| `isReachableByErrorHandlerFlow` | 判断错误处理流可达性 |
| `getDominatingObjectsApprox` | 近似计算支配节点（路径交集） |
| `getAllPathsToObject` | 枚举从 Start 到目标的所有路径（上限 100 条） |
| `getMergePredecessorBranches` | 获取 Merge 节点的前驱分支 |

---

## 三、MicroflowVariableSymbol 完整类型定义

**文件**: [schema/types.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/schema/types.ts) (L1416-L1438)

```typescript
interface MicroflowVariableSymbol {
  id?: string;                              // 符号唯一标识，格式: "{source.kind}:{name}:{objectId|collectionId}"
  name: string;                             // 变量名（如 "NewObject1", "$currentUser"）
  displayName?: string;                     // 显示名称（默认等于 name）
  kind?: MicroflowVariableKind;             // 变量种类
  dataType: MicroflowDataType;              // 数据类型
  type?: MicroflowTypeRef;                  // 类型引用（用于实体/枚举）
  source: MicroflowVariableSource;          // 变量来源
  scope: MicroflowVariableScope;            // 作用域
  visibility?: MicroflowVariableVisibility;  // 可见性 (definite/maybe/unavailable)
  readonly: boolean;                        // 是否只读
  availableFromObjectId?: string;           // 变量可用的起始节点 ID
  availableInCollectionId?: string;         // 变量可用的 ObjectCollection ID
  availableInObjectIds?: string[];          // 变量可用的所有节点 ID 列表
  branchSourceObjectId?: string;            // 分支源节点 ID
  branchFlowId?: string;                    // 分支流 ID
  loopObjectId?: string;                    // 所属循环节点 ID
  errorHandlerFlowId?: string;              // 所属错误处理流 ID
  maybeReason?: string;                     // 可见性为 maybe 的原因
  unavailableReason?: string;               // 不可用的原因
  diagnostics?: MicroflowVariableDiagnostic[];  // 诊断信息
  documentation?: string;                   // 文档说明
}
```

### 3.1 MicroflowVariableKind

```typescript
type MicroflowVariableKind =
  | "parameter"       // 微流输入参数
  | "actionOutput"    // 动作输出（retrieve/createObject 等）
  | "localVariable"   // 局部变量（createVariable）
  | "objectOutput"    // 对象输出（createObject/retrieve 单个对象）
  | "listOutput"      // 列表输出（createList/aggregateList/listOperation）
  | "primitiveOutput" // 原始类型输出
  | "microflowReturn" // 调用微流返回值
  | "restResponse"    // REST 响应变量
  | "loopIterator"    // 循环迭代器
  | "system"          // 系统变量 ($currentUser, $currentSession, $currentIndex)
  | "errorContext"    // 错误上下文 ($latestError)
  | "soapFault"       // SOAP 错误 ($latestSoapFault)
  | "modeledOnly"     // 仅建模（运行时可能不支持）
  | "unknown";        // 未知类型
```

### 3.2 MicroflowVariableSource (判别联合)

```typescript
type MicroflowVariableSource =
  | { kind: "parameter"; parameterId: string }
  | { kind: "actionOutput"; objectId: string; actionId: string; actionKind?: MicroflowActionKind }
  | { kind: "createVariable"; objectId: string; actionId: string }
  | { kind: "createList"; objectId: string; actionId: string }
  | { kind: "aggregateList"; objectId: string; actionId: string }
  | { kind: "listOperation"; objectId: string; actionId: string }
  | { kind: "localVariable"; objectId: string; actionId: string }
  | { kind: "loopIterator"; loopObjectId: string }
  | { kind: "system"; name: "$currentUser" | "$currentSession" | "$currentIndex" }
  | { kind: "errorContext"; flowId: string; sourceObjectId?: string; errorVariable?: "$latestError" | "$latestHttpResponse" | "$latestSoapFault" }
  | { kind: "microflowReturn"; objectId: string; targetMicroflowId: string }
  | { kind: "restResponse"; objectId: string; responseKind: "string" | "json" | "importMapping" | "statusCode" | "headers" }
  | { kind: "modeledOnly"; objectId: string; actionId?: string; actionKind?: MicroflowActionKind }
  | { kind: "unknown"; objectId?: string; actionId?: string; reason?: string };
```

### 3.3 MicroflowVariableIndex

```typescript
interface MicroflowVariableIndex {
  schemaId?: string;                                    // Schema ID
  builtAt?: string;                                     // 构建时间
  metadataVersion?: string;                             // 元数据版本
  all?: MicroflowVariableSymbol[];                      // 所有变量符号
  byName?: Record<string, MicroflowVariableSymbol[]>;   // 按名称索引
  byObjectId?: Record<string, MicroflowVariableSymbol[]>; // 按产生节点的 ID 索引
  byActionId?: Record<string, MicroflowVariableSymbol[]>; // 按 Action ID 索引
  byCollectionId?: Record<string, MicroflowVariableSymbol[]>; // 按 Collection ID 索引
  byScopeKey?: Record<string, MicroflowVariableSymbol[]>; // 按作用域键索引
  diagnostics?: MicroflowVariableDiagnostic[];          // 诊断信息
  graphAnalysis?: MicroflowVariableGraphAnalysis;       // 图分析结果
  parameters: Record<string, MicroflowVariableSymbol>;      // 参数变量
  localVariables: Record<string, MicroflowVariableSymbol>;  // 局部变量
  objectOutputs: Record<string, MicroflowVariableSymbol>;   // 对象输出
  listOutputs: Record<string, MicroflowVariableSymbol>;     // 列表输出
  loopVariables: Record<string, MicroflowVariableSymbol>;   // 循环变量
  errorVariables: Record<string, MicroflowVariableSymbol>;  // 错误变量
  systemVariables: Record<string, MicroflowVariableSymbol>; // 系统变量
}
```

### 3.4 MicroflowVariableDiagnostic

```typescript
interface MicroflowVariableDiagnostic {
  id: string;
  severity: "error" | "warning" | "info";
  code: string;           // 如 "MF_VARIABLE_NAME_REQUIRED", "MF_VARIABLE_DUPLICATED"
  message: string;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  fieldPath?: string;
  variableName?: string;
}
```

### 3.5 变量名验证规则

| 诊断码 | 严重度 | 规则 |
|--------|--------|------|
| `MF_VARIABLE_NAME_REQUIRED` | error | 变量名不能为空 |
| `MF_VARIABLE_NAME_SYSTEM_RESERVED` | error | 变量名不能以 `$` 开头 |
| `MF_VARIABLE_NAME_INVALID` | error | 变量名必须以字母或下划线开头，只含字母/数字/下划线 |
| `MF_VARIABLE_NAME_RESERVED` | error | 变量名与系统保留变量冲突 |
| `MF_VARIABLE_DUPLICATED` | error | 同名变量重复定义 |
| `MF_VARIABLE_PARAMETER_CONFLICT` | warning | 变量名与参数名冲突 |
| `MF_VARIABLE_OUTPUT_TYPE_UNKNOWN` | warning | 输出变量类型未知 |
| `MF_VARIABLE_OUTPUT_MODELED_ONLY` | warning | 输出变量仅为建模用途 |
| `MF_VARIABLE_METADATA_ENTITY_NOT_FOUND` | warning | 实体元数据缺失 |
| `MF_VARIABLE_METADATA_ASSOCIATION_NOT_FOUND` | warning | 关联元数据缺失 |
| `MF_VARIABLE_MICROFLOW_RETURN_VOID` | warning | 微流返回 void 但配置了输出变量 |
| `MF_VARIABLE_MICROFLOW_RETURN_UNKNOWN` | warning | 微流返回类型未知 |
| `MF_VARIABLE_LOOP_ITERATOR_REQUIRED` | warning | 循环迭代器变量名缺失 |
| `MF_VARIABLE_LOOP_ENTITY_UNKNOWN` | warning | 循环迭代器实体元数据缺失 |
| `MF_VARIABLE_REST_RESPONSE_UNKNOWN` | warning | REST 响应类型未知 |
| `MF_VARIABLE_TYPE_MISMATCH` | error | 变量类型不匹配 |
| `MF_LIST_ENTITY_METADATA_PENDING` | warning | 列表元素类型实体元数据缺失 |
| `MF_ACTION_CONFIG_MISSING` | warning | 动作配置缺失 |

---

## 四、属性面板宽度与开关行为约定

### 4.1 面板尺寸常量

**文件**: [editor/index.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx)

```typescript
const RIGHT_DOCK_PANEL_WIDTH_PX = 380;      // 右侧面板宽度（属性面板）
const NODE_TOOLBOX_PANEL_WIDTH_PX = 380;     // 左侧面板宽度（节点面板）= 右侧面板宽度
const RIGHT_PANEL_EXPANDED_PX = 380;         // 右侧面板展开宽度 = 右侧面板宽度
const BOTTOM_STRIP_HEIGHT_PX = 32;           // 底部状态条高度
const BOTTOM_DOCK_PEEK_HEIGHT_PX = 260;      // 底部面板半展开高度
const BOTTOM_DOCK_FULL_DEFAULT_PX = 420;     // 底部面板全展开默认高度
```

### 4.2 面板布局模型

编辑器采用 CSS Grid 布局，右侧列宽度根据面板状态动态切换：

```typescript
const rightCol = focusMode || !AUXILIARY_PANELS_ENABLED
  ? 0                                           // 聚焦模式：隐藏面板
  : leftOpen
    ? NODE_TOOLBOX_PANEL_WIDTH_PX               // 节点面板打开：380px
    : rightOpen
      ? RIGHT_PANEL_EXPANDED_PX                 // 属性面板打开：380px
      : 0;                                      // 都关闭：0px

// Grid 布局
gridTemplateColumns: `minmax(0, 1fr) ${rightCol}px`
// 过渡动画
transition: "grid-template-columns 250ms cubic-bezier(0.4, 0, 0.2, 1)"
```

### 4.3 面板互斥规则

**关键约束：节点面板和属性面板互斥，不能同时打开。**

```typescript
// 互斥逻辑
useEffect(() => {
  if (leftOpen && rightOpen) {
    setRightOpen(false);  // 左面板打开时强制关闭右面板
  }
}, [leftOpen, rightOpen]);
```

### 4.4 面板开关函数

| 函数 | 行为 |
|------|------|
| `openNodePanel()` | `setLeftOpen(true); setRightOpen(false);` |
| `closeNodePanel()` | `setLeftOpen(false);` |
| `openPropertiesPanel()` | `setLeftOpen(false); setRightOpen(true);` |
| `closePropertiesPanel()` | `setRightOpen(false);` |
| 工具栏切换节点面板 | `setLeftOpen(value => !value);` |

### 4.5 面板状态持久化

面板开关状态持久化到 `localStorage`：

| Key | 说明 |
|-----|------|
| `atlas_microflow_panel_left_open` | 节点面板开关 |
| `atlas_microflow_panel_right_open` | 属性面板开关 |
| `atlas_microflow_panel_bottom_open` | 底部面板开关 |
| `atlas_microflow_panel_bottom_tab` | 底部面板活动标签 |
| `lowcode-studio:mendix-layout:v1` | Mendix 布局状态（含 nodesDrawerOpen, inspectorOpen, bottomHeight） |

### 4.6 属性面板打开时机

| 触发条件 | 行为 |
|---------|------|
| 选中节点 | 自动打开属性面板（如果未打开） |
| 选中连线 | 自动打开属性面板 |
| 内联编辑提交 | 打开属性面板并聚焦第一个输入框 |
| `focus-node` intent | 打开属性面板并聚焦目标节点 |
| 点击关闭按钮 | 关闭属性面板 |
| 取消选中 | 属性面板保持打开，显示文档属性 |

### 4.7 属性面板内部结构

```
MicroflowPropertyPanel
  ├── 未选中 → MicroflowDocumentPropertiesForm (微流文档属性)
  ├── 选中连线 → FlowEdgeForm (连线属性)
  └── 选中节点 → ObjectPanel (节点属性)
        └── node-form-registry 查找对应表单
              ├── ActionActivityForm
              ├── ExclusiveSplitForm
              ├── LoopNodeForm
              └── ... (16 种表单)
```

属性面板内部表单控件强制 `width: 100%; max-width: 100%`，通过 CSS 选择器：

```css
[data-testid="microflow-property-panel"] .semi-input-wrapper,
[data-testid="microflow-property-panel"] .semi-input,
[data-testid="microflow-property-panel"] .semi-textarea-wrapper,
[data-testid="microflow-property-panel"] textarea,
[data-testid="microflow-property-panel"] .semi-select,
[data-testid="microflow-property-panel"] .semi-select-selection {
  box-sizing: border-box;
  width: 100%;
  max-width: 100%;
}
```

---

## 五、节点面板卡片渲染规则

### 5.1 卡片基础样式

**文件**: [node-panel/index.tsx](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx)

```typescript
const cardBaseStyle: CSSProperties = {
  position: "relative",
  display: "flex",
  gap: 8,
  alignItems: "center",
  width: "100%",
  minHeight: 34,
  padding: "6px 8px",
  border: "1px solid transparent",
  borderRadius: 6,
  background: "transparent",
  boxSizing: "border-box",
  textAlign: "left",
  transition: "background 120ms ease, border-color 120ms ease",
  userSelect: "none"
};
```

### 5.2 engineSupport 对应 UI 样式

节点面板中 `engineSupport.level` 通过**筛选机制**而非直接样式映射影响 UI：

#### 筛选器映射

| 筛选 Key | 条件 | 说明 |
|---------|------|------|
| `"all"` | 无过滤 | 显示所有节点 |
| `"favorites"` | `favoriteSet.has(key)` | 仅显示收藏节点 |
| `"enabled"` | `canDragRegistryItem(item)` | 仅显示可拖拽节点 |
| `"supported"` | `item.engineSupport?.level === "supported"` | 仅显示运行时完全支持的节点 |

#### 禁用状态判断

`getMicroflowNodeDisabledReason` 函数按优先级判断节点是否禁用：

```
1. context.microflowId 不存在 → "Open a microflow to add nodes."
2. context.schemaLoaded === false → "Microflow schema is loading."
3. context.readonly → "This microflow is read-only."
4. entry.actionKind 不在 context.supportedActionKinds 中 → "Not supported in current release."
5. entry.featureStatus === "unsupported" → entry.disabledReason ?? "Not supported in current release."
6. entry.disabled(context) 返回 true → entry.disabledReason ?? "Node is disabled."
7. entry.disabled === true → entry.disabledReason ?? "Node is disabled."
8. getDisabledDragReason(entry):
   a. entry.enabled === false → "Node is disabled."
   b. entry.availability === "nanoflowOnlyDisabled" → "Nanoflow-only node cannot be used in Microflow."
   c. entry.availability === "requiresConnector" → "Connector is required before this node can be used."
```

#### 禁用卡片的 UI 表现

```typescript
const disabled = Boolean(disabledReason);

const cardStyle: CSSProperties = {
  ...cardBaseStyle,
  opacity: disabled ? 0.58 : 1,                    // 禁用时降低透明度
  cursor: disabled ? "not-allowed" : dragging ? "grabbing" : "grab",  // 禁用时显示禁止光标
};
```

禁用卡片：
- **不可拖拽** (`draggable={false}`)
- **不可键盘聚焦** (`tabIndex={-1}`)
- **不可双击添加**
- **降低透明度** (0.58)
- **显示禁止光标** (not-allowed)

### 5.3 卡片交互状态样式

| 状态 | 背景 | 边框 | 阴影 | 变换 |
|------|------|------|------|------|
| 默认 | transparent | transparent | none | translateY(0) |
| Hover/Active | `--semi-color-fill-0` | `--semi-color-border` | `0 6px 14px rgba(31,35,41,0.08)` | translateY(-1px) |
| 收藏+Hover | `rgba(255,247,217,0.98)` | `rgba(255,177,0,0.66)` | 同上 | 同上 |
| 收藏+默认 | `rgba(255,250,232,0.4)` | `rgba(255,177,0,0.38)` | none | translateY(0) |
| 拖拽中 | 继承 | 继承 | `0 14px 28px rgba(31,35,41,0.18), 0 0 0 1px rgba(22,93,255,0.18)` | translateY(-6px) scale(1.02) |

### 5.4 图标色调映射

节点图标根据 `group` 和 `subgroup` 分配色调：

| 分组 | 背景色 | 前景色 | 示例节点 |
|------|--------|--------|---------|
| Events | `#e8f8ef` | `#12b886` | Start, End, Error |
| Decisions | `#fff7e8` | `#ff8800` | Decision, Merge, Gateway |
| Object (subgroup) | `#eef4ff` | `#165dff` | CreateObject, Retrieve |
| List (subgroup) | `#fff9db` | `#d48806` | CreateList, Aggregate |
| Call (subgroup) | `#f2edff` | `#722ed1` | CallMicroflow |
| Variable (subgroup) | `#e6fffb` | `#13a8a8` | CreateVariable |
| Client (subgroup) | `#f0f8e8` | `#52c41a` | ShowPage, ShowMessage |
| Integration (subgroup) | `#fff1f0` | `#f93920` | RestCall, WebService |
| 默认 | `#f2f3f5` | `#4e5969` | 其他 |

### 5.5 卡片布局模式

| 模式 | minHeight | padding | flexDirection | 图标大小 | 文字排列 |
|------|-----------|---------|---------------|---------|---------|
| `list` (默认) | 34px | 6px 8px | row | 22px | 单行，标题左+中文右 |
| `toolbox` | 86px | 10px 6px 8px | column | 32px | 双行，标题居中 |
| `compact` | 30px | 4px 8px | row | 22px | 单行，仅标题 |

### 5.6 节点面板分区结构

节点面板采用 Mendix 风格的固定分区 + 自动回退分区：

```typescript
const mendixToolboxSections = [
  { key: "object",     label: "Object activities",    itemKeys: ["activity:objectCreate", "activity:objectChange", ...] },
  { key: "list",       label: "List activities",      itemKeys: ["activity:listAggregate", "activity:listCreate", ...] },
  { key: "action",     label: "Action call activities", itemKeys: ["activity:callMicroflow", "activity:callRest", ...] },
  { key: "variable",   label: "Variable activities",  itemKeys: ["activity:variableCreate", "activity:variableChange"] },
  { key: "flow",       label: "Flow control",         itemKeys: ["decision", "merge", "loop", ...] },
  { key: "input",      label: "Input parameters",     itemKeys: ["parameter"] },
  { key: "event",      label: "Loop events",          itemKeys: ["startEvent", "endEvent", ...] },
  { key: "documentation", label: "Documentation",     itemKeys: ["annotation"] },
];
```

未匹配到固定分区的节点自动按 `categoryKey` 回退分组。

---

> **文档维护**: 本补充文档与 `CODE_WIKI.md` 配合使用，覆盖变量作用域引擎、属性面板约定和节点面板渲染的深层细节。最后更新: 2026-05-13
