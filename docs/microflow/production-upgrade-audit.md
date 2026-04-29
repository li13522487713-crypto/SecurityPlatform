# Microflows 生产化升级审计报告

> 轮次：R1 — 审计 + 节点矩阵 + 生产门禁骨架  
> 范围：本文件按 todo 逐步补齐。当前已闭环 `R1-01-audit-frontend` 与 `R1-02-audit-backend`；综合分级和阶段计划将在 `R1-03` 继续补齐。

## 1. R1 todo 闭环状态

| Todo | 状态 | 本文件章节 | 验证方式 |
|---|---|---|---|
| R1-01-audit-frontend | 完成 | §2 | 证据点 F-01 ~ F-16 均来自当前前端源码路径与行号 |
| R1-02-audit-backend | 完成 | §3 | 证据点 B-01 ~ B-20 均来自当前后端源码路径与行号 |
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

## 3. 后端源码审计（R1-02）

### 3.1 审计结论

后端 Microflows 已经不是纯演示实现：AppHost 端点统一收敛到 `api/v1`，基类默认 `[Authorize]`，请求经过 API exception / production guard / workspace ownership filters；runtime action registry 已将 Server / Connector / Command / Unsupported 四类分开；FlowGram JSON 已被 schema helper 拒绝；RestCall 已有 SSRF 安全策略和 production safe defaults。

但距离 41 章生产化仍有明确缺口：

- **Blocker**：`rollback` / `cast` / `listOperation` 仍是 `ConfiguredMicroflowActionExecutor`，Server fallback 会成功返回，需 R1 矩阵标红并在 R3 真实实现。
- **Blocker**：health 端点继承基类 `[Authorize]`，当前无显式 `[AllowAnonymous]`，与 R2 生产探活要求不一致。
- **Critical**：workspace ownership filter 已覆盖 query/header/route id，但未覆盖 `appId` 资产反查。
- **Critical**：save DTO 已有 `ClientRequestId`，但 `SaveSchemaAsync` 未使用它做幂等；409 details 也未结构化返回 remoteVersion/remoteUpdatedAt/remoteUpdatedBy。
- **Major**：parallel/inclusive gateway 在 runtime 主路径仍显式 unsupported；Expression Parser/Evaluator 已有基础但无 API/editor 共享 AST 闭环；Step Debug session API 尚未落地。

### 3.2 后端证据点

| ID | 分级 | 证据路径 | 现象 | 生产风险 | 修复轮次 |
|---|---|---|---|---|---|
| B-01 | Major | `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowApiControllerBase.cs:8-13` | Microflow 控制器基类统一 `[ApiController]`、`[Authorize]`、Exception/Production/Ownership filters。 | 基础安全链路已具备，但 health 匿名例外需显式处理并测试。 | R2 |
| B-02 | Blocker | `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowResourceController.cs:334-356` | `GET api/v1/microflows/health` 未标 `[AllowAnonymous]`。 | 生产 health probe 可能被认证阻断，且与用户要求不一致。 | R2 |
| B-03 | Major | `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowResourceController.cs:357-391` | runtime health 检查 descriptor count、runtime limits、Rest 安全默认。 | 可作为 production gate live health 基础，但 R1 仅骨架，R5 才全量联通。 | R1/R5 |
| B-04 | Major | `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowResourceController.cs:393-400` | storage health 已挂载到 `api/v1/microflows/storage/health`。 | 需要 R5 production gate 统一纳入 live health。 | R5 |
| B-05 | Critical | `src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowWorkspaceOwnershipFilter.cs:34-69` | filter 要求登录、workspaceId、workspace 格式和 workspace membership。 | 已有 ownership 基础；仍需覆盖 appId 资产入口与测试。 | R2 |
| B-06 | Critical | `src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowWorkspaceOwnershipFilter.cs:71-94` | workspaceId 解析覆盖 query、request context/header、route `{id}` 反查 resource。 | `api/v1/microflow-apps/{appId}` 未通过 appId 反查 workspace，可能绕过资产归属校验。 | R2 |
| B-07 | Critical | `src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowProductionGuardFilter.cs:25-49` | production guard 在非开发环境要求认证与 `X-Workspace-Id`，health path 放行。 | 还未扫描 mock/seed/internal-debug 配置；需 R2 硬化。 | R2 |
| B-08 | Major | `src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowProductionGuardFilter.cs:53-55` | production guard 由 `!IsDevelopment && EnableProductionGuard` 控制。 | 默认策略可用；需测试 production config 缺陷时 fail closed。 | R2 |
| B-09 | Blocker | `src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs:291-309` | `rollback` / `cast` / `listOperation` 明确由 `ConfiguredMicroflowActionExecutor` 占位。 | Server fallback 成功返回，无法满足生产级事务回滚/类型转换/列表操作语义。 | R1/R3 |
| B-10 | Critical | `src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs:325-332` | connector-backed 集成动作保留 capability gate，缺 connector 返回 `RUNTIME_CONNECTOR_REQUIRED`。 | R3 需补 connector stub 接口与 DI，R1/R3 verify 必须防止 silent success。 | R1/R3 |
| B-11 | Major | `src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs:334-340` | showPage/showMessage/downloadFile 等 client action 注册为 RuntimeCommand。 | server 只产 command preview；前端/发布矩阵需明确不可当服务端真实执行。 | R1/R5 |
| B-12 | Critical | `src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs:441-566` | `ConfiguredMicroflowActionExecutor` 对 ConnectorBacked/Unsupported 有错误路径，但 Server fallback 返回 Success。 | R1 coverage verify 需禁止 supported 节点落到 Configured fake success。 | R1/R3 |
| B-13 | Critical | `src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs:277-334` | `SaveSchemaAsync` 做 baseVersion 比对并返回 409，但 details 仅 `resource.SchemaId ?? resource.Version`。 | 冲突 UI 缺 remoteVersion/remoteUpdatedAt/remoteUpdatedBy 结构化证据。 | R2 |
| B-14 | Critical | `src/backend/Atlas.Application.Microflows/Models/MicroflowResourceApiDtos.cs:151-165` | save DTO 已定义 `BaseVersion` / `SchemaId` / `Version` / `SaveReason` / `ClientRequestId`。 | DTO 有字段但服务未用 `ClientRequestId` 做幂等。 | R2 |
| B-15 | Major | `src/backend/Atlas.Application.Microflows/Services/MicroflowSchemaJsonHelper.cs:27-40` | schema helper 拒绝 `nodes` / `edges` / `workflowJson` / `flowgram`。 | AuthoringSchema-only 方向正确，R2 需补持久化路径测试证明无 FlowGram JSON。 | R2 |
| B-16 | Blocker | `src/backend/Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs:249-252` | parallel/inclusive gateway 在 runtime 主路径显式 unsupported。 | 不满足 trueParallel 与 inclusive activation set 要求。 | R4 |
| B-17 | Major | `src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionParser.cs:5-42` | 后端已有 parser，可解析 if/then/else 等表达式并产 diagnostics。 | R4 仍需 lexer/parser/typechecker/evaluator/formatter/completion/preview API 与前端 editor 共享语义。 | R4 |
| B-18 | Major | `src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionModels.cs:438-448` | `IMicroflowExpressionEvaluator` 暴露 Parse/Infer/Evaluate。 | 运行时基础有了，但尚无 `api/v1/microflow-expressions/*` 端点。 | R4 |
| B-19 | Major | `src/backend/Atlas.Application.Microflows/Runtime/MicroflowVariableStoreModels.cs:57-107` | 已有 `MicroflowRuntimeVariableValue` 与 `MicroflowVariableDefinition`。 | R3 变量类型系统需进一步收敛为 RuntimeObjectRef/ListValue/PrimitiveValue 等强类型。 | R3 |
| B-20 | Critical | `src/backend/Atlas.Application.Microflows/Runtime/Actions/Http/MicroflowRestSecurityPolicy.cs:43-109` | Rest security policy 拒绝空 URL、非法 scheme、denylist、私网/localhost。 | SSRF 基础存在；R5 需补 restCallSsrf/privateNetworkBlocked 场景测试。 | R5 |
| B-21 | Major | `src/backend/Atlas.AppHost/appsettings.Production.json:16-48` | Production 配置禁用 real HTTP/private network，限制 runtime steps/trace/logs。 | R2/R5 gate 需检查生产配置未被 mock/seed/internal-debug 破坏。 | R2/R5 |
| B-22 | Major | `src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionRuntimeModels.cs:72-129` | action executor / connector registry / connector 接口已抽象。 | R3 connector stub 可复用接口，但 server-action/workflow/document/ml/external 专用接口尚未落地。 | R3 |

### 3.3 后端后续修复计划

| 修复项 | 目标轮次 | 验收 |
|---|---|---|
| health 明确 `[AllowAnonymous]`，其余 microflow API 保持 `[Authorize]` | R2 | `MicroflowAuthorizationTests` |
| workspace ownership 覆盖 appId 资产反查 | R2 | `MicroflowAppAssetsControllerWorkspaceOwnershipTests` |
| production guard 拒 mock/seed/internal-debug 配置 | R2 | `MicroflowProductionGuardFilterTests` + P0 readiness verify |
| save baseVersion 409 envelope 与 clientRequestId 幂等 | R2 | `MicroflowSaveBaseVersionConflictTests` |
| rollback/cast/listOperation 真实 executor | R3 | executor 单测 + strict coverage verify |
| connector stub 与 capability registry | R3 | connector stub registry tests |
| trueParallel / inclusive gateway | R4 | gateway tests + verify scripts |
| expression API / editor / step debug API | R4 | API tests + frontend specs |
