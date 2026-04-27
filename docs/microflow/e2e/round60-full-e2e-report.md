# 第 60 轮 Microflow 全链路 E2E 回归报告

## 范围与结论口径

第 60 轮是内测准入回归，不新增 Runtime 语义、不扩展新 ActionExecutor、不改 FlowGram 协议。回归范围覆盖：

1. 前端 `app-web` 微流资源库、编辑器、HTTP adapter、错误态、ProblemPanel、DebugPanel、Publish/Versions/References 抽屉。
2. 后端 `api/microflows`、`api/microflow-metadata`、Validation、Publish、References、TestRun、Run/Trace/Cancel。
3. Runtime ExecutionPlanLoader、FlowNavigator、VariableStore、ExpressionEvaluator、MetadataResolver、EntityAccess Stub、TransactionManager、ActionExecutor、Loop、CallMicroflow、RestCall、LogMessage、ErrorHandling、Limits。
4. 数据库落库：MicroflowResource、SchemaSnapshot、Version、PublishSnapshot、Reference、RunSession、TraceFrame、RunLog、MetadataCache。

自动化入口：

```bash
npx tsx scripts/verify-microflow-round60-full-e2e.ts
```

证据目录：

```text
artifacts/microflow-e2e/round60/
```

## 当前质量基线

- 前端入口：`@atlas/mendix-studio-core` 统一导出微流资源、编辑器、adapter、config、contracts；`app-web` 注册 `/microflow` 与 `/microflow/:microflowId/editor`。
- Adapter：`app-web` 通过 `createAppMicroflowAdapterConfig` 读取 `VITE_MICROFLOW_ADAPTER_MODE` / `VITE_MICROFLOW_API_BASE_URL`，生产策略应为 `http` 且不允许 mock/local fallback。
- 后端路由：当前微流 API 实际为 `api/microflows`，Metadata 为 `api/microflow-metadata`，运行时 Metadata 诊断为 `api/microflows/runtime/metadata`；不是 `api/v1/microflows`。
- Envelope：前后端均使用 `MicroflowApiResponse<T>`，前端 `MicroflowApiClient` 解包后把 `data` 交给 adapter。
- 持久化：后端显式拒绝 FlowGram JSON，只保存 AuthoringSchema / Runtime DTO / Trace / Log。
- 现有资产：已存在 14 个根目录 `scripts/verify-microflow-*.ts`、1 个前端 Resource/Schema verify 脚本、`MicroflowBackend.http` Round 50/55/56/57/58 段和 39 份微流文档。

## 测试环境

- 后端：`Atlas.AppHost`，默认 `http://localhost:5002`。
- 前端：`src/frontend/apps/app-web`，默认 `http://localhost:5181`。
- 数据库：由 AppHost `Database__ConnectionString` 决定，默认 SqlSugar + SQLite。
- 测试前缀：`R60_E2E_` / `E2E_MF_`。
- 运行变量：`MICROFLOW_API_BASE_URL`、`MICROFLOW_WORKSPACE_ID`、`MICROFLOW_TENANT_ID`、`MICROFLOW_USER_ID`、`MICROFLOW_ROUND60_RESET`、`MICROFLOW_ROUND60_CLEANUP`。

## E2E 测试矩阵

| Case | 覆盖范围 | 自动化 |
| --- | --- | --- |
| R60-FE-ADAPTER-MODES | adapterMode、apiBaseUrl、bundle 边界 | `pnpm --dir src/frontend run verify:microflow-adapter-modes` |
| R60-FE-NO-PROD-MOCK | 生产路径禁 mock/local/fallback | `pnpm --dir src/frontend run verify:microflow-no-production-mock` |
| R60-FE-ERROR-HANDLING | 401/403/404/409/422/5xx/network 映射 | `pnpm --dir src/frontend run verify:microflow-http-error-handling` |
| R60-RESOURCE-SCHEMA | 资源库、Schema 加载/保存、重命名、收藏、复制、归档、恢复、删除 | `node src/frontend/scripts/verify-microflow-resource-schema-integration.mjs` |
| R60-METADATA-SELECTOR | Metadata catalog、selector 数据源、404 | `npx tsx scripts/verify-microflow-metadata-integration.ts` |
| R60-VALIDATION-PROBLEM | Validation modes、ProblemPanel issues、fieldPath | `npx tsx scripts/verify-microflow-validation-integration.ts` |
| R60-PUBLISH-VERSION-REFERENCES-TESTRUN | Publish、Version、Impact、References、TestRun、Debug、Trace、Cancel | `npx tsx scripts/verify-microflow-publish-version-references-testrun-debug-integration.ts` |
| R60-RUNTIME-PLAN | ExecutionPlanLoader | `npx tsx scripts/verify-microflow-execution-plan-loader.ts` |
| R60-FLOW-NAVIGATOR | FlowNavigator | `npx tsx scripts/verify-microflow-flow-navigator.ts` |
| R60-VARIABLE-STORE | VariableStore、variables snapshot | `npx tsx scripts/verify-microflow-variable-store.ts` |
| R60-EXPRESSION-EVALUATOR | Expression parse/type/eval/runtime integration | `npx tsx scripts/verify-microflow-expression-evaluator.ts` |
| R60-METADATA-RESOLVER-ENTITY-ACCESS | MetadataResolver、EntityAccess Stub | `npx tsx scripts/verify-microflow-metadata-resolver-entity-access.ts` |
| R60-TRANSACTION-MANAGER | TransactionManager、UnitOfWork、Run summary | `npx tsx scripts/verify-microflow-transaction-manager.ts` |
| R60-ACTION-EXECUTORS | ActionExecutor 全量覆盖 | `npx tsx scripts/verify-microflow-action-executors-full-coverage.ts` |
| R60-LOOP | Loop、Break、Continue | `npx tsx scripts/verify-microflow-loop-runtime.ts` |
| R60-CALLSTACK | CallMicroflow、CallStack、child trace | `npx tsx scripts/verify-microflow-callstack-runtime.ts` |
| R60-REST-LOG | RestCall、LogMessage、RuntimeLog | `npx tsx scripts/verify-microflow-restcall-logmessage-runtime.ts` |
| R60-ERROR-HANDLING | rollback/customWithRollback/customWithoutRollback/continue | `npx tsx scripts/verify-microflow-error-handling-runtime.ts` |
| R60-HARDENING | Cancel、maxSteps、maxIterations、REST security/timeout、trace/log 落库 | `npx tsx scripts/verify-microflow-runtime-hardening.ts` |

## Seed / Reset 策略

- 总控脚本启动时默认执行 reset + seed。
- Reset 只删除名称以 `R60_E2E_` 或 `E2E_MF_` 开头的资源。
- Seed 创建 blank、object CRUD、list、loop、callMicroflow、restCall、errorHandling、publish/reference、large graph 样例。
- Seed 输出：`artifacts/microflow-e2e/round60/seeded-resources.json`。
- 清理失败会写入 `seed-reset.log`，不吞掉错误。

## 覆盖结果记录方式

总控脚本会生成：

1. `e2e-summary.json`：结构化汇总、环境、commit、pass/fail/blocked 计数。
2. `e2e-summary.md`：人工可读摘要。
3. `failed-cases.json`：失败/阻塞用例。
4. `coverage-matrix.json`：完整矩阵与状态。
5. `traces/*.log`：每个 verify 子命令 stdout/stderr。
6. `runtime-run-sessions/`、`runtime-trace-samples/`、`http-responses/`：保留给后续更细证据采样。

## 当前缺口与风险

- 浏览器级 Playwright 微流专项用例尚未单独建立，Round60 总控以 HTTP verify + 前端静态/契约 verify 为主。
- 微流控制器当前 `[AllowAnonymous]`，401/403 更多依赖外层中间件或网关；本轮把权限态 UI 映射纳入前端验证，把真实鉴权策略列入内测风险。
- TestRun 仍是同步请求内执行；`cancel` 可更新已存在 RunSession 状态，但长时间运行中的协作取消不是完整异步 Job Queue 语义。
- `TraceWriter`、`RuntimeLogWriter`、`RunStateMachine`、`CancellationRegistry`、`RuntimeLimitsOptions` 没有逐字同名类型，职责分布在 `MicroflowMockRuntimeRunner`、`MicroflowTestRunService`、Run repository、HTTP client 与 options 中。

## 内测准入判定

准入门槛：

1. `verify-microflow-round60-full-e2e.ts` pass，且 Blocker=0、Critical=0。
2. `dotnet build` 通过。
3. `pnpm --dir src/frontend run build:app-web` 通过。
4. AppHost 与 app-web 可启动。
5. `artifacts/microflow-e2e/round60/e2e-summary.md` 无未解释 blocker/critical。

若上述任一项不满足，不建议进入第 61 轮生产准备；需先修复 blocker/critical，再重新生成本报告证据。
