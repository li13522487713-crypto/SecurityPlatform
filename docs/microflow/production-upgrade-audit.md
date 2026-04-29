# Microflows 生产化升级审计报告

> 轮次：R1 — 审计 + 节点矩阵 + 生产门禁骨架  
> 范围：本文件按 todo 逐步补齐。当前已闭环 `R1-01-audit-frontend` 前端源码审计章节；后端审计、综合分级和阶段计划将在 `R1-02` / `R1-03` 继续补齐。

## 1. R1 todo 闭环状态

| Todo | 状态 | 本文件章节 | 验证方式 |
|---|---|---|---|
| R1-01-audit-frontend | 完成 | §2 | 证据点 F-01 ~ F-16 均来自当前前端源码路径与行号 |
| R1-02-audit-backend | 待执行 | 待补 | 后续补充 AppHost / Application / Runtime 证据 |
| R1-03-audit-doc | 待执行 | 待补 | 后续汇总 30+ 证据点、分级与阶段计划 |

## 2. 前端源码审计（R1-01）

### 2.1 审计结论

前端 Microflows 已具备生产化基础：AppWeb adapter 已默认走 HTTP、错误 envelope 已覆盖 401/403/404/409/422/500、多 tab dirty/save 隔离已有 spec、保存 payload 已带 `baseVersion`/`schemaId`/`version`/`saveReason`/`clientRequestId`，冲突弹窗已出现四个操作入口。

但仍存在生产发布前必须纳入后续轮次的缺口：

- **Blocker**：生产构建没有在配置入口直接拒绝 `VITE_MICROFLOW_API_MOCK=msw`；store 仍保留 `SAMPLE_PROCUREMENT_APP` fallback。
- **Critical**：前端 registry 将 `rollback` / `listOperation` 标为 supported，但后端仍是 modeled-only 占位，存在“前端可建模、运行时假成功”的一致性风险。
- **Major**：property panel 目前只有注册表抽象，缺少 R3 要求的专用 forms；Gateway、Step Debug、Expression Editor 仍是建模或基础能力，未达到 41 章生产级目标。
- **Minor**：部分用户可见文案仍集中在组件内，后续 UI 变更需要同步 i18n 基线。

### 2.2 前端证据点

| ID | 分级 | 证据路径 | 现象 | 生产风险 | 修复轮次 |
|---|---|---|---|---|---|
| F-01 | Blocker | `src/frontend/apps/app-web/src/app/microflow-adapter-config.ts:3-4` | `isMicroflowContractMockEnabled()` 读取 `VITE_MICROFLOW_API_MOCK` / `MICROFLOW_API_MOCK` 是否为 `msw`。 | 生产构建若注入 mock 环境变量，当前入口不会直接 fail build。 | R2 |
| F-02 | Critical | `src/frontend/apps/app-web/src/app/microflow-adapter-config.ts:34-39` | `import.meta.env.PROD` 时 mode 被强制为 `http`，但返回值仍受 `contractMockEnabled` 分支影响。 | production no-mock 策略需要构建期扫描与运行期拒绝双保险。 | R2 |
| F-03 | Major | `src/frontend/apps/app-web/src/app/microflow-adapter-config.ts:45-47` | 401/403/API error 已派发 `atlas:microflow-unauthorized` / `atlas:microflow-forbidden` / `atlas:microflow-api-error`。 | 事件机制可用，但 R2 仍需覆盖 spec 与全链路 UI 处理。 | R2 |
| F-04 | Blocker | `src/frontend/packages/mendix/mendix-studio-core/src/store.ts:8-9` | store 仍 import `SAMPLE_PROCUREMENT_APP` / `SAMPLE_RUNTIME_OBJECT`。 | 生产入口可能保留 demo fallback 数据源。 | R2 |
| F-05 | Blocker | `src/frontend/packages/mendix/mendix-studio-core/src/store.ts:450-453` | `loadSampleApp()` 仍会把采购样例写入 `appSchema` 与 runtime object。 | 空 app / 无权限 / 后端不可用时可能退回示例应用，掩盖 403/404。 | R2 |
| F-06 | Major | `src/frontend/packages/mendix/mendix-studio-core/src/store.ts:84-102` | `MicroflowSaveConflict` / `MicroflowSaveState` 已有 `remoteVersion`、`remoteUpdatedAt`、`remoteUpdatedBy`、`traceId` 字段。 | 前端已为 409 生产冲突准备字段，但后端 envelope 尚需 R2 补齐。 | R2 |
| F-07 | Major | `src/frontend/packages/mendix/mendix-studio-core/src/workbench-tabs-lifecycle.spec.ts:110-124` | spec 已覆盖 dirty tab 关闭 guard 与 force close。 | 多 tab 基础已测，但 beforeunload / switch tab dirty guard 仍需补充。 | R2 |
| F-08 | Major | `src/frontend/packages/mendix/mendix-studio-core/src/workbench-tabs-lifecycle.spec.ts:148-178` | spec 已覆盖不同 microflow 的 dirty/save 状态隔离。 | 已具备隔离基础；R2 需扩展 history / save queue / close-switch 场景。 | R2 |
| F-09 | Critical | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx:191-197` | save 请求已传 `baseVersion`、`schemaId`、`version`、`saveReason`、`clientRequestId`、`force`。 | payload 已接近生产要求；需 spec 固化并与后端幂等/409 语义对齐。 | R2 |
| F-10 | Critical | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx:221-239` | 409 时进入 `conflict` 状态，但 `remoteUpdatedAt` / `remoteUpdatedBy` 仍写 `undefined`。 | 用户无法判断远端保存者与时间，R2 需后端返回并前端展示。 | R2 |
| F-11 | Major | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx:490-499` | 冲突弹窗已有 Reload Remote / Keep Local / Force Save / Cancel 四个按钮。 | Force Save 二次确认与按钮行为 spec 仍需补齐。 | R2 |
| F-12 | Critical | `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts:341-363` | `SUPPORTED_ACTION_KINDS` 包含 `rollback` 和 `listOperation`。 | 与后端 modeled-only 真实能力不一致，可能误导发布前判断。 | R1/R3 |
| F-13 | Critical | `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts:365-393` | `PARTIAL_ACTION_KINDS` 列出 connector-backed 动作与 runtime command 类动作。 | 需要 R1 矩阵和 verify 固化前后端 capability gate，否则工具箱与 runtime 漂移。 | R1 |
| F-14 | Major | `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts:403-416` | `engineSupportFromAction()` 仅基于前端 set 判断 supported/partial/unsupported。 | 需要 machine-readable matrix 校验后端 descriptor，不能只靠前端静态 set。 | R1 |
| F-15 | Major | `src/frontend/packages/mendix/mendix-microflow/src/property-panel/node-form-registry.ts:8-39` | property panel 当前只有 `registerMicroflowNodeForm()` / `getMicroflowNodeFormForObject()` 注册机制。 | R3 要求的 Rollback/Cast/ListOperation 等专用表单尚未落地。 | R3 |
| F-16 | Major | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-error.ts:7-42` | API 错误已按 401/403/404/409/422/500 映射为统一错误码。 | 错误 envelope 基础已具备；R2 需补齐 spec 与冲突字段解析。 | R2 |

### 2.3 前端后续修复计划

| 修复项 | 目标轮次 | 验收 |
|---|---|---|
| production build/runtime 拒绝 mock/local/MSW | R2 | `verify-microflow-production-no-mock.ts` 扫描产物并失败退出 |
| 移除 `SAMPLE_PROCUREMENT_APP` 生产 fallback | R2 | app-explorer spec 覆盖空 app、403、404，不回退 sample |
| 固化 save payload 与 409 conflict UX | R2 | save/conflict spec 覆盖 baseVersion/clientRequestId/saveReason 与四按钮行为 |
| 三方节点矩阵校验前端 supported/partial 与后端 descriptor | R1 | `verify-microflow-node-capability-matrix.ts` |
| 补齐 property panel forms | R3 | 每个 R3 表单有 spec，reload 不丢字段 |
| Gateway trueParallel、Expression Editor、Step Debug UI | R4 | 对应 verify + spec + 后端 API 测试 |
