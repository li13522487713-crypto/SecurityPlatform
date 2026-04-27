# Microflow Stage 23 - Call Microflow Runtime

## 1. Scope

本轮完成：
- Call Microflow 后端执行器落在 Runtime Engine 执行链路（非 fake call）。
- 运行时按 `targetMicroflowId` 加载目标微流 schema。
- `parameterMappings` 真正执行并参与子微流入参绑定。
- 子微流运行结束后执行返回值绑定。
- 增加运行时调用栈、递归检测、最大调用深度限制。
- `nodeResults`（trace）补充 `microflowId`，并返回 `childRuns`。
- 前端 Run 面板显示 child run、call stack、mapping/recursion/callMode 错误。

本轮不做：
- async/fire-and-forget 真实执行（仅报 unsupported）。
- Loop / Break / Continue 真实执行。
- List / Object / REST Call 执行增强。
- 完整表达式语言与可视化 trace 高亮。
- 运行历史完整页面与 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/backend/Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs` | 修改 | 增加 Call Microflow 真实执行、参数映射、返回绑定、调用栈、递归与深度保护 |
| `src/backend/Atlas.Application.Microflows/Models/MicroflowRuntimeDtos.cs` | 修改 | 扩展 error/session/trace 字段（microflowId/callStack/correlationId）并补错误码 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowTestRunService.cs` | 修改 | 持久化与回读新增 call stack / correlation / trace microflowId 字段 |
| `tests/Atlas.AppHost.Tests/Microflows/MicroflowRuntimeEngineTests.cs` | 修改 | 新增 Call Microflow、参数映射、递归、深度、callMode 等测试 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/trace-types.ts` | 修改 | 扩展 Run Session / Trace / Error 类型以承载 childRuns 与 callStack |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/runtime-error-codes.ts` | 修改 | 增加 Stage 23 Call Microflow 相关错误码 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/MicroflowTestRunModal.tsx` | 修改 | Run 面板展示 child runs、child node outputs、call stack |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 23 对应 P2 状态 |

## 3. Call Microflow Schema Contract

| 语义 | 源码字段 | 类型 | 当前是否存在 | Runtime 使用方式 |
|---|---|---|---|---|
| Action kind | `action.kind = "callMicroflow"` | `string` | 是 | 命中 Call Microflow 执行分支 |
| 目标微流主键 | `targetMicroflowId` | `string` | 是 | 唯一执行主键；为空即失败 |
| 目标显示限定名 | `targetMicroflowQualifiedName` | `string?` | 是 | 仅展示/日志辅助，不参与主键查找 |
| 目标显示名称 | `targetMicroflowName` / `displayName` | `string?` | 是（`targetMicroflowName`） | 仅展示辅助 |
| 参数映射集合 | `parameterMappings[]` | `array` | 是 | 构造子微流入参 |
| 映射目标参数名 | `parameterName`（兼容 `targetParameterName`） | `string?` | 是 | 与子微流参数名匹配 |
| 映射目标参数 ID | `parameterId`（兼容 `targetParameterId`） | `string?` | 是 | 参数备用匹配键 |
| 映射表达式 | `argumentExpression` / `expression` / `valueExpression` | `string?` | 是 | 父变量上下文表达式求值 |
| 映射源变量 | `sourceVariableName` / `sourceVariableId` | `string?` | 是 | 直接读取父变量 |
| 返回绑定配置 | `returnValue` | `object?` | 是 | 控制是否做返回值写回 |
| 返回绑定开关 | `returnValue.storeResult` | `bool?` | 是 | 决定是否绑定 child output |
| 返回目标变量 | `returnValue.outputVariableName`（兼容 `outputVariableName`/`resultVariableName`） | `string?` | 是 | 子结果写回父变量名 |
| 调用模式 | `callMode` | `string?` | 是 | 仅 `sync` 支持，`async`/`fire-and-forget` 返回 unsupported |
| 错误处理配置 | `errorHandling`（若存在于 action config） | `object?` | 否（本轮 schema 未使用） | 本轮 runtime 不消费 |

## 4. Runtime Architecture Update

- 在 `MicroflowRuntimeEngine` 内补充 `ExecuteCallMicroflowAsync`，保持 Stage 22 主执行链不变。
- 运行时上下文新增 `RunId/ParentRunId/RootRunId/CorrelationId/CallDepth/CallStackPath`。
- 子微流通过递归 `RunInternalAsync` 执行，创建独立变量上下文。
- 父子变量不共享，仅通过参数映射输入 + return binding 输出。
- 目标 schema 由 `IMicroflowResourceRepository` + `IMicroflowSchemaSnapshotRepository` 加载，避免 HTTP 调用。

## 5. Parameter Mapping Strategy

- 参数来源：子微流 schema-level `parameters`。
- required 参数：缺 mapping 直接失败（`RUNTIME_PARAMETER_MAPPING_MISSING`）。
- mapping 支持：
  - `sourceVariableName` 直接读取父变量。
  - `argumentExpression` / `expression` / `valueExpression` 走表达式求值。
- optional 参数：无 mapping 时优先 defaultValue，其次 `null`。
- 映射值按目标参数类型做 coercion，失败返回 `RUNTIME_PARAMETER_MAPPING_FAILED`。
- 指向不存在目标参数的 mapping 记录为 ignored warning，不阻断执行。

## 6. Return Binding Strategy

- `returnValue.storeResult=true` 且存在目标变量名时执行绑定。
- 目标变量已存在则覆盖，不存在则创建。
- 子微流无返回值或目标 returnType=void 但配置了绑定时失败（`RUNTIME_RETURN_BINDING_FAILED`）。
- 子微流失败不做绑定，父微流直接失败（`RUNTIME_CHILD_MICROFLOW_FAILED`）。

## 7. Recursion Guard

- self call：`sourceMicroflowId == targetMicroflowId` -> `RUNTIME_CALL_RECURSION_DETECTED`。
- active stack cycle：`targetMicroflowId` 已存在于 call stack -> `RUNTIME_CALL_RECURSION_DETECTED`。
- max depth：`currentDepth >= MaxCallDepth` -> `RUNTIME_CALL_STACK_OVERFLOW`。
- 错误对象携带 `microflowId` + `callStack`。

## 8. NodeResults / Call Stack Result

- 采用扁平 trace + childRuns：
  - `trace[*].microflowId` 标记所属微流。
  - `session.childRuns[*]` 保存子微流完整 trace。
  - `session.callStack` / `error.callStack` 返回调用链。
- 父 Call 节点与子微流节点均可在响应中定位。

## 9. Frontend Display

- Run 面板继续展示主 session，并新增：
  - child runs 状态与子节点输出；
  - call stack 文本；
  - mapping / recursion / unsupported callMode 错误码与信息。
- 未实现 trace 可视化高亮树，留待 Stage 24。

## 10. Verification

自动测试：
- `MicroflowRuntimeEngineTests` 新增 Call Microflow 成功、参数缺失、callMode 不支持、target 不存在、递归、深度限制等场景。

手工测试建议：
- 按 Stage 23 验收步骤执行 `MF_SubmitPurchaseRequest -> MF_ValidatePurchaseRequest` 场景；
- 检查响应中 `childRuns`、`trace.microflowId`、`error.callStack`。
