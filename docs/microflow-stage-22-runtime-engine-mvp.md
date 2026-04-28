# Microflow Stage 22 - Runtime Engine MVP

## 1. Scope

本轮完成后端 Runtime Engine MVP：`POST /api/microflows/{id}/test-run` 从当前草稿或已保存 `MicroflowAuthoringSchema` 构建运行图，绑定参数，按 sequence flow 从 Start 执行到 End，并返回真实 `session.output`、`session.error`、`session.trace`、`session.logs`、`durationMs` 可由前端 Run 面板展示。

已支持 Start executor、End executor、Parameter binding、Create Variable executor、Change Variable executor、Decision true/false executor、Merge executor、Unsupported node handling、运行响应 DTO 复用现有 `MicroflowRunSessionDto` / trace frame，前端 Run 面板继续消费真实后端响应。

本轮不做 Call Microflow executor、Loop/List/Object/REST executor、完整表达式引擎、step debug、trace 可视化高亮、运行历史完整页面、schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/backend/Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs` | 新增 | Stage 22 MVP runtime engine、runtime context、graph builder、受限表达式 evaluator 与节点执行逻辑 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowTestRunService.cs` | 修改 | `test-run` 从旧 mock runner 切到 `IMicroflowRuntimeEngine` |
| `src/backend/Atlas.Application.Microflows/DependencyInjection/MicroflowApplicationServiceCollectionExtensions.cs` | 修改 | 注册 `IMicroflowRuntimeEngine` |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowSchemaReader.cs` | 修改 | 参数 `dataType=Number/number` 映射为 decimal |
| `tests/Atlas.AppHost.Tests/Microflows/MicroflowRuntimeEngineTests.cs` | 新增 | 覆盖 Stage 22 runtime MVP 关键路径 |
| `docs/microflow-stage-22-runtime-engine-mvp.md` | 新增 | 本轮范围、架构、契约、限制和验证记录 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | P2 运行入口 / 运行契约更新 Stage 22 状态 |

## 3. Runtime Architecture

| 组件 | 说明 |
|---|---|
| Controller | `MicroflowResourceController.TestRun` 继续使用 `POST /api/microflows/{id}/test-run`，不新增孤立 API |
| Application service | `MicroflowTestRunService` 负责加载草稿/已保存 schema、执行 validation gate、调用 runtime engine、持久化 run session / trace / logs |
| Runtime engine | `MicroflowRuntimeEngine` 只解释 Stage 22 支持节点，命中不支持节点立即 failed |
| Runtime context | 保存 runId、microflowId、inputs、variables、trace frames、logs、step count、startedAt、maxSteps |
| Graph builder | 从 `objectCollection.objects` 与 `flows` 建立 object/flow 字典，校验 flow source/target，查找唯一 root Start |
| Executor registry | 本轮在 engine 内按 `node.kind` / `action.kind` 分发，未引入完整 registry |
| Expression evaluator | 安全手写 parser，仅支持 MVP 字面量、变量、比较、布尔与简单加减 |
| Response mapper | 复用现有 `MicroflowRunSessionDto`，trace frame 作为 nodeResults 展示来源 |

## 4. API Contract

| API | 请求 DTO | 响应 DTO | 状态 |
|---|---|---|---|
| `POST /api/microflows/{id}/test-run` | `TestRunMicroflowApiRequest { schema?, input?, options? }` | `TestRunMicroflowApiResponse { session }` | 真实执行；`session.status=success/failed` |
| `GET /api/microflows/runs/{runId}` | path `runId` | `MicroflowRunSessionDto` | 返回持久化 session、output、error、trace、logs |
| `GET /api/microflows/runs/{runId}/trace` | path `runId` | `GetMicroflowRunTraceResponse` | 返回 trace 与 logs |

前端 adapter 仍将 `session.status=success` 映射为 `TestRunMicroflowResponse.status=succeeded`；failed 不会显示成成功。

## 5. Supported Nodes

| 节点 | 支持能力 | 限制 |
|---|---|---|
| Start | 记录 trace，要求唯一 normal outgoing flow | 无 outgoing 或多 outgoing 失败 |
| End | 读取 `returnValue` / `returnValueExpression`，为空返回 null | 表达式超出 MVP 范围失败 |
| Parameter | 运行前绑定 schema-level parameters 到 Variables | 以参数名查找；复杂 Object/List 只接受 JSON object/array |
| Create Variable | 执行 `variableName`、`dataType`、`initialValue` / `initialValueExpression` | 变量作用域为本次 run context |
| Change Variable | 按 `targetVariableName` 更新变量 | 本轮不按 id 解析 target；变量不存在失败 |
| Decision | 执行 boolean expression，按 true/false `caseValues` 选 flow | duplicate/missing true/false 分支失败 |
| Merge | 汇合通过，要求唯一 normal outgoing flow | 多 outgoing 失败 |

## 6. Unsupported Nodes

Call Microflow、Loop、Break、Continue、List / Collection、Object Activity、REST Call、Async Task、Message / UI 等节点本轮均不执行。运行路径命中时返回 `session.status=failed`，`error.code=RUNTIME_UNSUPPORTED_ACTION`，错误信息包含 node id、node name、node type，不跳过、不假成功。

## 7. Expression Evaluator MVP

支持：

| 类型 | 示例 |
|---|---|
| 字面量 | `"abc"`、`'abc'`、`100`、`12.5`、`true`、`false`、`null` |
| 变量引用 | `amount`、`userName`、`$amount` |
| 比较 | `amount > 100`、`amount >= 100`、`userName == "alice"`、`approvalLevel != "L1"` |
| 布尔 | `amount > 100 && userName == "alice"`、`amount > 100 || approvalLevel == "L2"`、`!isApproved` |
| 简单算术 | `amount + 1`、`amount - 1` |

不支持函数调用、成员访问、集合操作、任意代码执行、动态编译、文件/网络/环境访问。超出范围返回 `RUNTIME_EXPRESSION_ERROR`，节点 failed，run failed。

## 8. Execution Flow

`POST test-run` -> load draft schema 或当前 schema snapshot -> Stage 20 validation gate -> graph build -> bind parameters -> find Start -> node executor -> sequence flow selection -> End result -> persist run session / trace / logs -> response DTO -> frontend Run panel。

## 9. Error Handling

| 错误 | code | 展示策略 |
|---|---|---|
| microflowId 不存在 | API `MicroflowNotFound` | Controller 404 envelope |
| schema 不存在/格式错误 | API `MicroflowSchemaInvalid` / `RUNTIME_UNKNOWN_ERROR` | Run 面板显示 service error 或 session error |
| Start 缺失/多个 | `RUNTIME_START_NOT_FOUND` | session failed |
| flow 悬挂 | `RUNTIME_FLOW_NOT_FOUND` | session failed |
| 参数缺失 | `RUNTIME_VARIABLE_NOT_FOUND` | session failed |
| 参数类型错误 | `RUNTIME_VARIABLE_TYPE_MISMATCH` | session failed |
| 表达式不支持/失败 | `RUNTIME_EXPRESSION_ERROR` | 出错节点 trace failed |
| 变量不存在 | `RUNTIME_VARIABLE_NOT_FOUND` | 出错节点 trace failed |
| Decision 无匹配出边 | `RUNTIME_INVALID_CASE` | 出错节点 trace failed |
| Unsupported node | `RUNTIME_UNSUPPORTED_ACTION` | 出错节点 trace failed |
| MaxStepCount exceeded | `RUNTIME_MAX_STEPS_EXCEEDED` | session failed |
| cancellation requested | `RUNTIME_CANCELLED` | session failed |

## 10. Verification

自动测试：

- `dotnet build src/backend/Atlas.Application.Microflows/Atlas.Application.Microflows.csproj`
- `dotnet test tests/Atlas.AppHost.Tests/Atlas.AppHost.Tests.csproj --no-restore --filter "FullyQualifiedName~MicroflowRuntimeEngineTests" -p:BuildProjectReferences=false --logger "console;verbosity=minimal"`

手工验收建议：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 打开 `MF_ValidatePurchaseRequest`。
3. 配置 `amount:Number`、`userName:String`。
4. 构建 `Start -> Decision -> End`，Decision expression 为 `amount > 100`。
5. true 分支 End returnValue 为 `true`，false 分支为 `false`。
6. 保存并在 Run 面板输入 `amount=150`，确认真实 `POST /api/microflows/{id}/test-run` 返回 `success` / 前端显示 `succeeded`，result 为 `true`。
7. 输入 `amount=50`，确认 result 为 `false`。
8. 添加 Create Variable `approvalLevel = "L1"` 与 Change Variable `approvalLevel = "L2"`，End 返回 `approvalLevel`，确认 result 为 `"L2"`。
9. 让路径命中 REST Call，确认返回 failed / `RUNTIME_UNSUPPORTED_ACTION`，不是假成功。
10. 缺少 required parameter 或输入 `amount=abc`，确认 failed 与真实错误。
11. 打开另一个微流运行，确认变量和结果不串。
12. 查看 nodeResults/trace，确认包含 Start / Decision / End 或 Variable 节点执行记录。
