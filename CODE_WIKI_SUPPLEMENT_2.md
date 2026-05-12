# Code Wiki 补充文档（二）：字段写入路径、拖拽创建链路、前后端变量对齐

> 生成日期: 2026-05-13
> 关联主文档: `CODE_WIKI.md`、`CODE_WIKI_SUPPLEMENT.md`
> 范围: 属性面板字段级写入路径、节点面板拖拽创建链路、前端变量系统与后端 Runtime 变量系统对齐

---

## 一、属性面板字段级写入路径

### 1.1 Patch 机制概述

属性面板通过 `onPatch` 回调统一写入 Schema。Patch 对象分三层：

```typescript
interface MicroflowDesignPatch {
  object?: Partial<MicroflowObject>;       // 对象基础属性
  config?: Partial<MicroflowObjectConfig>;  // 对象配置（动作参数等）
  flow?: Partial<MicroflowFlow>;           // 连线属性
  document?: Partial<MicroflowDocumentConfig>; // 文档属性
}
```

`applyDesignObjectPatch` 将 Patch 映射到 `MicroflowDesignSchema`：

```
patch.object  → schema.objectCollection[*].objects[id]       (对象级)
patch.config  → schema.objectCollection[*].objects[id].config (配置级)
patch.flow    → schema.objectCollection[*].flows[id]         (连线级)
patch.document → schema (根级属性)                            (文档级)
```

### 1.2 对象基础属性写入路径 (patch.object)

所有节点类型共享的基础属性，写入 `schema.objectCollection[*].objects[id]`：

| 字段 | Schema Path | 类型 | 说明 | 适用节点 |
|------|-----------|------|------|---------|
| `caption` | `objects[id].caption` | `string` | 节点标题/名称 | 所有 |
| `documentation` | `objects[id].documentation` | `string` | 文档说明 | 所有 |
| `autoGenerateCaption` | `objects[id].autoGenerateCaption` | `boolean` | 自动生成标题 | ActionActivity |
| `x` | `objects[id].x` | `number` | X 坐标 | 所有 |
| `y` | `objects[id].y` | `number` | Y 坐标 | 所有 |
| `width` | `objects[id].width` | `number` | 宽度 | Annotation |
| `height` | `objects[id].height` | `number` | 高度 | Annotation |

### 1.3 动作活动表单写入路径 (ActionActivityForm)

写入 `objects[id].config.action` 子对象：

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 实体选择 | `config.action.entityId` | `string` | 目标实体 ID |
| 实体限定名 | `config.action.entityQualifiedName` | `string` | 实体全限定名 |
| 输出变量名 | `config.outputVariableName` | `string` | 输出变量名 |
| 输出变量类型 | `config.outputVariableType` | `MicroflowDataType` | 输出变量数据类型 |
| 错误变量名 | `config.errorVariableName` | `string` | 错误处理变量名 |
| 错误处理类型 | `config.errorHandlingType` | `string` | 错误处理策略 ("custom" / "all" / "none") |
| 提交方式 | `config.action.commit` | `string` | 提交模式 ("yes" / "no" / "yesWithoutEvents") |
| 刷新模式 | `config.action.refreshInClient` | `boolean` | 是否在客户端刷新 |
| 回滚 | `config.action.rollback` | `boolean` | 是否回滚 |
| 检索方式 | `config.action.retrieveMethod` | `string` | 检索方式 ("first" / "last" / "all") |
| XPath 约束 | `config.action.xpathConstraint` | `string` | XPath 约束表达式 |
| 范围 | `config.action.range` | `string` | 检索范围 |
| 目标微流 ID | `config.action.targetMicroflowId` | `string` | 调用微流的目标 ID |
| 目标微流名 | `config.action.targetMicroflowName` | `string` | 目标微流名 |
| 参数映射 | `config.action.parameterMappings` | `MicroflowParameterMapping[]` | 参数映射列表 |
| REST URL | `config.action.url` | `string` | REST 请求 URL |
| HTTP 方法 | `config.action.httpMethod` | `string` | HTTP 方法 |
| 请求头 | `config.action.headers` | `KeyValueItem[]` | 请求头 |
| 请求体 | `config.action.body` | `string` | 请求体表达式 |
| 响应类型 | `config.action.responseType` | `string` | 响应处理方式 |
| 映射 ID | `config.action.mappingId` | `string` | 导入/导出映射 ID |
| 页面 ID | `config.action.pageId` | `string` | 显示页面 ID |
| 页面名 | `config.action.pageName` | `string` | 页面名称 |
| 消息模板 | `config.action.messageTemplate` | `string` | 消息模板 |
| 消息类型 | `config.action.messageType` | `string` | 消息类型 ("info"/"warning"/"error") |
| 日志级别 | `config.action.logLevel` | `string` | 日志级别 |
| 日志节点 | `config.action.logNode` | `string` | 日志节点 |

### 1.4 决策节点表单写入路径 (ExclusiveSplitForm)

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 条件表达式 | `config.conditionExpression` | `string` | 决策条件表达式 |
| 条件类型 | `config.conditionType` | `string` | 条件类型 ("expression"/"variable") |

### 1.5 对象类型决策表单写入路径 (InheritanceSplitForm)

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 变量名 | `config.variableName` | `string` | 判断对象类型的变量名 |
| 实体 ID | `config.entityId` | `string` | 基础实体 ID |
| 实体限定名 | `config.entityQualifiedName` | `string` | 实体全限定名 |

### 1.6 循环节点表单写入路径 (LoopNodeForm)

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 循环类型 | `config.loopType` | `string` | 循环类型 ("iterableList"/"whileCondition") |
| 集合变量名 | `config.collectionVariableName` | `string` | 迭代列表变量名 |
| 迭代器变量名 | `config.iteratorVariableName` | `string` | 迭代器变量名 |
| 索引变量名 | `config.indexVariableName` | `string` | 索引变量名（$currentIndex） |
| While 条件 | `config.whileCondition` | `string` | While 循环条件表达式 |

### 1.7 参数表单写入路径 (ParameterObjectForm)

参数同时写入两个位置：

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 参数名 | `schema.parameters[idx].name` | `string` | 参数名 |
| 参数类型 | `schema.parameters[idx].dataType` | `MicroflowDataType` | 数据类型 |
| 参数类型引用 | `schema.parameters[idx].type` | `MicroflowTypeRef` | 类型引用 |
| 是否必填 | `schema.parameters[idx].required` | `boolean` | 是否必填 |
| 参数文档 | `schema.parameters[idx].documentation` | `string` | 参数文档 |
| 节点标题 | `objects[id].caption` | `string` | 节点显示名 |

### 1.8 连线属性表单写入路径 (FlowEdgeForm)

写入 `schema.objectCollection[*].flows[id]`：

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| Case 值 | `flows[id].caseValue` | `string` | 分支条件值 (True/False/自定义) |
| Case 值类型 | `flows[id].caseValueKind` | `string` | 条件值类型 ("boolean"/"objectType"/"enumeration") |
| 连线标签 | `flows[id].label` | `string` | 连线标签 |
| 错误处理变量 | `flows[id].errorVariableName` | `string` | 错误处理变量名 |

### 1.9 错误处理器表单写入路径 (ErrorHandlerForm)

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 错误处理类型 | `config.errorHandlingType` | `string` | 错误处理策略 |
| 错误变量名 | `config.errorVariableName` | `string` | 错误上下文变量名 |

### 1.10 Try/Catch 表单写入路径 (TryCatchForm)

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 错误变量名 | `config.errorVariableName` | `string` | 错误上下文变量名 |
| 错误处理类型 | `config.errorHandlingType` | `string` | 错误处理策略 |

### 1.11 事件节点表单写入路径 (EventNodesForm)

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 返回值表达式 | `config.returnValueExpression` | `string` | EndEvent 返回值 |
| 返回值类型 | `config.returnValueDataType` | `MicroflowDataType` | 返回值类型 |

### 1.12 注释表单写入路径 (AnnotationObjectForm)

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 注释文本 | `objects[id].caption` | `string` | 注释内容 |
| 宽度 | `objects[id].width` | `number` | 注释框宽度 |
| 高度 | `objects[id].height` | `number` | 注释框高度 |

### 1.13 文档属性表单写入路径 (MicroflowDocumentPropertiesForm)

写入 Schema 根级属性：

| 表单字段 | Schema Path | 类型 | 说明 |
|---------|-----------|------|------|
| 微流名称 | `schema.name` | `string` | 微流名称 |
| 微流文档 | `schema.documentation` | `string` | 微流文档说明 |
| 返回类型 | `schema.returnType` | `MicroflowDataType` | 返回值类型 |
| 返回类型引用 | `schema.returnTypeRef` | `MicroflowTypeRef` | 返回类型引用 |
| 标记 | `schema.markers` | `string[]` | 标记列表 |

### 1.14 applyDesignObjectPatch 核心逻辑

```typescript
function applyDesignObjectPatch(schema, objectId, patch): MicroflowDesignSchema {
  let next = { ...schema };

  // 1. 应用 object 级 patch
  if (patch.object) {
    next = updateObjectInCollection(next, objectId, obj => ({
      ...obj,
      ...patch.object,
    }));
  }

  // 2. 应用 config 级 patch
  if (patch.config) {
    next = updateObjectInCollection(next, objectId, obj => ({
      ...obj,
      config: { ...obj.config, ...patch.config },
    }));
  }

  // 3. 应用 flow 级 patch
  if (patch.flow) {
    next = updateFlowInCollection(next, flowId, flow => ({
      ...flow,
      ...patch.flow,
    }));
  }

  // 4. 应用 document 级 patch
  if (patch.document) {
    next = { ...next, ...patch.document };
  }

  return next;
}
```

---

## 二、节点面板拖拽创建链路

### 2.1 完整调用链

```
用户拖拽节点卡片
  │
  ├── 1. onDragStart → 生成 MicroflowNodeDragPayload
  │     └── node-registry/drag-drop.ts: buildDragPayload()
  │
  ├── 2. onDragOver → 验证放置目标
  │     └── FlowGram 画布计算鼠标位置
  │
  ├── 3. onDrop → 触发创建
  │     └── editor/index.tsx: handleDrop()
  │           │
  │           ├── getDropTargetCollectionId(schema, position, parentLoopObjectId)
  │           │     → 确定 collectionId
  │           │
  │           ├── addMicroflowObjectFromDragPayload(input)
  │           │     │
  │           │     ├── validateDropAllowedInCollection(schema, payload, collectionId)
  │           │     │     → 验证放置合法性
  │           │     │
  │           │     ├── canDragRegistryItem(registryItem)
  │           │     │     → 检查节点是否可拖拽
  │           │     │
  │           │     ├── createObjectFromNodeRegistry() 或 createActionActivityFromActionRegistry()
  │           │     │     → 创建 MicroflowObject 实例
  │           │     │
  │           │     └── applyEditorGraphPatchToAuthoring(schema, patch)
  │           │           → 将对象插入 Schema
  │           │
  │           └── onSchemaChange(nextSchema, "add-object")
  │                 → 通知上层 Schema 变更
  │
  └── 4. 画布刷新 → FlowGram 重新渲染
```

### 2.2 collectionId 确定逻辑

**文件**: [drag-drop.ts](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/mendix/mendix-microflow/src/node-registry/drag-drop.ts)

```typescript
function getDropTargetCollectionId(
  schema: MicroflowSchema,
  _position: MicroflowPoint,
  parentLoopObjectId?: string,
): string {
  if (!parentLoopObjectId) {
    // 没有指定父循环节点 → 放到根 ObjectCollection
    return schema.objectCollection.id;
  }
  // 指定了父循环节点 → 放到该循环的 ObjectCollection
  const loop = findObjectWithCollection(schema, parentLoopObjectId)?.object;
  return loop?.kind === "loopedActivity"
    ? loop.objectCollection.id
    : schema.objectCollection.id;
}
```

**collectionId 判定规则**:

| 条件 | collectionId | 说明 |
|------|-------------|------|
| 无 `parentLoopObjectId` | `schema.objectCollection.id` | 放到根级 |
| 有 `parentLoopObjectId` 且对应 Loop | `loop.objectCollection.id` | 放到循环体内 |
| 有 `parentLoopObjectId` 但找不到 Loop | `schema.objectCollection.id` | 降级到根级 |

**parentLoopObjectId 的来源**:

- 画布拖拽时，FlowGram 引擎根据鼠标位置判断是否在循环节点区域内
- 如果鼠标在 Loop 节点的 `objectCollection` 区域内，`parentLoopObjectId` 被设为该循环节点的 ID
- 否则 `parentLoopObjectId` 为 `undefined`

### 2.3 放置验证规则

`validateDropAllowedInCollection` 函数验证放置合法性：

| 规则 | 条件 | 错误信息 |
|------|------|---------|
| Collection 存在 | `getObjectCollectionById(schema, collectionId)` 失败 | "Drop target collection does not exist." |
| 事件不能进循环 | `!isRoot && (startEvent \|\| endEvent)` | "Start / End events cannot be placed inside Loop." |
| ErrorEvent 不能进循环 | `!isRoot && errorEvent` | "ErrorEvent cannot be placed inside Loop in this version." |
| Parameter 不能进循环 | `!isRoot && parameterObject` | "ParameterObject cannot be placed inside Loop." |
| Break/Continue 只能在循环内 | `isRoot && (breakEvent \|\| continueEvent)` | "Break / Continue can only be placed inside Loop." |
| StartEvent 唯一性 | 已有 startEvent | "A microflow can only have one Start Event." |

### 2.4 对象创建流程

#### 普通节点创建

```typescript
const object = createObjectFromNodeRegistry({
  registryKey: payload.registryKey,     // 如 "decision", "merge", "loop"
  position,                              // { x, y } 鼠标位置
  id: createUniqueMicroflowObjectId(schema, payload.registryKey),
}).object;
```

`createObjectFromNodeRegistry` 内部逻辑：
1. 从 `microflowNodeRegistryByKey` 查找注册表项
2. 调用注册表项的 `createObject` 函数
3. 合并 `defaultNodeConfig` 中的默认值
4. 设置 `kind`, `id`, `stableId`, `x`, `y`, `caption`
5. 为 Loop 类型创建嵌套 `objectCollection`

#### 动作活动节点创建

```typescript
const object = createActionActivityFromActionRegistry({
  actionRegistryKey: payload.actionKind,  // 如 "objectCreate", "callMicroflow"
  position,
  id: createUniqueMicroflowObjectId(schema, `activity-${payload.actionKind}`)
});
```

`createActionActivityFromActionRegistry` 内部逻辑：
1. 从 `defaultMicroflowActionPanelRegistry` 查找动作注册项
2. 创建 `kind: "actionActivity"` 的对象
3. 设置 `config.action.kind` 为 `actionKind`
4. 调用 `defaultNodeConfigForAction(actionKind)` 填充默认配置
5. 自动生成 `outputVariableName`（如 "NewObject1", "NewList1"）

#### 参数节点创建

参数节点创建时额外处理：
1. 生成参数名 `nextParameterName(schema)` → "Param1", "Param2", ...
2. 创建 `MicroflowParameter` 对象并添加到 `schema.parameters`
3. 创建对应的参数节点对象
4. 调用 `alignRootParameterObjectsToStartEvent` 对齐参数节点位置

### 2.5 ID 生成规则

```typescript
function createUniqueMicroflowObjectId(schema: MicroflowSchema, prefix: string): string {
  // 格式: "{prefix}_{counter}"
  // 如: "decision_1", "activity-objectCreate_2", "parameter-object-Param1_1"
  // 确保在 schema 全局唯一
}

function createUniqueParameterId(schema: MicroflowSchema): string {
  // 格式: "param_{counter}"
}

function nextParameterName(schema: MicroflowSchema): string {
  // 格式: "Param{counter}"，跳过已存在的参数名
}
```

### 2.6 Patch 应用

```typescript
const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
  addObject: { object, parentLoopObjectId },
  selectedObjectId: object.id,
  selectedFlowId: undefined,
});
```

`applyEditorGraphPatchToAuthoring` 内部：
1. 如果 `parentLoopObjectId` 存在，找到对应的 Loop 节点
2. 将 `object` 插入到 Loop 的 `objectCollection.objects` 中
3. 否则插入到 `schema.objectCollection.objects` 中
4. 更新 `selectedObjectId` 为新创建的对象 ID

### 2.7 addObjectToDropTargetCollection

当需要显式指定 collectionId 时（如从画布内部拖拽）：

```typescript
function addObjectToDropTargetCollection(schema, patch, collectionId) {
  if (!collectionId || collectionId === schema.objectCollection.id || !patch.addObject) {
    return applyEditorGraphPatchToAuthoring(schema, patch);
  }
  const parentLoopObjectId = parentLoopObjectIdForCollection(schema, collectionId);
  return applyEditorGraphPatchToAuthoring(schema, {
    ...patch,
    addObject: { ...patch.addObject, parentLoopObjectId },
  });
}
```

---

## 三、前端变量系统与后端 Runtime 变量系统对齐

### 3.1 架构对照总览

| 维度 | 前端设计期 | 后端 Runtime |
|------|-----------|-------------|
| **变量存储** | `MicroflowVariableIndex` (只读索引) | `MicroflowVariableStore` (可读写存储) |
| **作用域模型** | `MicroflowVariableScope` (静态分析) | `MicroflowVariableScopeStack` (运行时栈) |
| **可见性判断** | `isVariableVisibleAtObject` (图可达性) | `ScopeStack.TryGet` (栈帧遍历) |
| **变量类型** | `MicroflowDataType` (设计期类型) | `RuntimeTypeDescriptor` (运行时类型) |
| **变量值** | 无值（仅声明） | `RuntimeVariableValue` (含实际值) |
| **作用域种类** | global/downstream/loop/errorHandler/branch | Global/Parameter/NodeLocal/Action/Loop/ErrorHandler/Call/ParallelBranch/BranchMerge/System/Downstream |
| **分支合并** | `getDominatingObjectsApprox` (近似支配节点) | `DefaultBranchMergePolicy.Merge` (实际值合并) |

### 3.2 作用域种类对齐

| 前端 ScopeKind | 后端 ScopeKind | 对齐关系 |
|---------------|---------------|---------|
| `global` | `Global` | ✅ 完全对齐 |
| `downstream` | `Downstream` / `Action` | ⚠️ 前端 `downstream` 对应后端 `Action` scope（动作输出变量） |
| `loop` | `Loop` | ✅ 完全对齐 |
| `errorHandler` | `ErrorHandler` | ✅ 完全对齐 |
| `branch` | `ParallelBranch` | ⚠️ 前端 `branch` 对应后端 `ParallelBranch`（并行分支） |
| - | `Parameter` | 后端独有，参数初始化时使用 |
| - | `NodeLocal` | 后端独有，节点局部临时变量 |
| - | `Call` | 后端独有，微流调用栈帧 |
| - | `BranchMerge` | 后端独有，分支合并后的变量 |
| - | `System` | 后端独有，系统变量 |

### 3.3 变量类型对齐

| 前端 MicroflowDataType.kind | 后端 RuntimeTypeDescriptor.Kind | 对齐关系 |
|---------------------------|-------------------------------|---------|
| `string` | `Primitive(primitiveKind: "string")` | ✅ |
| `integer` | `Primitive(primitiveKind: "integer")` | ✅ |
| `decimal` | `Primitive(primitiveKind: "decimal")` | ✅ |
| `boolean` | `Primitive(primitiveKind: "boolean")` | ✅ |
| `dateTime` | `Primitive(primitiveKind: "dateTime")` | ✅ |
| `object` | `Entity(qualifiedName, generalization, specializations)` | ✅ |
| `list` | `List(itemType)` | ✅ |
| `enumeration` | `Primitive(primitiveKind: "string")` + 枚举约束 | ⚠️ 后端存储为 string |
| `json` | 后端 `MicroflowRuntimeVariableKind.Json` | ✅ |
| - | `ExternalObjectRef` | 后端独有，外部连接器对象 |
| - | `FileRef` | 后端独有，文件引用 |
| - | `RuntimeCommandValue` | 后端独有，运行时命令 |

### 3.4 变量源种类对齐

| 前端 VariableSource.kind | 后端 VariableSourceKind | 对齐关系 |
|-------------------------|------------------------|---------|
| `parameter` | `Parameter` | ✅ |
| `actionOutput` | `ActionOutput` | ✅ |
| `localVariable` | `LocalVariable` | ✅ |
| `loopIterator` | `LoopIterator` | ✅ |
| `system` | `System` | ✅ |
| `errorContext` | `ErrorContext` | ✅ |
| `restResponse` | `RestResponse` | ✅ |
| `microflowReturn` | `MicroflowReturn` | ✅ |
| `modeledOnly` | `ModeledOnly` | ✅ |
| `unknown` | `Unknown` | ✅ |

### 3.5 Runtime 变量读取机制

#### MicroflowVariableScopeStack

后端运行时使用**栈式作用域**管理变量可见性：

```csharp
public sealed class MicroflowVariableScopeStack
{
    private readonly List<MicroflowVariableScopeFrame> _frames = [];

    // 初始化时创建 Global 帧
    public MicroflowVariableScopeStack()
    {
        _frames.Add(new MicroflowVariableScopeFrame
        {
            Kind = MicroflowVariableScopeKind.Global,
        });
    }

    // 变量查找：从栈顶向栈底遍历，找到第一个匹配的
    public bool TryGet(string name, out MicroflowRuntimeVariableValue? value, out MicroflowVariableScopeFrame? scope)
    {
        for (var index = _frames.Count - 1; index >= 0; index--)
        {
            var frame = _frames[index];
            if (frame.Variables.TryGetValue(name, out var found))
            {
                value = found;
                scope = frame;
                return true;
            }
        }
        value = null;
        scope = null;
        return false;
    }

    // 可见变量：合并所有帧的变量（后帧覆盖前帧同名变量）
    public IReadOnlyDictionary<string, MicroflowRuntimeVariableValue> VisibleVariables()
    {
        var result = new Dictionary<string, MicroflowRuntimeVariableValue>(StringComparer.Ordinal);
        foreach (var frame in _frames)
        {
            foreach (var variable in frame.Variables)
            {
                result[variable.Key] = variable.Value;  // 后帧覆盖
            }
        }
        return result;
    }
}
```

#### 变量查找规则

| 操作 | 方法 | 行为 |
|------|------|------|
| 读取变量 | `TryGet(name)` | 从栈顶向栈底遍历，返回第一个匹配 |
| 写入变量 | `Set(name, value)` | 查找变量定义帧，原地更新（不允许跨帧写入） |
| 定义变量 | `Define(definition)` | 在当前帧（栈顶）创建变量 |
| 删除变量 | `Remove(name)` | 从定义帧中删除 |
| 读取所有可见变量 | `VisibleVariables()` | 合并所有帧，后帧覆盖前帧 |

### 3.6 Runtime 变量写入机制

```csharp
public void Set(string name, MicroflowRuntimeVariableValue value)
{
    // 1. 查找变量定义帧
    if (!_scopeStack.TryGet(name, out var existing, out var scope))
        throw new MicroflowVariableStoreException("Variable not found");

    // 2. 只读检查
    if (existing.Readonly || existing.System)
        throw new MicroflowVariableStoreException("Variable is readonly");

    // 3. 类型兼容性检查（不兼容仅 warning，不阻止写入）
    if (!Compatible(existing.DataTypeJson, value.DataTypeJson))
        AddDiagnostic("warning", "Type mismatch");

    // 4. 原地更新（保留原始 ScopeKind、CreatedAt 等）
    scope.Variables[name] = value with
    {
        Name = name,
        Readonly = existing.Readonly,
        System = existing.System,
        ScopeKind = existing.ScopeKind,
        CreatedAt = existing.CreatedAt,
        UpdatedAt = _utcNow(),
    };
}
```

### 3.7 Runtime 变量隔离机制

#### Loop 隔离

每次循环迭代都 Push 一个 `Loop` 作用域帧：

```csharp
// MicroflowLoopExecutor 中
using (loopContext.RuntimeExecutionContext.PushLoopScope(
    loopObjectId,
    collectionId,
    iteratorName,
    iterationCount,
    item,
    preview,
    itemTypeJson,
    defineIterator: true))
{
    var body = await actionContext.LoopBodyExecutor!(iteration, ct);
    // body 执行完毕后，Loop 帧自动 Pop
}
```

- **迭代器变量** (`iteratorName`, `$currentIndex`) 定义在 Loop 帧中
- **循环体输出变量** 定义在 Loop 帧中
- Loop 帧 Pop 后，迭代器变量不可访问
- 循环体的下游变量（在 Loop 帧外定义的）仍然可见

#### Call 隔离

调用微流时 Push 一个 `Call` 作用域帧：

```csharp
using (variableStore.PushScope(new MicroflowVariableScopeFrame
{
    Kind = MicroflowVariableScopeKind.Call,
    CallFrameId = callFrameId,
}))
{
    // 被调用微流的参数和局部变量在此帧中
    // 调用返回后帧自动 Pop
}
```

- 被调用微流的参数和局部变量在 Call 帧中隔离
- 调用返回后，Call 帧被 Pop，被调用微流的变量不可访问
- 返回值通过 `MicroflowReturn` 源类型写入调用者的 Action 帧

#### ErrorHandler 隔离

错误处理流执行时 Push 一个 `ErrorHandler` 作用域帧：

```csharp
using (variableStore.PushScope(new MicroflowVariableScopeFrame
{
    Kind = MicroflowVariableScopeKind.ErrorHandler,
    ErrorHandlerFlowId = errorFlowId,
}))
{
    // $latestError 等错误上下文变量在此帧中
    variableStore.Define(new MicroflowVariableDefinition
    {
        Name = "$latestError",
        SourceKind = MicroflowVariableSourceKind.ErrorContext,
        ScopeKind = MicroflowVariableScopeKind.ErrorHandler,
        Readonly = true,
    });
}
```

- `$latestError` 等错误上下文变量定义在 ErrorHandler 帧中
- 错误处理完成后帧 Pop，错误变量不可访问
- 错误处理流中的正常变量（如修改的对象）通过 `Set` 写回父帧

### 3.8 Runtime 分支变量合并机制

**文件**: [BranchIsolationContracts.cs](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/backend/Atlas.Application.Microflows/Runtime/Branches/BranchIsolationContracts.cs)

#### 分支隔离 (Fork)

并行网关的每个分支 Fork 出独立的变量存储：

```csharp
public sealed class DefaultVariableScopeForker : IVariableScopeForker
{
    public IMicroflowVariableStore Fork(IMicroflowVariableStore source, string branchId)
    {
        var forked = new MicroflowVariableStore();
        // 将源存储的所有可见变量复制到新存储
        foreach (var pair in source.CurrentVariables)
        {
            forked.Define(new MicroflowVariableDefinition
            {
                Name = pair.Key,
                Value = pair.Value with { },  // 浅拷贝
                DataTypeJson = pair.Value.DataTypeJson,
                ScopeKind = MicroflowVariableScopeKind.ParallelBranch,
                AllowShadowing = true  // 允许遮蔽外层同名变量
            });
        }
        return forked;
    }
}
```

#### 分支合并 (Merge)

所有分支执行完毕后，将各分支的写入变量合并回主存储：

```csharp
public sealed class DefaultBranchMergePolicy : IBranchMergePolicy
{
    public BranchMergeResult Merge(IMicroflowVariableStore target, IReadOnlyList<BranchExecutionContext> branches)
    {
        var merged = new List<BranchMergedVariable>();
        foreach (var branch in branches)
        {
            foreach (var variableName in branch.WrittenVariableNames)
            {
                if (!branch.VariableStore.TryGet(variableName, out var value) || value is null)
                    continue;

                var mergedValue = value with
                {
                    ScopeKind = MicroflowVariableScopeKind.BranchMerge,
                };

                // 如果目标存储已有该变量 → 更新
                if (target.Exists(variableName))
                    target.Set(variableName, mergedValue);
                // 如果目标存储没有 → 新建
                else
                    target.Define(new MicroflowVariableDefinition
                    {
                        Name = variableName,
                        Value = mergedValue,
                        ScopeKind = MicroflowVariableScopeKind.BranchMerge,
                        AllowShadowing = true,
                    });

                merged.Add(new BranchMergedVariable { BranchId = branch.BranchId, VariableName = variableName, Value = mergedValue });
            }
        }
        return new BranchMergeResult { Variables = merged };
    }
}
```

#### ⚠️ 合并策略的关键行为

| 场景 | 行为 | 与前端对齐 |
|------|------|-----------|
| 两分支都写入同名变量 | **后写入的分支覆盖先写入的**（按 branches 列表顺序） | ⚠️ 前端标记为 `maybe`，后端取最后一个分支的值 |
| 仅一个分支写入变量 | 变量被添加到目标存储 | ⚠️ 前端标记为 `maybe`，后端变量存在但可能来自非执行分支 |
| 没有分支写入变量 | 变量不变 | ✅ 一致 |
| 分支新建变量（主存储不存在） | 变量被添加到主存储 | ⚠️ 前端标记为 `maybe` 或 `unavailable` |

**核心差异**：前端设计期使用**交集语义**（所有路径都必须赋值才为 `definite`），后端 Runtime 使用**覆盖语义**（最后一个分支的值覆盖前面的）。这导致：
- 前端标记为 `maybe` 的变量，后端可能已有值（来自某个分支）
- 前端标记为 `unavailable` 的变量，后端可能已有值（来自并行分支的新建变量）

### 3.9 Runtime 表达式求值中的变量查找

**文件**: [MicroflowExpressionEvaluator.cs](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionEvaluator.cs)

表达式求值时通过 `MicroflowVariableStore.TryGet` 查找变量：

```
表达式: "$NewObject1/Attribute1"
  │
  ├── 解析变量引用: "$NewObject1"
  │     └── variableStore.TryGet("NewObject1", out var value)
  │           → 从 ScopeStack 栈顶向栈底遍历
  │           → 找到 Action 帧: { Name: "NewObject1", Kind: "object", ... }
  │
  ├── 解析属性路径: "/Attribute1"
  │     └── 从 RuntimeVariableValue.RawValueJson 中读取属性值
  │           或从 EntityQualifiedName 对应的运行时对象中读取
  │
  └── 返回求值结果
```

### 3.10 Runtime 调用栈变量传递

**文件**: [MicroflowCallStackService.cs](file:///d:/Code/Web_SaaS_Backend/SecurityPlatform/src/backend/Atlas.Application.Microflows/Runtime/Calls/MicroflowCallStackService.cs)

```
调用微流 A → 微流 B:
  │
  ├── 1. Push Call 帧
  │     variableStore.PushScope({ Kind: "call", CallFrameId: "call-1" })
  │
  ├── 2. 传入参数
  │     variableStore.Define({ Name: "Param1", SourceKind: "parameter", Value: arg1 })
  │     variableStore.Define({ Name: "Param2", SourceKind: "parameter", Value: arg2 })
  │
  ├── 3. 执行微流 B
  │     → B 的局部变量在 Call 帧中隔离
  │     → B 可读取调用者帧中的变量（通过栈遍历）
  │
  ├── 4. 获取返回值
  │     returnVariable = variableStore.TryGet("$returnValue")
  │
  └── 5. Pop Call 帧
        → B 的所有局部变量被销毁
        → 返回值写入调用者的 Action 帧
```

### 3.11 大对象处理

后端 Runtime 支持大对象引用模式：

```csharp
// 变量定义时检查大小
var useReference = definition.PreferReferenceWhenLarge
    && definition.MemoryBudget is not null
    && estimatedSizeBytes > definition.MemoryBudget.MaxVariableBytes;

// 大对象存储 ValueRef 而非 RawValueJson
value = new MicroflowRuntimeVariableValue
{
    IsLargeObject = useReference,
    RawValueJson = useReference ? null : normalizedRawValueJson,  // 大对象不存原始值
    ValueRef = useReference ? new RuntimeValueRef
    {
        RefKind = "blob",
        SizeBytes = estimatedSizeBytes,
        Preview = TrimPreview(definition.ValuePreview, 200)
    } : null
};
```

### 3.12 前后端变量系统差异总结

| 差异点 | 前端设计期 | 后端 Runtime | 影响 |
|--------|-----------|-------------|------|
| 分支合并语义 | 交集（所有路径赋值 → definite） | 覆盖（最后分支覆盖） | 前端 maybe 变量在后端可能有值 |
| 变量值 | 无值（仅声明+类型） | 有值（RawValueJson / ValueRef） | 设计期无法预知运行时值 |
| 作用域粒度 | 基于图可达性分析 | 基于栈帧遍历 | 前端更保守，后端更精确 |
| 支配节点 | 近似算法（路径上限 100） | 无需计算（栈帧直接可见） | 嵌套分支可能前端误判 |
| 类型系统 | 静态类型（设计期推断） | 动态类型（运行时推断+兼容性检查） | 运行时可能类型不匹配 |
| 错误处理 | 静态可见性分析 | Push/Pop ErrorHandler 帧 | 对齐一致 |
| 循环隔离 | loopAncestors 判断 | Push/Pop Loop 帧 | 对齐一致 |
| 大对象 | 不涉及 | ValueRef 引用模式 | 仅后端关注 |
| 变量遮蔽 | 检测同名冲突 | AllowShadowing 允许遮蔽 | 后端允许，前端警告 |

---

> **文档维护**: 本补充文档与 `CODE_WIKI.md` 和 `CODE_WIKI_SUPPLEMENT.md` 配合使用。最后更新: 2026-05-13
