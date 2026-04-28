# Microflow Release Stage 17 - Run & Debug

## 1. Scope

本轮基于当前源码推进运行与调试基础闭环：Run / Test Run 按钮、Run Panel、参数输入表单、参数类型转换、validation gate、dirty Save & Run、真实 test-run API、Runtime Engine MVP、Start / End / Parameter / Variable / Decision / Merge executor、Call Microflow executor、call stack / recursion guard、nodeResults、Trace Panel、canvas highlight、run state isolation。

本轮不做完整 Mendix Runtime、Loop/List/Object/REST runtime、完整表达式语言、step debug / breakpoint、streaming trace、生产级运行历史页面、权限规则 runtime、数据库事务级对象提交 runtime。

依赖缺口：目标页真实 app/module 树和权限模型仍不是本轮范围；Object/List/REST/Loop runtime 已存在部分历史代码或 P0 能力，但本轮仅把执行路径中的未支持能力按真实错误处理。本轮最小补齐点：收紧 test-run 请求/响应契约、Run Panel 去除模拟选项、dirty 后按保存版本执行、补齐 response 顶层 run/result/nodeResults/callStack 字段。

### Run / Test-run API 盘点

| 能力 | 前端 adapter | 后端 API | Controller | Service | DTO | 当前语义 | 本轮处理 |
|---|---|---|---|---|---|---|---|
| test-run | `createHttpMicroflowRuntimeAdapter().testRunMicroflow` | `POST /api/microflows/{id}/test-run` | `MicroflowResourceController.TestRun` | `MicroflowTestRunService.TestRunAsync` | `TestRunMicroflowApiRequest/Response` | 真实 schema validation 后执行 runtime 并持久化 session/trace/logs | 补 `inputs/schemaId/version/debug/correlationId`，response 补顶层 run/result/nodeResults/callStack |
| run | 无独立 adapter | 未发现独立 `POST /api/microflows/{id}/run` | 无 | 无 | 无 | 本轮使用 test-run 作为编辑器执行入口 | 不新增 mock run API |
| run history | `listMicroflowRuns` | `GET /api/microflows/{id}/runs` | `ListRuns` | `ListRunsAsync` | `ListMicroflowRunsResponse` | 后端持久化 session 最小列表 | Trace Panel 保持可加载历史 |
| run detail | `getMicroflowRunDetail` | `GET /api/microflows/{id}/runs/{runId}` | `GetRunByMicroflow` | `GetRunSessionAsync` | `MicroflowRunSessionDto` | 返回 session + childRuns | 保持 |
| trace | `getMicroflowRunTrace/getTrace` | `GET /api/microflows/runs/{runId}/trace` | `GetTrace` | `GetRunTraceAsync` | `GetMicroflowRunTraceResponse` | 返回 trace/logs | Trace Panel 使用真实 trace |
| cancel | `cancelMicroflowRun` | `POST /api/microflows/runs/{runId}/cancel` | `Cancel` | `CancelAsync` | `CancelMicroflowRunResponse` | 标记 session cancelled | 保持 |
| runtime engine | `testRunMicroflow` 消费结果 | Application runtime | Controller 不直接执行 | `IMicroflowRuntimeEngine` | `MicroflowRunSessionDto/MicroflowTraceFrameDto` | 按 sequence flow 执行 schema | 本轮收紧 unsupported/correlation/response |
| Call Microflow | trace childRuns 展示 | runtime 内部加载 target schema | 同 test-run | `MicroflowRuntimeEngine` | child session/callStack | sync 子微流调用 | 保持并记录策略 |

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/runtime-adapter/types.ts` | 修改 | test-run request 支持可选 schema、schemaId/version/debug/correlationId，response 支持 unsupported |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/trace-types.ts` | 修改 | run/trace status 增加 unsupported，移除 Run Panel 中旧模拟选项类型 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/run-input-model.ts` | 修改 | buildRunRequest 默认不提交整份 draft schema，补 debug/correlationId |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/MicroflowTestRunModal.tsx` | 修改 | Run Panel 移除 simulate/decision/loop 等模拟控制，仅保留 allowRealHttp/maxSteps |
| `src/frontend/packages/mendix/mendix-microflow/src/runtime-adapter/local-adapter.ts` | 修改 | unsupported 响应状态归一，避免错误被显示成普通 failed |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/http-runtime-adapter.ts` | 修改 | POST `/api/microflows/{id}/test-run` 携带 inputs/debug/correlationId，识别 unsupported |
| `src/backend/Atlas.Application.Microflows/Models/MicroflowRuntimeDtos.cs` | 修改 | test-run DTO 增加 inputs/schemaId/version/debug/correlationId/timeout/mode；response 增加 runId/result/duration/nodeResults/callStack |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowTestRunService.cs` | 修改 | 接受 inputs alias/schemaId，按最新保存 schema 执行，返回顶层 run/debug 字段 |
| `src/backend/Atlas.Application.Microflows/Abstractions/IMicroflowTestRunService.cs` | 修改 | runtime request 增加 CorrelationId |
| `src/backend/Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs` | 修改 | runtime context 使用请求 correlationId 并传递到子调用 |
| `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` | 修改 | test-run 示例补 inputs/debug/correlationId 和顶层响应断言 |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Stage 17 run/debug 状态 |
| `docs/microflow-release-stage-17-run-debug.md` | 新增 | 本轮说明文档 |

## 3. Run API Contract

| API | Adapter 方法 | Request DTO | Response DTO | Status / Error |
|---|---|---|---|---|
| `POST /api/microflows/{microflowId}/test-run` | `testRunMicroflow` | `inputs/input`, `schemaId`, `version`, `debug`, `correlationId`, `options`, 可选 `schema` | `runId`, `status`, `result`, `errorCode`, `errorMessage`, `durationMs`, `traceId`, `nodeResults`, `callStack`, `session` | `succeeded`, `failed`, `unsupported`, `cancelled` |
| `GET /api/microflows/{id}/runs` | `listMicroflowRuns` | `pageIndex`, `pageSize`, `status` | `items`, `total` | history 持久化来自后端 |
| `GET /api/microflows/{id}/runs/{runId}` | `getMicroflowRunDetail` | path ids | `MicroflowRunSession` | 404 显示 run missing |
| `GET /api/microflows/runs/{runId}/trace` | `getMicroflowRunTrace` | `runId` | `trace`, `logs` | traceId 来自 response/header |

## 4. Run Input Model

参数优先来自 `schema.parameters`，缺失时回退 Parameter 节点，并显示 warning；两者不一致时优先 schema-level parameters。String 使用文本框，Integer/Long/Decimal 做数字转换，Boolean 使用 true/false 控件，DateTime 使用文本输入，Object/List/JSON 用 JSON 输入且不伪造对象，Unknown 按 JSON 提交并提示风险。required 缺失和类型错误在前端阻止 Run，输入状态按 microflowId 存在 `runInputsByMicroflowId`。

## 5. Validation / Save & Run Strategy

运行前执行本地 validation，并通过注入的 validation adapter 执行后端 validation；存在 error/blockSave/blockPublish 类严重问题时打开 Problems 并阻止运行。dirty=false 直接执行已保存 schema；dirty=true 时按钮显示 Save & Run，先保存当前 schema，保存成功后调用 test-run，保存失败或 conflict 不运行，不静默执行旧版本。

## 6. Runtime Architecture

Controller 是 `MicroflowResourceController.TestRun`，Service 是 `MicroflowTestRunService`。Service 从请求 draft schema 或已保存 snapshot 解析真实 schema，后端 validation 通过后调用 `IMicroflowRuntimeEngine`。Runtime 内部构建 `MicroflowRuntimeGraph`，按 sequence flow 执行，`RuntimeContext` 隔离变量、runId、rootRunId、parentRunId、callDepth、callStack、nodeResults/logs/childRuns。表达式求值器是安全手写 parser，不执行脚本。

## 7. Supported Executors

| Executor | 支持能力 | 限制 |
|---|---|---|
| Start | 唯一 start，单 outgoing | 多 start/无 outgoing 失败 |
| End | returnValueExpression 求值，产生 result | 空表达式返回 null |
| Parameter binding | required/default/type conversion | defaultValueExpression 仅按可解析字面量处理 |
| Create Variable | initialValueExpression 求值并写变量 | 不做复杂对象构造 |
| Change Variable | newValueExpression 求值并更新变量 | 目标变量不存在失败 |
| Decision | boolean expression true/false 分支 | duplicate/missing branch 失败 |
| Merge | passthrough 单 outgoing | 多 outgoing/无 outgoing 失败 |
| Call Microflow | 加载目标 schema、参数映射、子运行、返回绑定、call stack | 仅 sync call |
| Unsupported | 未支持节点真实失败 | 不跳过、不成功 |

## 8. Expression Evaluator MVP

支持字符串/数字/布尔/null 字面量、变量引用、比较运算、`&&`/`||`/`!` 和基础算术。禁止 C# eval、动态编译、脚本、文件/网络/环境访问；不支持表达式返回 `RUNTIME_EXPRESSION_ERROR`。

## 9. Call Microflow Runtime

Call Microflow 读取 `targetMicroflowId` 后加载目标微流最新 schema snapshot，参数 mapping 从父 context 求值并转换到目标参数类型；子 context 不共享父 variables，只通过输入和返回绑定交互。返回值可写入父变量。递归防护覆盖 self-call、A -> B -> A、`MaxCallDepth`，错误包含调用链。

## 10. Trace Panel Strategy

Trace Panel 展示 runId、status、duration、result/error、inputs/output、logs、nodeResults、callStack。`buildExecutionPath` 会展开 childRuns 并用 callDepth 缩进；点击 nodeResult 选中并定位当前画布节点，子微流节点在父画布定位 caller node。

## 11. Canvas Highlight Strategy

运行 trace 通过 `runtimeTrace` 投影到 FlowGram JSON 的 node/edge data，CSS 根据 `success/failed/unsupported/skipped/running` 高亮节点与边。Clear Trace 只清前端运行态，不写入 schema；切换微流时 `filterNodeResultsByMicroflowId` 只显示当前 microflow trace。

## 12. Error Mapping

| error | UI 行为 | status |
|---|---|---|
| validation blocked | 打开 Problems，禁止运行 | blocked |
| required missing/type invalid | Run Panel 字段错误，禁止运行 | blocked |
| network/401/403/404/409/422/500 | adapter 保留真实 message/traceId | failed |
| unsupported node/expression | Trace/Run Result 展示 code/message/failed node | unsupported/failed |
| target microflow missing | Call Microflow nodeResult 失败 | failed |
| recursion/max depth | callStack 展示调用链 | failed |
| timeout/max steps | runtime error 展示 | failed |

## 13. Verification

已计划验证：`dotnet build src/backend/Atlas.Application.Microflows/Atlas.Application.Microflows.csproj`、前端 package type/build 检查、Run Panel 单元测试、后端 Runtime MVP 测试。手工验收按本轮清单覆盖 Start/Decision/End、Create/Change Variable、Call Microflow、unsupported、required/type error、A/B/C 隔离、Clear Trace。
