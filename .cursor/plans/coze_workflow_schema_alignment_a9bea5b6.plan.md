---
name: Coze workflow schema alignment
overview: 把前端 workflow 栈（已是 Coze Studio 同构）对 Coze 源码做一致性守卫与漂移修复；按 Coze Go `backend/domain/workflow/internal/nodes/**` 的 `Config.Adapt` 逐个对齐后端 CozeNodeConfigAdapter（Coze Go 有 28 个可参照，12 个本项目自研按 FE form-meta 建）；修 `CozeNodeTypeMap` 5 处漂移（含 '12'→DatabaseCustomSQL）；建百节点 fixture + roundtrip + Playwright E2E。
todos:
  - id: r1-doc
    content: 第 1 轮：落地 docs/coze-workflow-large-schema-alignment.md 初版（49 节点矩阵 + 8 类 fixture 矩阵 + 20 轮路线）
    status: completed
  - id: r2-diff-tool
    content: 第 2 轮：tools/coze-source-diff 生成 docs/coze-source-drift-report.md
    status: completed
  - id: r3-stringify
    content: 第 3 轮：前端 workflow-json-format.ts console.log 治理 + stable stringify
    status: completed
  - id: r4-fixtures
    content: 第 4 轮：8 类复杂工作流 fixture 生成器 + 种子 JSON
    status: completed
  - id: r5-roundtrip
    content: 第 5 轮：前端 schema roundtrip vitest 套件
    status: completed
  - id: r6-invariants
    content: 第 6 轮：前端 schema invariants validator + 单测
    status: completed
  - id: r7-typemap
    content: 第 7 轮：后端 CozeNodeTypeMap 修 '14'/'16'/'17'/'23' 漂移 + 回归测试
    status: completed
  - id: r8-id
    content: 第 8 轮：后端 long.TryParse 容错 + 大整数 ID 边界测试
    status: completed
  - id: r9-adapter-p0
    content: 第 9 轮：后端 Adapter 批量 P0（Plugin/Dataset/SubWorkflow/Output/Question/Batch/Input/Intent）
    status: completed
  - id: r10-adapter-db
    content: 第 10 轮：后端 Adapter 批量 Database/Variable 系列
    status: completed
  - id: r11-adapter-chat
    content: 第 11 轮：后端 Adapter 批量 Chat/Trigger/Image/Json/其他
    status: completed
  - id: r12-exec-fix
    content: 第 12 轮：后端执行器错配/缺失核对
    status: completed
  - id: r13-integration
    content: 第 13 轮：后端保存→compile 集成测试 + SHA256 字节对等
    status: completed
  - id: r14-variables
    content: 第 14 轮：前端变量引用索引与跨作用域规则
    status: completed
  - id: r15-ports
    content: 第 15 轮：Condition/Loop/Batch 端口与边一致性断言
    status: completed
  - id: r16-subwf-resource
    content: 第 16 轮：SubWorkflow 大整数 ID + 资源混合节点 roundtrip
    status: completed
  - id: r17-e2e
    content: 第 17 轮：Playwright E2E 30 节点建图→保存→刷新→试运行
    status: completed
  - id: r18-perf
    content: 第 18 轮：百节点大图性能回归 + CI 基线
    status: completed
  - id: r19-ci-guard
    content: 第 19 轮：Coze 源码漂移守卫接入 CI
    status: completed
  - id: r20-final-report
    content: 第 20 轮：最终报告回填 alignment md + backlog
    status: completed
isProject: false
---

## 结论先行（调研纠正 3 个关键假设）

1. **节点是 49 个（`StandardNodeType`），不是 33 个**；`NODES_V2` 已注册 48 个，1 个在枚举无 playground registry（`JsonParser '59'` 仅枚举，`'23' ImageCanvas` compile 映射缺失）。
2. **前端已经是 Coze Studio 同构**：`@flowgram.ai/free-layout-core` + `WorkflowJSON` + `WorkflowDocumentWithFormat` + `WorkflowJSONFormat` + 每节点 `data-transformer`/`form-meta`，从 [D:\Code\coze-studio-main](D:\Code\coze-studio-main) 移植。**没有"前端内部 schema vs 提交后端 schema"分裂**。保存 payload 就是 `JSON.stringify(WorkflowJSON)` 走 `/api/workflow_api/save`；trial run 就是 `{ workflow_id, input: Record<string,string> }`。和 Coze 完全对齐。
3. **真正风险在后端**：`CozeNodeConfigAdapterRegistry` 只覆盖 **9/49**（Entry、Exit、Llm、Code、Selector、SubWorkflow、Text、Loop、Http），其余 40 类走默认映射 → 节点在前端能保存、但执行时 `BuildNodeConfig` 读不到专属 `data` 字段，`NodeExecutor` 运行拿到的 Config 不完整。此外 `CozeNodeTypeMap` 有 **3 个语义漂移**（`'14' Imageflow`→`ImageGenerate`、`'16' ImageGenerate`→`ImageReference`、`'17' ImageReference`→`ImageCanvas`），`'23'` 未映射，本项目 `'12' Database`/`'31' Comment` 退化为 Comment（**Coze 自己 `'12'` 是 `DatabaseCustomSQL`，不是 Comment——这是本项目的漂移**）。

所以路线 A 的本质被改写为"**Coze Studio 源码一致性守卫 + 后端 Adapter 补齐 + 大图测试基建**"，不再新建 `WorkflowSchemaBuilder/Hydrator/Normalizer/Hasher/...` 这套与 Coze 同名功能重复的抽象。

## Coze Go 源码基准（3 个风险点的真正对齐目标）

下面所有轮次里涉及后端修复的 Go 基准必须来自这些位置，不能自行发明。

### Coze Go 节点裁决矩阵（来自 [node_meta.go](D:\Code\coze-studio-main\backend\domain\workflow\entity\node_meta.go) `NodeTypeMetas` + [to_schema.go](D:\Code\coze-studio-main\backend\domain\workflow\internal\canvas\adaptor\to_schema.go) `RegisterAllNodeAdaptors`）

| FE `StandardNodeType` | Coze Go `NodeTypeMetas` 有? | Coze Go `Adapt` 实现 | 本项目应对策略 |
|---|---|---|---|
| '1' Start | 是 | `NodeTypeEntry` | 已有 adapter，保持 |
| '2' End | 是 | `NodeTypeExit` | 已有 adapter，保持 |
| '3' LLM | 是 | `NodeTypeLLM` | 已有 adapter，保持 |
| '4' Api/Plugin | 是 | `plugin.Config.Adapt` | **第 9 轮按 Go 补 adapter** |
| '5' Code | 是 | `NodeTypeCodeRunner` | 已有 |
| '6' Dataset | 是 | `knowledge.RetrieveConfig.Adapt` | **第 9 轮按 Go 补** |
| '8' Selector | 是 | `selector.Config.Adapt` | 已有 |
| '9' SubWorkflow | 是 | `subworkflow.Config.Adapt` | 已有（需升级替换 `CommonOutputsNodeConfigAdapter`） |
| **'11' Variable** | **否** | — | **Coze Go 也没有**；本项目有执行器，adapter 按本项目前端 form-meta 定义 |
| **'12' Database** | **是 → `DatabaseCustomSQL`** | `customsql.Adapt` | **本项目漂移**：现在映射成 Comment 是错的；第 7 轮恢复为 DB SQL |
| '13' Output | 是 | `emitter.Config.Adapt` | **第 9 轮按 Go 补** |
| **'14' Imageflow** | **否**（仅 IDL `NodeTemplateType=14` 注释说是 Imageflow 专用副本） | — | **Coze Go 也没有**；本项目自有 `ImageGenerateUpstreamNodeExecutor`，按本项目语义修漂移 |
| '15' Text | 是 | `textprocessor.Config.Adapt` | 已有 |
| **'16' ImageGenerate** | **否** | — | 本项目自研；修漂移 |
| **'17' ImageReference** | **否** | — | 本项目自研；修漂移 |
| '18' Question | 是 | `qa.Config.Adapt` | **第 9 轮按 Go 补** |
| '19' Break | 是 | `_break.Config.Adapt` | **第 11 轮按 Go 补** |
| '20' SetVariable | 是 | `variableassigner.InLoopConfig.Adapt` | **第 10 轮按 Go 补** |
| '21' Loop | 是 | `loop.Config.Adapt` | 已有 |
| '22' Intent | 是 | `intentdetector.Config.Adapt` | **第 9 轮按 Go 补** |
| **'23' ImageCanvas** | **否** | — | 本项目自研；第 7 轮补映射 |
| '24' SceneVariable / '25' SceneChat | （未确认） | （未确认） | 第 11 轮核对再定 |
| **'26' LTM** | **否** | — | 本项目自研 |
| '27' DatasetWrite | 是 | `knowledge.IndexerConfig.Adapt` | **第 11 轮按 Go 补** |
| '28' Batch | 是 | `batch.Config.Adapt` | **第 9 轮按 Go 补** |
| '29' Continue | 是 | `_continue.Config.Adapt` | **第 11 轮按 Go 补** |
| '30' Input | 是 | `receiver.Config.Adapt` | **第 9 轮按 Go 补** |
| '31' Comment | 是（`blockTypeToSkip`） | 编译期剥离 | 本项目 DagExecutor 已 bypass，对齐 |
| '32' VariableMerge | 是 | `variableaggregator.Config.Adapt` | **第 10 轮按 Go 补** |
| **'34'/'35'/'36' Triggers** | **否** | — | 本项目自研（Trigger*NodeExecutor 已存在） |
| '37-39','51-57' Chat/Message | 是（conversation/message 包） | 各自 `Config.Adapt` | **第 11 轮按 Go 补** |
| '40' VariableAssign | 是 | `variableassigner.Config.Adapt`（Pair 结构） | **第 10 轮按 Go 补** |
| '42-44','46' Database CRUD | 是 | `database/*.Adapt`（`DatabaseNode` 结构） | **第 10 轮按 Go 补** |
| '45' Http | 是 | `httprequester.Config.Adapt` | 已有 |
| '58' JsonStringify | 是 | `json.SerializationConfig.Adapt` | **第 11 轮按 Go 补** |
| '59' JsonParser | 是 | `json.DeserializationConfig.Adapt` | **第 11 轮按 Go 补** |

**关键结论**：不是"按 Coze 补 40 个"，而是"**Coze Go 有 ~28 个可参照，~12 个是本项目自研（Coze Go 也没落地）**"。后者 adapter 按本项目前端 `data-transformer.ts`/`form-meta.tsx` 声明字段自建，不需要也不可能参照 Go。

### Coze Go 共享机制（第 9/10/11 轮所有 adapter 必须遵循的三个函数）

1. **`convert.SetInputsForNodeSchema`** —— 通用 `data.inputs.inputParameters` → `NodeSchema.InputTypes` + `InputSources`：

```496:523:D:\Code\coze-studio-main\backend\domain\workflow\internal\canvas\convert\type_convert.go
func SetInputsForNodeSchema(n *vo.Node, ns *schema.NodeSchema) error {
    if n.Data.Inputs == nil { return nil }
    inputParams := n.Data.Inputs.InputParameters
    for _, param := range inputParams {
        tInfo, _ := CanvasBlockInputToTypeInfo(param.Input)
        ns.SetInputType(param.Name, tInfo)
        sources, _ := CanvasBlockInputToFieldInfo(param.Input, FieldPath{param.Name}, n.Parent())
        ns.AddInputSource(sources...)
    }
    return nil
}
```

2. **`convert.SetOutputTypesForNodeSchema`** —— `data.outputs` → 输出类型登记。

3. **`CanvasBlockInputToFieldInfo`** —— 解析块输出引用（`source:'block-output'` / `'variable'` / `'global_variable_*'`），这是变量引用跨节点解析的**唯一**正确方式，后端 adapter 不得自己拼路径。

本项目要补的 40 个 Adapter 里，**只有节点特有字段走节点专属逻辑，通用 `inputParameters`/`outputs` 一律通过"项目版等价函数"走**（本项目对应 [CozeWorkflowPlanCompiler.cs BuildNodeConfig](src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeWorkflowPlanCompiler.cs) 已有类似逻辑，第 9 轮前先核对一遍是否等价）。

## 重要参考坐标

- 前端 canonical：[frontend/packages/workflow/base/src/types/node.ts](D:\Code\coze-studio-main\frontend\packages\workflow\base\src\types\node.ts) L25-L38 `WorkflowJSON` / `WorkflowNodeJSON`
- 前端节点注册：[src/frontend/packages/workflow/playground/src/nodes-v2/constants.ts](src/frontend/packages/workflow/playground/src/nodes-v2/constants.ts) `NODES_V2`
- 前端保存：[src/frontend/packages/workflow/playground/src/services/workflow-operation-service.ts](src/frontend/packages/workflow/playground/src/services/workflow-operation-service.ts) L143-L161
- 前端 format：[src/frontend/packages/workflow/nodes/src/workflow-json-format.ts](src/frontend/packages/workflow/nodes/src/workflow-json-format.ts) L217-L332（含 **L331 的 `console.log` 百节点卡主线程**）
- 前端 flatten/nest：[src/frontend/packages/workflow/nodes/src/workflow-document-with-format.ts](src/frontend/packages/workflow/nodes/src/workflow-document-with-format.ts) L111-L181
- 后端 gateway：[src/backend/Atlas.AppHost/Controllers/AppWebWorkflowGatewayController.cs](src/backend/Atlas.AppHost/Controllers/AppWebWorkflowGatewayController.cs) 双路由 `api/workflow_api` + `api/app-web/workflow-sdk`
- 后端 compile：[src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeWorkflowPlanCompiler.cs](src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeWorkflowPlanCompiler.cs) L17-L86 `CozeNodeTypeMap`
- 后端 adapter：[src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeNodeConfigAdapters.cs](src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeNodeConfigAdapters.cs) L57-L79 `AdapterMap`（**9 项**）
- 后端执行：[src/backend/Atlas.Infrastructure/Services/WorkflowEngine/NodeExecutorRegistry.cs](src/backend/Atlas.Infrastructure/Services/WorkflowEngine/NodeExecutorRegistry.cs) + [DagExecutor.cs](src/backend/Atlas.Infrastructure/Services/WorkflowEngine/DagExecutor.cs)
- 后端存储：[src/backend/Atlas.Domain/AiPlatform/Entities/CozeWorkflowDraft.cs](src/backend/Atlas.Domain/AiPlatform/Entities/CozeWorkflowDraft.cs) `schema_json longtext`
- ID 约束：`long.TryParse(workflow_id)` [AppWebWorkflowGatewayController.cs L1659-L1663](src/backend/Atlas.AppHost/Controllers/AppWebWorkflowGatewayController.cs) —— 非数字字符串 400

## 交付物概览

1. `docs/coze-workflow-large-schema-alignment.md`（分析文档 + 验收用例矩阵 + 对齐状态表）
2. `docs/coze-source-drift-report.md`（自动生成的漂移报告）
3. `tools/coze-source-diff/`（前端对 Coze 源码的 diff 守卫脚本 + CI hook）
4. `src/frontend/packages/workflow/__fixtures__/workflow-large/*.json`（8 类复杂 fixture）
5. 前端 roundtrip 测试套件（vitest）
6. 后端 40 个新增 `CozeNodeConfigAdapter` + 对应 xUnit 测试
7. 后端修复：`CozeNodeTypeMap` 3 处语义漂移 + `'23'` 缺失映射
8. 前端修复：`workflow-json-format.ts` L331 `console.log` 保护、stable stringify、ID string 守卫
9. Playwright E2E：30 节点建图→保存→刷新→回显→试运行
10. 后端 `CozeWorkflowCompatIntegrationTests` 扩展（大图 roundtrip、ID 精度、资源节点）

## 20 轮执行（按你的格式）

### 第 1 轮：分析文档与漂移报告初版
- 目标：落地 `docs/coze-workflow-large-schema-alignment.md`，含本计划"结论先行"的所有事实、49 节点对齐矩阵、8 类验收用例矩阵、20 轮路线图。
- 参考：上述坐标集。
- 修改：新建 `docs/coze-workflow-large-schema-alignment.md`。
- 测试：无。
- 风险：文档需后续轮次回填实测结果。

### 第 2 轮：Coze 源码 diff 守卫工具
- 目标：`tools/coze-source-diff/` Node 脚本，接受 `D:\Code\coze-studio-main` 路径，对比 `packages/workflow/{base,nodes,variable,playground/src/{node-registries,nodes-v2}}` 下同名文件，生成 `docs/coze-source-drift-report.md`（漂移文件清单、每文件 unified diff 摘要）。
- 输出：漂移文件 top-N 清单作为后续轮次修复输入。

### 第 3 轮：前端 workflow stable stringify + 保存日志治理
- 目标：修 [workflow-json-format.ts L331](src/frontend/packages/workflow/nodes/src/workflow-json-format.ts) 无条件 `console.log`（改 `IS_DEV && ...`），引入 `fast-json-stable-stringify`，为 save/roundtrip/hash 统一 stringify 顺序。
- 测试：百节点 fixture benchmark 显示保存主线程阻塞显著下降。

### 第 4 轮：8 类复杂工作流 fixture 生成器
- 目标：`src/frontend/packages/workflow/__fixtures__/workflow-large/` 下程序化生成：
  - `long-chain-30.json`、`large-graph-100.json`、`condition-branches.json`、`loop-nested.json`、`batch-processing.json`、`sub-workflow-big-id.json`、`external-resources-mixed.json`、`variable-heavy.json`
- 生成器 `fixtures/generate.ts` 使用真实 `StandardNodeType` 值 + Coze `WorkflowJSON` 形状，不编造节点类型。
- `sub-workflow-big-id.json` 故意使用接近 `long.MaxValue` 的字符串 ID 验证精度。

### 第 5 轮：前端 roundtrip 测试套件
- 目标：`packages/workflow/__tests__/schema-roundtrip.spec.ts`：
  1. 读 fixture → `workflowDocument.fromJSON`
  2. `workflowDocument.toJSON`（= `formatOnSubmit`）
  3. `JSON.stringify(stable)` → hashA
  4. 再 `fromJSON` → `toJSON` → hashB
  5. 比较节点/边/端口/变量引用数量 + hash
- 8 个 fixture 全量跑，100 节点需在合理时间内完成（设阈值）。

### 第 6 轮：前端 schema invariants validator
- 目标：`packages/workflow/base/src/utils/schema-invariants.ts` + 单测：
  - 无 `undefined` / `Function` / `Map` / `Set` / `Date`
  - 所有 `id` 是 string
  - 每条 edge 的 `sourceNodeID`/`targetNodeID` 存在
  - Condition 动态分支 port 一致、Loop/Batch `loop-output`/`loop-output-to-function` 成对
  - 子工作流 `workflowId` 为非空 string，且 `/^\d+$/` 时不超过 `Number.MAX_SAFE_INTEGER`
- 作为 roundtrip 测试的前置断言。

### 第 7 轮：后端 CozeNodeTypeMap 漂移修复（5 处）
- **Go 基准**：[node_meta.go](D:\Code\coze-studio-main\backend\domain\workflow\entity\node_meta.go) `NodeTypeMetas` + `IDStrToNodeType`：
  ```35:45:D:\Code\coze-studio-main\backend\domain\workflow\entity\node_meta.go
  func IDStrToNodeType(s string) NodeType {
      id, _ := strconv.ParseInt(s, 10, 64)
      for _, m := range NodeTypeMetas {
          if m.ID == id { return m.Key }
      }
      return ""
  }
  ```
  Coze `NodeTypeMetas` 里 `ID: 12 → NodeTypeDatabaseCustomSQL`（`DisplayKey:"Database", Name:"SQL自定义"`），**没有** ID `14/16/17/23` 条目。
- 修 [CozeWorkflowPlanCompiler.cs L17-L86 `CozeNodeTypeMap`](src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeWorkflowPlanCompiler.cs)：
  1. **`'12'`**：从 `Comment` 改为 `WorkflowNodeType.DatabaseCustomSql`（本项目已有 `DatabaseNl2SqlNodeExecutor`/`DatabaseCustomSql` 枚举，对齐 Go `DatabaseCustomSQL`）。注释里明确"本项目 42/43/44/46 是细粒度 CRUD，12 保留为 SQL 自定义，与 Coze 一致"。
  2. **`'14' Imageflow`**：当前错映射到 `ImageGenerate`；改为映射到 `WorkflowNodeType.Imageflow`（本项目内部枚举值 67，有 `ImageGenerateUpstreamNodeExecutor` 之外的专属处理时再接，当前阶段先保证映射名字和 FE 枚举一致，避免压缩成另一种节点）。**Coze Go `NodeTypeMetas` 不包含 14**，这是本项目扩展，映射按 FE 语义不按 Go。
  3. **`'16' ImageGenerate`**：从错位的 `ImageReference` 改为 `ImageGenerate`。
  4. **`'17' ImageReference`**：从错位的 `ImageCanvas` 改为 `ImageReference`。
  5. **`'23' ImageCanvas`**：新增映射到 `WorkflowNodeType.Canvas`/`ImageCanvas`（本项目 `Canvas`=69）。**Coze Go `NodeTypeMetas` 不包含 23**，属本项目扩展；映射理由记录在代码注释。
- 同步修 [CozeCompatGatewaySupport.ToCozeNodeTypeCode](src/backend/Atlas.Presentation.Shared/Workflows/CozeCompatGatewaySupport.cs)（反向映射）。
- 回归测试 `tests/Atlas.SecurityPlatform.Tests/Workflows/CozeNodeTypeMapDriftTests.cs`：
  - 对 Coze Go `NodeTypeMetas` 有条目的 id（1-10,13,15,18-22,25,27-33,37-59 去掉 11/14/16/17/23/26/34-36）—— 断言本项目映射**与 Go 一致**。
  - 对本项目扩展 id（11/14/16/17/23/26/34-36）—— 断言映射**在本项目枚举内语义正确且不压缩为其他节点类型**。
- `CozeWorkflowPlanCompilerTests` 新增 type `'12' DatabaseCustomSQL` 编译测试（参照 Go `customsql.Adapt` 读 `DatabaseInfoList` + `sql` 字段）。

### 第 8 轮：后端 long.TryParse 容错与大整数 ID 测试
- 目标：保持 `long` 存储（本项目约束），但在 gateway 层：
  - 非数字 ID 返回明确 4xx + `INVALID_WORKFLOW_ID_FORMAT`，不再 500；
  - 增加 JS `Number.MAX_SAFE_INTEGER` 边界测试。
- 文档记录约束：项目 ID 使用 snowflake long，不使用任意字符串。

### 第 9 轮：后端 Adapter 批量补齐（第 1 批 P0，全部有 Go 基准）
按 Coze Go `Adapt` 逐个对齐。每个 Adapter 的 Config 字段集合**必须覆盖 Go 版读到的字段**，不得少。

#### 9.1 `'4' Plugin` —— 对齐 Go `plugin.Config.Adapt`
- Go 源：
  ```47:88:D:\Code\coze-studio-main\backend\domain\workflow\internal\nodes\plugin\plugin.go
  apiParams := slices.ToMap(inputs.APIParams, ...)
  ps, _ := apiParams["pluginID"]  // 还读 apiID、pluginVersion、apiName、tool_name 等
  c.PluginFrom = inputs.PluginFrom
  // 之后走 SetInputsForNodeSchema + SetOutputTypesForNodeSchema
  ```
- 新 `PluginNodeConfigAdapter`：从 `data.inputs.apiParam[]` 映射 `plugin_id`、`api_id`、`plugin_version`、`api_name`、`tool_name`、`plugin_from`、`batch`；`inputParameters`/`outputs` 走通用路径。
- 测试：fixture `external-resources-mixed.json` 的 plugin 节点 compile 后 Config 含以上 7 字段，且 `plugin_id`/`api_id` 类型为 string。

#### 9.2 `'6' Dataset` (Knowledge Retrieve) —— 对齐 `knowledge.RetrieveConfig.Adapt`
- Go 源：
  ```55:88:D:\Code\coze-studio-main\backend\domain\workflow\internal\nodes\knowledge\knowledge_retrieve.go
  datasetListInfoParam := inputs.DatasetParam[0]
  datasetIDs := datasetListInfoParam.Input.Value.Content.([]any)
  // topK / useRerank / useRewrite / useNl2sql / minScore / strategy 等都从 DatasetParam 按 name 取
  r.ChatHistorySetting = inputs.ChatHistorySetting
  ```
- 新 `KnowledgeRetrieveNodeConfigAdapter`：`data.inputs.datasetParam[]` 按 name 解出 `datasetList`（string[]）、`topK`、`minScore`、`useRerank`、`useRewrite`、`useNl2sql`、`strategy`，外加 `chatHistorySetting`。

#### 9.3 `'9' SubWorkflow` —— 对齐 `subworkflow.Config.Adapt`，替换 `CommonOutputsNodeConfigAdapter`
- 现状：本项目用 `CommonOutputsNodeConfigAdapter(SubWorkflow)`，只转发 outputs。
- Go 源 `subworkflow` 还读 `workflowId`（大整数 string）、`workflowVersion`、`inputDefs[]`、`inputParameters[]`、`batch` 子结构。
- 新 `SubWorkflowNodeConfigAdapter`：显式读这些字段；**`workflow_id`/`workflow_version` 保持 string**，不做 `long.TryParse`（Coze 允许 Snowflake 大整数 string，本项目 subworkflow 调用点单独处理）。

#### 9.4 `'13' Output` —— 对齐 `emitter.Config.Adapt`
- Go 源：
  ```51:79:D:\Code\coze-studio-main\backend\domain\workflow\internal\nodes\emitter\emitter.go
  content := n.Data.Inputs.Content
  streamingOutput := n.Data.Inputs.StreamingOutput
  ns.StreamConfigs = &schema2.StreamConfig{ RequireStreamingInput: streamingOutput }
  SetInputsForNodeSchema(n, ns)
  ```
- 新 `OutputEmitterNodeConfigAdapter`：读 `data.inputs.content`（模板 string）、`streamingOutput`（bool）、`terminatePlan`（若存在，与 End 节点的 `UseAnswerContent` 语义打通）。

#### 9.5 `'18' Question` —— 对齐 `qa.Config.Adapt`
- Go 读 `data.inputs.llmParam`、`answer_type`（`option` / `text`）、`options[]`、`limit`。本项目前端 types 已有这些字段（[question/data-transformer.ts L35-L46](src/frontend/packages/workflow/playground/src/node-registries/question/data-transformer.ts)）。
- 新 `QuestionAnswerNodeConfigAdapter`：映射完整。

#### 9.6 `'22' Intent` —— 对齐 `intentdetector.Config.Adapt`
- Go 读 `intents[]`（每个含 `name`/`description`）、`llmParam`、`chatHistorySetting`。
- 新 `IntentDetectorNodeConfigAdapter`。

#### 9.7 `'28' Batch` —— 对齐 `batch.Config.Adapt`
- Go 读 `data.inputs.batch`（`inputArray`/`concurrency`/`batchSize`）、`inputParameters`。
- 本项目前端 [batch/node-registry.ts L38-L54](src/frontend/packages/workflow/playground/src/node-registries/batch/node-registry.ts) 路径 `BatchPath.Inputs`。
- 新 `BatchNodeConfigAdapter`。

#### 9.8 `'30' Input` (InputReceiver) —— 对齐 `receiver.Config.Adapt`
- Go 源：
  ```48:60:D:\Code\coze-studio-main\backend\domain\workflow\internal\nodes\receiver\input_receiver.go
  c.OutputSchema = n.Data.Inputs.OutputSchema
  SetOutputTypesForNodeSchema(n, ns)
  ```
- 新 `InputReceiverNodeConfigAdapter`：读 `data.inputs.outputSchema`（JSON string）+ outputs 登记。

**公共约束（9.1-9.8 都要做）**：
- 每个 Adapter 单测使用对应 fixture 片段：`data.inputs` 完整 / 缺必填项 / 含未知字段 三组输入。
- 单测断言：Config 字段类型和 Go `Adapt` 产出 `NodeSchema.Configs` 语义对等；`BigInt` 类 ID 保持 string。
- 所有 Adapter 注册到 [CozeNodeConfigAdapterRegistry.AdapterMap L57-L79](src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeNodeConfigAdapters.cs)。

### 第 10 轮：后端 Adapter 批量补齐（第 2 批 Variable/Database，Go 基准）

#### 10.1 `'11' Variable` —— **Coze Go 无基准**（`NodeTypeMetas` 无 ID 11）
- 按本项目前端 [variable/form-meta.tsx](src/frontend/packages/workflow/playground/src/node-registries/variable/form-meta.tsx) 声明字段自建：`variable_type`、`default_value`、`outputs`。注释里说明"Coze Go 未实现 id 11，本项目自研保留"。

#### 10.2 `'20' SetVariable`（Loop 内部）—— 对齐 `variableassigner.InLoopConfig.Adapt`
- Go 读 `inputParameters[]` 每项 `Left`/`Input` 组合成 Pair（Left 必须是可写的 loop 变量路径）。
- 新 `SetVariableInLoopNodeConfigAdapter`：严格复现 Pair 结构，注入作用域校验（下游 ValidateCanvas 时 fail-fast）。

#### 10.3 `'32' VariableMerge` —— 对齐 `variableaggregator.Config.Adapt`
- Go 读 `data.inputs.mergeGroups[]`（每组 `{ name, variables: ValueExpression[] }`），**不是** `data.groups`（前端 Coze 本身 mock 里有旧字段 `groups` 已废弃，以 `mergeGroups` 为准）。
- 新 `VariableAggregatorNodeConfigAdapter`：字段和 Go 完全一致。

#### 10.4 `'40' VariableAssign` —— 对齐 `variableassigner.Config.Adapt`
- Go 源：
  ```55:90:D:\Code\coze-studio-main\backend\domain\workflow\internal\nodes\variableassigner\variable_assign.go
  for i, param := range n.Data.Inputs.InputParameters {
      if param.Left == nil || param.Input == nil { return fail }
      leftSources, _ := CanvasBlockInputToFieldInfo(param.Left, ...)
      inputSource, _ := CanvasBlockInputToFieldInfo(param.Input, ...)
      pair := &Pair{ Left: *leftSources[0].Source.Ref, Right: inputSource[0].Path }
      pairs = append(pairs, pair)
  }
  c.Pairs = pairs
  ```
- 新 `VariableAssignNodeConfigAdapter`：**不可复用** `SetInputsForNodeSchema`（路径结构不同，Left 是**写目标**不是输入源）。必须自己解析 Left/Right 两端，产出 `pairs` 结构。

#### 10.5 `'42'/'43'/'44'/'46'` Database CRUD —— 对齐 `database/*.Adapt`
- Go 用 `DatabaseNode` struct（字段见 [canvas.go L91-L145](D:\Code\coze-studio-main\backend\domain\workflow\entity\vo\canvas.go)）：
  - 公共：`databaseInfoList`（含 `databaseInfoID` string）
  - Update：`updateParam`（`conditionList`、`fieldInfo`、`isBatch`）
  - Query：`selectParam`（`conditionList`、`orderByList`、`limit`、`fieldList`）
  - Delete：`deleteParam`（`conditionList`、`isBatch`）
  - Insert/Create：`insertParam`（`fieldInfo`、`isBatch`）
- 4 个新 Adapter：`DatabaseUpdateNodeConfigAdapter` / `DatabaseQueryNodeConfigAdapter` / `DatabaseDeleteNodeConfigAdapter` / `DatabaseCreateNodeConfigAdapter`。**`database_info_id` 保持 string**（Coze Snowflake）。

#### 10.6 `'12' DatabaseCustomSQL` —— 对齐 `customsql.Adapt`
- Go 源：
  ```44:69:D:\Code\coze-studio-main\backend\domain\workflow\internal\nodes\database\customsql.go
  dsList := n.Data.Inputs.DatabaseInfoList
  if len(dsList) == 0 { return error }
  sql := n.Data.Inputs.SQL
  if len(sql) == 0 { return error }
  c.SQLTemplate = sql
  ```
- 新 `DatabaseCustomSqlNodeConfigAdapter`：读 `databaseInfoList` + `sql`，必填校验前置。

- 所有 10.x Adapter 单测覆盖：有效输入 / 缺必填 / 大整数 ID 保精度。

### 第 11 轮：后端 Adapter 批量补齐（第 3 批 Chat/Trigger/其他）

按"有 Go 基准"和"无 Go 基准"分两类推进。

#### 11.A 有 Coze Go 基准 —— 严格对齐

**`'19' Break` / `'29' Continue`** —— Go `_break.Config.Adapt` / `_continue.Config.Adapt`：只需节点 meta + 空 config（执行期控制流），Adapter 仅保留节点骨架。

**`'27' DatasetWrite`** —— `knowledge.IndexerConfig.Adapt`：`datasetParam[0].input.value.content` 取 `datasetID`，加 `inputParameters`。

**`'37-39','51-57' Chat/Conversation/Message`（10 个）** —— Coze `backend/domain/workflow/internal/nodes/conversation/*` 每个 `*_config.go` 的 `Adapt`：
- `37 QueryMessageList` → `message_list_config.go`：读 `conversation_name`、`limit`、`order`、`before_id`、`after_id`。
- `38 ClearContext` → `clear_conversation_history_config.go`：`conversation_name`。
- `39 CreateConversation` → `create_conversation_config.go`：`conversation_name`、`bot_id`。
- `51 UpdateConversation`、`52 DeleteConversation`、`53 QueryConversationList`、`54 QueryConversationHistory`、`55 CreateMessage`、`56 UpdateMessage`、`57 DeleteMessage` —— 每个都按 Go 同名 config 逐字段对齐。
- 统一规则：所有 `conversation_id`/`message_id`/`bot_id` 保持 string。

**`'58' JsonStringify` / `'59' JsonParser`** —— Go `json.SerializationConfig.Adapt` / `DeserializationConfig.Adapt`：
  ```52:70:D:\Code\coze-studio-main\backend\domain\workflow\internal\nodes\json\json_serialization.go
  SetInputsForNodeSchema(n, ns)
  SetOutputTypesForNodeSchema(n, ns)
  ```
两个都只走通用 inputs/outputs，无额外字段。Adapter 可以共享 `PassThroughInputsOutputsAdapter` 基类。

#### 11.B 无 Coze Go 基准 —— 按本项目前端 form-meta 自建

**`'14' Imageflow`、`'16' ImageGenerate`、`'17' ImageReference`、`'23' ImageCanvas`、`'26' LTM`、`'34'/'35'/'36' Trigger*`、`'24' SceneVariable`、`'25' SceneChat`** —— Coze Go `NodeTypeMetas` 无对应 ID，本项目自研。Adapter 按：
- 前端对应 `node-registries/<name>/types.ts` 和 `form-meta.tsx` 声明字段
- 本项目对应 `NodeExecutor` 实际读哪些 Config key
两端取并集映射，并在 Adapter 文件顶注释：`// No Coze Go baseline; aligned with FE <name>/types.ts`。

- 合计覆盖目标：**9.x + 10.x + 11.x = 49/49**（Comment 31 / Database-parent 12 现状变更为 DatabaseCustomSQL，不再 bypass）。

### 第 12 轮：后端执行器缺失或错配核对
- 目标：基于第 7 轮修复后的映射，跑 `NodeExecutorRegistryCoverageTests`，补齐：
  - `'14' Imageflow`、`'16' ImageGenerate`、`'17' ImageReference` 执行器与新映射对齐
  - `'23' ImageCanvas` 在 `NodeExecutorRegistry` 确认注册
- 不引入新执行器实现，只纠正错配/缺失注册。

### 第 13 轮：后端保存→编译干跑（publish validate）集成测试
- 目标：`CozeWorkflowCompatIntegrationTests` 新增：
  - 注入 8 类 fixture 到 DB draft
  - 触发 `publish` → `CozeWorkflowPlanCompiler.Compile` + `ICanvasValidator.ValidateCanvas` 全量通过
  - 验证 `schema_json` DB 存取无字节丢失（SHA256 一致）

### 第 14 轮：前端变量引用索引与校验
- 目标：变量引用解析器 perf + 正确性：
  - `packages/workflow/variable` 下加 `buildVariableReferenceIndex(workflowJSON)` 供校验器使用
  - 变量密集 fixture（50+ 引用）保存/回显路径稳定
  - Condition/Loop/Batch 跨作用域引用的可见性规则验证

### 第 15 轮：Condition/Loop/Batch 端口与边一致性
- 目标：给 [workflow-document-with-format.ts](src/frontend/packages/workflow/nodes/src/workflow-document-with-format.ts) 的 `formatWorkflowJSON` 补硬断言：
  - Condition 动态分支 port → edge `sourcePortID` 必须在节点声明端口集合
  - Loop/Batch 的 `loop-output` / `loop-output-to-function` / `batch-output` / `batch-output-to-function` 配对正确
  - `SETTING_ON_ERROR_PORT = 'branch_error'` 分支 port 处理
- fixture `loop-nested.json` / `batch-processing.json` / `condition-branches.json` 专项测试。

### 第 16 轮：SubWorkflow 大整数 ID + 资源混合节点
- 目标：
  - `sub-workflow-big-id.json` roundtrip 无精度丢失（前端纯 string，后端 long 保序）
  - `external-resources-mixed.json` 覆盖 Plugin('4') + DatabaseQuery('43') + DatasetWrite('27') + Http('45') + CreateMessage('55')：保存→detail→compile→execute dry-run 全链路 adapter 字段对齐

### 第 17 轮：Playwright E2E（30 节点建图 roundtrip + 试运行）
- 目标：`src/frontend/e2e/app/workflow-large-schema.spec.ts`：
  - 基于 `long-chain-30.json` 通过 API 预置一个工作流，UI 打开
  - 断言节点数 = 30、边数正确、随机抽 5 个节点表单字段可回显
  - 触发 test_run，轮询 `get_process`，断言状态不为 `NODE_EXECUTOR_NOT_REGISTERED`
  - 失败 trace 保留

### 第 18 轮：百节点大图性能回归
- 目标：`large-graph-100.json` 在 CI 下：
  - 前端 `toJSON` p95 < 阈值
  - 保存 API 成功
  - 后端 compile + validate 不超时
  - console 无红色错误
- 纳入定期性能基线。

### 第 19 轮：CI 漂移守卫
- 目标：第 2 轮的 diff 工具加到 `pnpm turbo run lint` 或前端 pre-push hook：
  - 关键 Coze 同名文件偏离超过白名单阈值 → 失败
  - 白名单文件（本项目定制）维护在 `tools/coze-source-diff/allowlist.json`
- 文档记录如何从 Coze 同步新版本。

### 第 20 轮：最终报告
- 目标：回填 `docs/coze-workflow-large-schema-alignment.md` 末节：
  - 49 节点对齐状态（前端 registry / 后端 mapping / 后端 adapter / 后端 executor / 有无测试）
  - 8 类 fixture 通过情况
  - 已修漂移项清单与未修漂移项（白名单理由）
  - 剩余风险 + 后续 backlog

## 关键禁止事项（承接你的需求）

- 不新建 `WorkflowSchemaBuilder/Hydrator/Normalizer/Hasher/NodeSchemaAdapterRegistry` 等抽象；改用 Coze 现成 `WorkflowDocumentWithFormat` + `WorkflowJSONFormat` + 节点 `formatOnInit/formatOnSubmit`。原需求那份抽象清单与 Coze 现有机制功能重叠，新建会造成双写。
- 不为凑数创建假节点：以 `StandardNodeType` 49 项为准，`Knowledge`/`Card`/`Bot`/`Audio`/`Answer`/`AsyncTask` 不当作独立节点对齐。
- 不破坏已通过的 Loop/AI/Input。
- 不 mock 资源选择。

## 验收标准（映射你提的 20 项）

| 原验收 | 本计划覆盖 |
|---|---|
| 生成 alignment md | 第 1、20 轮 |
| 49 节点对齐关系 | 第 7、9-12、20 轮 |
| ≥ 20 公共适配器修复 | 第 9-11 轮（40 个 backend adapter + 前端验证器） |
| 统一 Builder | 不新建，用 Coze `toJSON`；第 5-6 轮保证 |
| 保存/回显/试运行同一 canonical | 已是；第 5 轮 roundtrip 证明 |
| 100 节点 build | 第 4、5、18 轮 |
| 100 节点 roundtrip 一致 | 第 5、18 轮 |
| 无 undefined/循环/非 JSON 原生对象 | 第 6 轮 invariants |
| ID string 精度 | 第 6、8、16 轮 |
| JSON 不被 HTML encode | 后端已用 Web defaults；第 13 轮字节对等测试证伪风险 |
| Condition 分支端口与 edges | 第 15 轮 |
| Loop/Batch body/done 端口 | 第 15 轮 |
| SubWorkflow 大整数 ID | 第 16 轮 |
| 资源节点 ID/名称回显 | 第 16 轮 |
| 变量引用稳定 | 第 14 轮 |
| trial run 与保存 schema 一致 | 架构上已一致；第 17 轮 E2E 验 |
| 后端不丢 node data | 第 13 轮字节对等 + 第 9-11 轮 adapter 覆盖 |
| TS 检查通过 | 每轮结束前 `pnpm -w typecheck` |
| .NET 编译通过 | 每轮结束前 `dotnet build` |
| 最终报告 | 第 20 轮 |

## 执行前请确认

路线定了，但有一项规模决定是否还可再砍：**第 9-11 轮合计新增 ~40 个后端 Adapter + 40 个 xUnit**，是工作量大头。若 CI/工期紧，可以用 **"语义对齐注释 + TODO + 仅 P0 节点上 Adapter（约 15 个）"** 替代；其余节点 Config 继续用默认映射（当前线上状态）。如果你接受现状 40 个裸 Config，告诉我是否把第 10/11 轮降级为"差缺报告 + 后续 backlog"。