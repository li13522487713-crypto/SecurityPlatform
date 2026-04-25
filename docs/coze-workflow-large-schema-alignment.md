# Coze Workflow Large Schema Alignment

本文记录当前 Atlas 工作流前端、后端兼容层与本机 Coze Studio 源码 `D:\Code\coze-studio-main` 的 schema 对齐分析。目标不是重建一套新的 workflow schema，而是守住当前已经移植的 Coze/Flowgram canonical schema，补齐后端执行侧适配，确保复杂工作流、大型工作流、百节点工作流在保存、回显、试运行、执行链路上稳定。

## 1. 当前项目工作流 Schema 总览

当前前端工作流设计态直接使用 Coze Studio 同构的 Flowgram schema：

- 顶层 `WorkflowJSON` 只有 `nodes` 与 `edges`。
- 节点是 `WorkflowNodeJSON`，核心字段为 `id`、`type`、`meta`、`data`、`blocks`、`edges`。
- 保存时通过 `workflowDocument.toJSON()` 得到 `WorkflowJSON`，再 `JSON.stringify(json)` 提交给 `/api/workflow_api/save`。
- 回显时读取后端 `schema_json`，`JSON.parse` 后交给 `workflowDocument.fromJSON()`。
- Loop/Batch 等容器节点通过 `blocks` / `edges` 表示内部子图。

当前项目关键文件：

- `src/frontend/packages/workflow/base/src/types/node.ts`
- `src/frontend/packages/workflow/base/src/types/node-type.ts`
- `src/frontend/packages/workflow/nodes/src/workflow-document-with-format.ts`
- `src/frontend/packages/workflow/nodes/src/workflow-json-format.ts`
- `src/frontend/packages/workflow/playground/src/services/workflow-operation-service.ts`

关键类型：

```ts
export interface WorkflowNodeJSON<T = Record<string, unknown>> {
  id: string;
  type: StandardNodeType | FlowNodeBaseType | string;
  meta?: WorkflowNodeMeta;
  data: T;
  version?: string;
  blocks?: WorkflowNodeJSON[];
  edges?: WorkflowEdgeJSON[];
}

export interface WorkflowJSON {
  nodes: WorkflowNodeJSON[];
  edges: WorkflowEdgeJSON[];
}
```

## 2. Coze 原生工作流 Schema 总览

Coze Studio 前端源码中同样以 `WorkflowJSON` 为设计态 canonical schema。Coze Go 后端执行态不是直接执行前端 JSON，而是通过 `backend/domain/workflow/internal/canvas/adaptor/to_schema.go` 将前端 `vo.Canvas` 编译为 `schema.WorkflowSchema`。

Coze Go 核心流程：

1. JSON 反序列化为 `vo.Canvas`。
2. `CanvasToWorkflowSchema` 剪枝孤立节点。
3. 每个 `vo.Node` 进入 `NodeToNodeSchema`。
4. `entity.IDStrToNodeType(node.Type)` 解析节点类型。
5. 通过每类节点的 `Config.Adapt` 读取 `data.inputs` / `data.outputs`。
6. `normalizePorts` 规范 selector / branch 端口。
7. `schema.BuildBranches` 生成执行分支结构。

Coze Go 共享逻辑：

- `convert.SetInputsForNodeSchema`：将 `data.inputs.inputParameters` 映射为输入类型与输入来源。
- `convert.SetOutputTypesForNodeSchema`：将 `data.outputs` 映射为输出类型。
- `CanvasBlockInputToFieldInfo`：解析变量引用、块输出引用与全局变量引用。

## 3. 当前项目和 Coze 的 Schema 字段映射表

| 语义 | 当前项目设计态 | Coze 前端设计态 | Coze Go 执行态 | 当前项目后端执行态 |
|---|---|---|---|---|
| 顶层 workflow | `WorkflowJSON.nodes/edges` | 相同 | `vo.Canvas.nodes/edges` | `CanvasSchema.Nodes/Edges` |
| 节点 id | `node.id: string` | 相同 | `vo.Node.ID` | `NodeSchema.Key` |
| 节点类型 | `node.type: StandardNodeType` | 相同 | `entity.IDStrToNodeType` | `CozeNodeTypeMap` |
| 节点数据 | `node.data` | 相同 | `vo.Node.Data.Inputs/Outputs` | `NodeSchema.Config` |
| 输入参数 | `data.inputs.inputParameters` | 相同 | `Param[]` | `config` + input mappings |
| 输出参数 | `data.outputs` | 相同 | `SetOutputTypesForNodeSchema` | `config` + output schema |
| 连线 | `edges[].sourceNodeID/targetNodeID/sourcePortID/targetPortID` | 相同 | `EdgeToConnection` | `CanvasEdgeSchema` |
| 分支 | selector 动态 `sourcePortID` | 相同 | `normalizePorts` + `BuildBranches` | Selector executor |
| 容器子图 | `blocks` / `edges` 嵌套 | 相同 | container adaptor | Loop/Batch executor |
| 保存 payload | `{ workflow_id, schema: string }` | 相同 | `SaveWorkflowRequest.Schema` | raw `schema_json` longtext |
| 试运行 payload | `{ workflow_id, input }` | 相同 | `WorkFlowTestRunRequest` | 编译 draft schema 后执行 |

## 4. Workflow 顶层字段差异

当前项目与 Coze 前端顶层字段无结构差异，均为：

```json
{
  "nodes": [],
  "edges": []
}
```

差异主要发生在后端执行编译层：

- Coze Go 用 `NodeTypeMetas` 决定哪些 `node.type` 能进入执行态。
- 当前项目用 `CozeNodeTypeMap` 手动维护映射，存在漂移。
- 当前项目保存和读取 raw `schema_json` 基本无损；执行态会将 `node.data.inputs` 转成 Atlas `NodeSchema.Config`。

## 5. Node 顶层字段差异

设计态字段一致：

| 字段 | 设计态要求 | 风险 |
|---|---|---|
| `id` | string | 大整数 ID 不得 number 化 |
| `type` | string numeric id | 后端映射必须与前端/Coze Go 基准一致 |
| `meta.position` | 必须真实持久化 | 缺失会导致节点叠加 |
| `data.nodeMeta` | 表单与节点展示元信息 | 保存前会由 `WorkflowJSONFormat.formatNodeMeta` 刷新 |
| `data.inputs` | 节点入参和特有配置 | 后端 Adapter 覆盖不足会导致执行配置缺字段 |
| `data.outputs` | 节点输出 schema | 后端输出推导必须保留 |
| `blocks/edges` | 容器内部图 | Loop/Batch 大图容易错连 |

## 6. `node data` / `nodeData` / `data` / `setting` 字段差异

Coze canonical 字段是 `node.data`。常见结构：

```json
{
  "data": {
    "inputs": {
      "inputParameters": []
    },
    "nodeMeta": {
      "title": "",
      "description": "",
      "icon": "",
      "subTitle": "",
      "mainColor": ""
    },
    "outputs": []
  }
}
```

当前项目不应新增并行的 `nodeData` 或 `setting` 作为保存态字段。运行态 `setting` 只可作为 node debug 请求的一部分，不能污染设计态 schema。

## 7. Edge / Connection 字段差异

Coze/Flowgram 设计态 edge：

```ts
interface WorkflowEdgeJSON {
  sourceNodeID: string;
  targetNodeID: string;
  sourcePortID?: string | number;
  targetPortID?: string | number;
}
```

当前项目后端执行态会编译为 Atlas `CanvasEdgeSchema`。必须保证：

- 每条 edge 的 source/target 节点存在。
- 每条 edge 的 source/target port 存在。
- Selector/Condition edge 依赖 `sourcePortID`，不应另造 `branchId` 字段。
- Loop/Batch 的 body/done 端口必须保持 Coze 注册表定义。

## 8. Port / Branch 字段差异

Coze selector Go 逻辑：

- 前端端口：`true`、`false`、`true_1`、`true_2`。
- 编译时 `normalizePorts` 转成执行态：`branch_0`、`default`、`branch_N`。
- `BuildBranches` 再建立执行分支。

Loop 端口：

- `loop-output`
- `loop-output-to-function`

Batch 端口：

- `batch-output`
- `batch-output-to-function`

当前项目必须测试保存/回显后端口 ID 不变，且 edge 仍指向正确 port。

## 9. Variable / Input / Output Parameter 字段差异

Coze 引用表达式核心结构：

```ts
{
  "type": "ref",
  "content": {
    "source": "block-output",
    "blockID": "node_id",
    "name": "field.path"
  }
}
```

当前项目风险：

- 变量密集图中，每个节点重复扫描全图会带来性能问题。
- `VariableAssign` 的 `Left` 是写目标，不能按普通输入参数处理。
- `VariableMerge` 以 `data.inputs.mergeGroups` 为 canonical，旧 mock 中 `data.groups` 不应继续扩散。

## 10. Trial Run Payload 差异

Coze 前端 trial run 并不提交完整 schema，而是：

```json
{
  "workflow_id": "string",
  "space_id": "string",
  "input": {
    "key": "value"
  }
}
```

当前项目同样应走 `/api/workflow_api/test_run`，后端从 draft `schema_json` 读取 canonical schema 编译执行。禁止从 DOM 或表单临时状态另拼一套执行 schema。

## 11. Execute / Process Payload 差异

`get_process` 以 `workflow_id`、`execute_id`、`space_id` 查询执行状态。执行图来自已保存的 `schema_json`，而不是前端临时组装的另一份 payload。

当前项目后端执行链路：

1. `WorkFlowTestRun` 接收 runtime input。
2. 查询 draft schema。
3. `CozeWorkflowPlanCompiler.Compile` 编译 Coze schema。
4. `DagExecutor.RunAsync` 执行 Atlas runtime graph。

## 12. 保存 Payload 和回显 Payload 差异

保存：

```json
{
  "workflow_id": "string",
  "schema": "{\"nodes\":[],\"edges\":[]}",
  "space_id": "string",
  "submit_commit_id": "string"
}
```

回显：

```json
{
  "workflow": {
    "schema_json": "{\"nodes\":[],\"edges\":[]}"
  }
}
```

当前项目后端使用 longtext 原样保存 schema，设计态 roundtrip 字段丢失风险较低。更大的风险是执行编译阶段未读取节点特有字段。

## 13. 单节点 Schema 与全局 Workflow Schema 差异

单节点表单正确不代表全局 schema 正确。必须同时验证：

- 节点 `data.inputs` / `outputs` 完整。
- 全局 edge 指向存在节点与端口。
- 容器节点的 `blocks` / `edges` 没有和外部图混淆。
- 变量引用可通过图拓扑找到上游输出。
- 后端 Adapter 将节点 data 转成执行 Config 时不丢字段。

## 14. 上百节点大图风险点

| 风险 | 当前证据 | 处理轮次 |
|---|---|---|
| 保存时打印完整 JSON | `workflow-json-format.ts` 无条件 `console.log(json)` | 第 3 轮 |
| 多次全量深拷贝/JSON stringify | 保存/验证均 stringify 全图 | 第 3/18 轮 |
| 变量解析重复扫描全图 | 暂无统一索引 | 第 14 轮 |
| Adapter 覆盖不足 | `CozeNodeConfigAdapterRegistry` 仅 9 项 | 第 9-11 轮 |
| type map 漂移 | `'12'/'14'/'16'/'17'/'23'` | 第 7 轮 |
| 大整数 ID 精度 | 前端若 number 化会丢精度，后端 workflow_id 仍要求 long | 第 6/8/16 轮 |
| Port/Branch 错连 | condition/loop/batch 大图下风险高 | 第 15 轮 |

## 15. 当前已确认通过节点

已确认设计态接近 Coze canonical，且后端已有 Adapter 的节点：

| 节点 | type | 后端 Adapter |
|---|---:|---|
| Start / Entry | `1` | `EntryNodeConfigAdapter` |
| End / Exit | `2` | `ExitNodeConfigAdapter` |
| LLM | `3` | `LlmNodeConfigAdapter` |
| Code | `5` | `CodeRunnerNodeConfigAdapter` |
| Condition / If | `8` | `SelectorNodeConfigAdapter` |
| SubWorkflow | `9` | `CommonOutputsNodeConfigAdapter`（需升级） |
| Text | `15` | `TextProcessorNodeConfigAdapter` |
| Loop | `21` | `LoopNodeConfigAdapter` |
| HTTP | `45` | `HttpRequesterNodeConfigAdapter` |

## 16. 当前未对齐节点

以下节点需要后端 Adapter 或映射修复：

- Go 有基准，按 Coze Go 对齐：`4`、`6`、`12`、`13`、`18`、`19`、`20`、`22`、`27`、`28`、`29`、`30`、`32`、`37-40`、`42-44`、`46`、`51-59`。
- Go 无基准，本项目自研：`11`、`14`、`16`、`17`、`23`、`24`、`25`、`26`、`34`、`35`、`36`。

## 17. 高风险节点

| 节点 | 风险 |
|---|---|
| Database `12` | 当前项目映射为 Comment，但 Coze Go 是 `DatabaseCustomSQL` |
| Imageflow/ImageGenerate/ImageReference/ImageCanvas | 当前项目 type map 错位或缺失，Coze Go 无对应执行基准 |
| SubWorkflow | 仅 common outputs adapter，不足以承载 workflowId / inputDefs / batch |
| Batch | 端口、子图、并发、输出聚合复杂 |
| Condition | 动态分支端口、else 分支顺序与保存回显稳定性 |
| VariableAssign | Left/Right 语义不同，不能按普通输入处理 |
| Plugin/Database/Knowledge | 资源 ID 必须保留 string，显示名称不能替代真实 ID |

## 18. 需要优先修复的公共适配器

本项目不新增前端 `WorkflowSchemaBuilder/Hydrator`，复用 Coze `WorkflowDocumentWithFormat` / `WorkflowJSONFormat`。优先修复后端执行适配：

1. `CozeNodeTypeMap`
2. `CozeNodeConfigAdapterRegistry`
3. 通用 `inputParameters` / `outputs` 读取帮助函数
4. Database / Plugin / Knowledge / SubWorkflow 资源 ID 保真
5. Variable reference / Pair 解析
6. Selector / Loop / Batch port 编译校验

## 19. 复杂工作流验收用例矩阵

| 用例 | 规模 | 必须覆盖节点 | 验收点 |
|---|---|---|---|
| 基础长链工作流 | 30 节点 | Start、LLM、Code、HTTP、Message、End | 保存 payload、回显节点/边数量、试运行 payload 字段 |
| 百节点大图工作流 | 100 节点、120 边、10+ 类型 | 混合 P0/P1 | JSON 可序列化、无 undefined、ID string、hash 稳定、性能阈值 |
| 复杂条件分支 | 1 condition、5 分支、else | Condition + 多类型分支节点 | branch port 稳定、增删分支后 edge 正确、回显不乱序 |
| Loop 嵌套 | loop 内 Code/LLM/VariableAssign | Loop、Break、Continue、Code | body/done port、loop variable、内外连线不混淆 |
| Batch 批处理 | batch 内 HTTP/Code/VariableMerge | Batch、HTTP、Code、VariableMerge | input array、item/index variable、concurrency、输出聚合 |
| 子工作流 | 2 个 SubWorkflow | SubWorkflow | workflow_id string、大整数不丢精度、输入输出映射 |
| 外部资源混合 | plugin/database/knowledge/http/message | Plugin、Database、Dataset、HTTP、Message | 资源 ID string、名称回显、资源缺失不崩溃 |
| 变量密集 | 50+ 引用 | VariableMerge、VariableAssign、Code、Condition | 引用路径稳定、上游变更可感知、回显选择器正常 |

## 20. 分阶段实施计划

### 第 1 轮

- 目标：生成本文档初版。
- Coze 源码参考路径：`D:\Code\coze-studio-main\frontend\packages\workflow`、`D:\Code\coze-studio-main\backend\domain\workflow`。
- 当前项目路径：`src/frontend/packages/workflow`、`src/backend/Atlas.Infrastructure/Services/AiPlatform`。
- 发现的 schema 差异：前端设计态基本同构，后端执行 Adapter 覆盖不足，type map 漂移。
- 修改内容：新增 `docs/coze-workflow-large-schema-alignment.md`。
- 新增/更新测试：无。
- 验证结果：待后续轮次。
- 剩余风险：文档中的部分 Go 无基准自研节点仍需逐文件核对。

### 第 2 轮

- 目标：新增 Coze 源码 diff 守卫工具，生成 `docs/coze-source-drift-report.md`。
- Coze 源码参考路径：`D:\Code\coze-studio-main\frontend\packages\workflow`。
- 当前项目路径：`src/frontend/packages/workflow`。
- 发现的 schema 差异：待工具生成。
- 修改内容：`tools/coze-source-diff`。
- 新增/更新测试：工具 smoke test。
- 验证结果：待执行。
- 剩余风险：跨平台路径处理。

### 第 3 轮

- 目标：治理 `workflow-json-format.ts` 保存日志与 stable stringify。
- 参考路径：Coze 同名文件。
- 当前项目路径：`src/frontend/packages/workflow/nodes/src/workflow-json-format.ts`。
- 差异：当前无条件输出完整 JSON。
- 修改内容：开发环境保护日志；增加 stable stringify 工具。
- 测试：100 节点 stringify/hash 测试。
- 验证：待执行。
- 风险：改变 stringify 顺序可能影响快照。

### 第 4 轮

- 目标：生成 8 类复杂 workflow fixture。
- 当前项目路径：`src/frontend/packages/workflow/__fixtures__/workflow-large`。
- 测试：fixture schema invariants。
- 风险：部分资源节点需要真实字段结构。

### 第 5 轮

- 目标：roundtrip 测试 build → normalize → hydrate → rebuild → compare。
- 当前项目路径：`src/frontend/packages/workflow/__tests__`。
- 风险：测试环境需能实例化 WorkflowDocument。

### 第 6 轮

- 目标：schema invariants validator。
- 当前项目路径：`src/frontend/packages/workflow/base/src/utils/schema-invariants.ts`。
- 风险：校验过严可能拦截历史草稿。

### 第 7 轮

- 目标：修 `CozeNodeTypeMap` 漂移。
- 当前项目路径：`src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeWorkflowPlanCompiler.cs`。
- 差异：`12`、`14`、`16`、`17`、`23`。
- 测试：`CozeNodeTypeMapDriftTests`。
- 风险：旧测试可能依赖错误映射。

### 第 8 轮

- 目标：后端 workflow ID 容错与大整数边界。
- 当前项目路径：`AppWebWorkflowGatewayController.cs`。
- 风险：当前存储为 long，不能接受任意非数字 Coze ID。

### 第 9 轮

- 目标：补 P0 后端 Adapter：Plugin、Dataset、SubWorkflow、Output、Question、Batch、Input、Intent。
- Go 源码参考：`plugin`、`knowledge`、`subworkflow`、`emitter`、`qa`、`batch`、`receiver`、`intentdetector`。
- 测试：每个 adapter 至少一个 compile 单测。

### 第 10 轮

- 目标：补 Variable/Database 系列 Adapter。
- Go 源码参考：`variableassigner`、`variableaggregator`、`database`。
- 测试：缺必填 / 大整数 ID / 正常 compile。

### 第 11 轮

- 目标：补 Chat/Trigger/Image/Json/其他 Adapter。
- Go 源码参考：`conversation`、`json`；Go 无基准节点按当前项目前端 form-meta 与 executor 读 config 的并集实现。
- 测试：覆盖 49/49 adapter registry。

### 第 12 轮

- 目标：后端执行器错配/缺失核对。
- 当前项目路径：`NodeExecutorRegistry.cs`。
- 测试：registry coverage。

### 第 13 轮

- 目标：后端保存 → 读取 → compile 字节对等与集成测试。
- 测试：`CozeWorkflowCompatIntegrationTests` 扩展。

### 第 14 轮

- 目标：变量引用索引与跨作用域规则。
- 当前项目路径：`src/frontend/packages/workflow/variable`。
- 测试：`variable-heavy.json`。

### 第 15 轮

- 目标：Condition/Loop/Batch port 和 edge 一致性断言。
- 测试：condition/loop/batch 三个 fixture。

### 第 16 轮

- 目标：SubWorkflow 大整数 ID 与外部资源节点混合 workflow。
- 测试：`sub-workflow-big-id.json`、`external-resources-mixed.json`。

### 第 17 轮

- 目标：Playwright E2E 30 节点建图、保存、刷新、试运行。
- 当前项目路径：`src/frontend/e2e/app`。
- 风险：依赖可运行后端与测试账号。

### 第 18 轮

- 目标：百节点性能回归。
- 测试：100 节点 build/save/hydrate/compile 阈值。

### 第 19 轮

- 目标：Coze 源码漂移守卫接入 CI。
- 风险：需要维护 allowlist。

### 第 20 轮

- 目标：最终报告回填本文档。
- 内容：49 节点状态、fixture 通过情况、测试结果、剩余风险与 backlog。

## 21. 本轮最终修复报告

本轮按 20 个里程碑完成了复杂 workflow schema 对齐的第一阶段闭环。核心结论保持不变：前端设计态已是 Coze/Flowgram canonical `WorkflowJSON`，本轮主要修复后端执行编译适配、增加大图测试资产，并建立 drift guard。

### 21.1 已完成变更

| 类别 | 产出 |
|---|---|
| 文档 | 新增本文档；新增 `docs/coze-source-drift-report.md` |
| Drift guard | 新增 `tools/coze-source-diff/index.mjs`、`allowlist.json`；新增 `pnpm run coze:diff` |
| 前端稳定性 | `workflow-json-format.ts` 保存日志改为 `IS_DEV_MODE` 下 `console.debug` |
| Stable hash | 新增 `stableStringifyWorkflowSchema` |
| Fixture | 新增 8 个复杂 workflow fixture + 生成器 |
| Roundtrip | 新增 8 fixture roundtrip/hash 单测 |
| Invariants | 新增 `validateWorkflowSchemaInvariants`，覆盖 undefined、非 JSON 对象、循环引用、edge、Condition/Loop/Batch port |
| 变量引用 | 新增 `buildVariableReferenceIndex` |
| 后端 type map | 修复 `12/14/16/17/23` 语义漂移 |
| 后端 ID | 新增 `TryParsePositiveLongId`，gateway ID 解析拒绝非正整数和越界 ID |
| 后端 Adapter | `CozeNodeConfigAdapterRegistry` 从 9 类扩展到覆盖当前所有可执行/透传节点 |
| E2E | 新增 `workflow-large-schema.spec.ts`，以 30 节点 fixture 验证保存、刷新回显、test_run |

### 21.2 49 节点对齐状态

| 状态 | 节点 |
|---|---|
| 已有精细 Adapter | `1` Start、`2` End、`3` LLM、`5` Code、`8` If、`15` Text、`21` Loop、`45` Http |
| 本轮新增精细 Adapter | `4` Plugin、`6` Dataset、`9` SubWorkflow、`12` DatabaseCustomSQL、`13` Output、`18` Question、`20` SetVariable、`22` Intent、`28` Batch、`30` Input、`32` VariableMerge、`40` VariableAssign、`42-44/46` Database CRUD |
| 本轮新增透传 Adapter | `11` Variable、`14/16/17/23` Image 系列、`19/29` Break/Continue、`24/25` Scene、`26` LTM、`27` DatasetWrite、`34-36` Trigger、`37-39/51-57` Chat/Message、`58/59` JSON |
| 非运行态 | `31` Comment 仍作为画布注释/跳过节点 |

### 21.3 Fixture 与测试结果

| 测试 | 结果 |
|---|---|
| `pnpm exec vitest run stable-json / roundtrip / invariants` | 通过，23 个用例 |
| `pnpm exec vitest run variable-reference-index.spec.ts` | 通过，2 个用例 |
| `pnpm exec vitest run schema-invariants.spec.ts` | 通过，13 个用例 |
| `pnpm exec vitest run workflow-schema-roundtrip.spec.ts` | 通过，13 个用例 |
| `dotnet test ... CozeWorkflowPlanCompilerTests` | 通过，22 个用例 |
| `dotnet test ... NodeExecutorRegistryCoverageTests / NodeTypeMappingCoverageTests / CozeWorkflowPlanCompilerTests` | 通过，57 个用例 |
| `dotnet test ... CozeWorkflowGatewayIdParsingTests` | 通过，8 个用例 |
| `dotnet test ... CozeWorkflowLargeFixtureIntegrationTests` | 通过，8 个 fixture |
| `pnpm run coze:diff` | 通过并生成报告；当前 unallowed drift count = 27 |

### 21.4 当前剩余风险

1. `CozeNodeConfigAdapterRegistry` 已做到字段不丢，但部分节点仍是透传 Adapter；这些节点的执行器是否真正消费对应字段，需要继续逐节点做行为测试。
2. Coze Go 没有 `11/14/16/17/23/26/34-36` 的运行时基准，本项目只能按前端 form-meta 与现有 executor 读 config 的并集维护。
3. `workflow_id` 在本项目后端仍受 long 存储约束；任意非数字 Coze string ID 不支持，本轮只做到明确错误而不是任意字符串存储。
4. Playwright E2E 已新增但未在本轮启动完整浏览器/后端环境运行。
5. Drift guard 当前未白名单漂移为 27，后续要逐个分类：上游偏离修复、Atlas 定制白名单、或回退。

### 21.5 下一步 Backlog

- 将 `COZE_SOURCE_DIFF_STRICT=1 pnpm run coze:diff` 接入实际 CI pipeline，并补齐 allowlist 理由。
- 为透传 Adapter 节点逐个补 executor 行为测试，尤其 Chat/Message、Trigger、Image、LTM。
- 将 E2E spec 纳入稳定端到端流水线，记录 trace 和 payload。
- 为后端 `Fail` 响应新增更强类型的错误 DTO，逐步替代匿名对象。
- 若未来确实要支持任意 Coze string workflow id，需要迁移 domain/entity 的 workflow id 存储类型，不能只改 controller。

