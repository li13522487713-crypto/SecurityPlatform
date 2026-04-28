# Microflow Stage 24 - Trace Panel & Run History

## 1. Scope

本轮完成：
- Trace 面板（Execution Path / Node Results / Call Stack / Inputs / Output / Logs / Errors）。
- Run History 面板（列表、过滤、刷新、选择 run 查看详情）。
- `nodeResults`（基于 `trace`）按执行顺序展示，并支持 child run 按 `callDepth` 展示。
- 点击 Trace 节点项定位到画布节点/连线。
- 画布执行高亮（success / failed / skipped / unsupported）与 failed/unsupported 错误提示。
- Run 完成后写入 trace state，并刷新当前微流历史。
- A/B 微流隔离：history、selected run、trace highlight 全部按 `microflowId` 分桶。
- 接入真实后端 history API（无 mock / fake history）。

本轮不做：
- step debug / breakpoint / step over / step into。
- 实时流式 trace。
- 完整性能分析器。
- 新节点执行器。
- 独立 demo 页面。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowResourceController.cs` | 修改 | 新增 `GET /api/microflows/{id}/runs` 与 `GET /api/microflows/{id}/runs/{runId}` |
| `src/backend/Atlas.Application.Microflows/Abstractions/IMicroflowTestRunService.cs` | 修改 | 新增 run history 列表与按微流详情查询接口 |
| `src/backend/Atlas.Application.Microflows/Models/MicroflowRuntimeDtos.cs` | 修改 | 新增 run history list DTO/请求 DTO |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowTestRunService.cs` | 修改 | 实现 run history 列表、run detail 子调用图递归组装 |
| `src/backend/Atlas.Application.Microflows/Repositories/IMicroflowRepositories.cs` | 修改 | run session 列表接口增加 status filter/count |
| `src/backend/Atlas.Infrastructure/Repositories/Microflows/MicroflowRepositories.cs` | 修改 | 落地 status filter + count 查询 |
| `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` | 修改 | 增补 history list/detail 调用示例 |
| `src/frontend/packages/mendix/mendix-microflow/src/runtime-adapter/types.ts` | 修改 | 新增 Run History 类型与 adapter 方法 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/http-runtime-adapter.ts` | 修改 | 接入 history list/detail API，并做 normalize |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/editor-save-bridge.ts` | 修改 | 透传 history list/detail 方法 |
| `src/frontend/packages/mendix/mendix-microflow/src/runtime-adapter/local-adapter.ts` | 修改 | local 路径补齐 session-only run history 能力 |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | Run/Trace/History 状态分桶、面板集成、点击定位、隔离与防乱序 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/MicroflowTracePanel.tsx` | 新增 | Trace 面板 UI |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/MicroflowRunHistoryPanel.tsx` | 新增 | Run History 面板 UI |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/trace-history-utils.ts` | 新增 | execution path / status / filter 工具函数 |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/adapters/authoring-to-flowgram.ts` | 修改 | runtime error 透传到节点 data |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowCanvas.tsx` | 修改 | 增加 trace 定位滚动到节点能力 |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNodeRenderer.tsx` | 修改 | failed/unsupported 错误 tooltip 与状态 tag |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowTypes.ts` | 修改 | 节点 runtime 错误字段扩展 |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/styles/flowgram-microflow-node.css` | 修改 | unsupported 节点高亮样式 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/contracts/api/microflow-runtime-api-contract.ts` | 修改 | 补充 run history API 契约类型 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/__tests__/trace-history-utils.test.ts` | 新增 | Stage 24 纯函数测试 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | P2 Stage 24 状态更新 |

## 3. Run Result Contract

| 语义 | 后端 DTO | 前端类型 | 说明 |
|---|---|---|---|
| runId | `MicroflowRunSessionDto.id` | `MicroflowRunSession.id` | 运行唯一标识 |
| microflowId | `MicroflowRunSessionDto.resourceId` / `trace[*].microflowId` | `resourceId` / `MicroflowTraceFrame.microflowId` | session 所属微流 + trace 所属微流 |
| status | `MicroflowRunSessionDto.status` | `MicroflowRunSession.status` | `success/failed/cancelled` |
| result | `MicroflowRunSessionDto.output` | `MicroflowRunSession.output` | 运行输出 |
| errorCode/errorMessage | `MicroflowRuntimeErrorDto.code/message` | `MicroflowRuntimeError.code/message` | 失败或 unsupported 原因 |
| durationMs | `endedAt-startedAt`（服务侧计算） | 前端派生字段 | list/history 展示 |
| startedAt/completedAt | `startedAt/endedAt` | `startedAt/endedAt` | 运行时间窗口 |
| logs | `MicroflowRunSessionDto.logs` | `MicroflowRuntimeLog[]` | 结构化日志 |
| nodeResults | `MicroflowRunSessionDto.trace` | `MicroflowTraceFrame[]` | Stage 24 用 trace 作为 nodeResults 展示源 |
| callStack | `MicroflowRunSessionDto.callStack` | `MicroflowRunSession.callStack` | 调用链 |

## 4. Run History Contract

- API：
  - `GET /api/microflows/{microflowId}/runs?pageIndex=1&pageSize=20&status=all`
  - `GET /api/microflows/{microflowId}/runs/{runId}`
- DTO：
  - `ListMicroflowRunsResponse`（`items[] + total`）
  - `MicroflowRunHistoryItemDto`（runId/status/duration/startedAt/completedAt/errorMessage/summary）
- 持久化：
  - 复用既有 `MicroflowRunSession` / `MicroflowRunTraceFrame` / `MicroflowRunLog` 表，无新增并行表。
  - `TestRunAsync` 成功/失败/unsupported/cancelled 均会写入 run record。
- 前端策略：
  - 仅通过 adapter 请求真实 API；组件内不裸 fetch、不 localStorage 伪造持久 history。

## 5. Trace Panel Strategy

- Execution Path：按执行顺序 + `callDepth` 展示所有节点。
- Node Results：以结构化节点结果（由 trace 投影）展示输入/输出/耗时/错误/父子关系。
- Call Stack：展示 run 调用链与 child run 信息。
- Inputs/Output：保留完整 JSON 字段。
- Logs：展示 run 日志集合。
- Errors：展示 run/frame 错误，并支持定位。

## 6. Canvas Highlight Strategy

- `nodeResult(trace) -> canvas node`：通过 `objectId` 映射，`microflowId` 不匹配时仅允许回退定位 caller 节点。
- 节点状态：`success/failed/skipped/unsupported/running` 映射到 FlowGram runtime class。
- failed badge/提示：节点渲染层显示 error code/message tooltip。
- 清理策略：
  - 新运行开始先清空当前微流选中 run 与 focus。
  - Clear Trace 清空当前微流 run/selection。
  - 切换微流按 `microflowId` 读取对应 state，不复用其他微流高亮。
- 不写入 schema：trace 高亮仅存运行态内存状态。

## 7. Problems vs Trace

- Problems：设计期 validation（保存门禁、schema 规则）。
- Trace：运行期执行结果（run context、nodeResults、call stack、runtime errors）。
- 两者共用底部容器，但 Tab 语义分离，互不覆盖。

## 8. Isolation Strategy

- `runHistoryByMicroflowId`
- `selectedRunIdByMicroflowId`
- `runSessionByMicroflowId`
- `runtimeServiceErrorByMicroflowId`
- `runHistoryLoadingByMicroflowId`
- `runHistoryErrorByMicroflowId`
- 请求乱序保护：`runHistoryRequestSeqRef[microflowId]`，仅应用最新请求结果。

## 9. Verification

自动测试：
- `trace-history-utils.test.ts`
  - `normalizeRunHistoryStatus`
  - `buildRunHistoryItemFromSession`
  - `buildExecutionPath`
  - `filterNodeResultsByMicroflowId`

手工验证（Stage 24）：
- 成功 run：Trace execution path + 节点定位 + succeeded 高亮。
- 失败/unsupported run：failed 节点错误显示 + 定位。
- Call Microflow run：child run 与 call stack 展示。
- Run History：list/detail/过滤/刷新/error-retry。
- A/B 微流切换：history/trace/highlight 不互串。
