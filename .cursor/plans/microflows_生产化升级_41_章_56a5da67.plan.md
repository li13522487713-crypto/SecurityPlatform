---
name: Microflows 生产化升级 41 章
overview: 把 Microflows 模块从演示级升级为生产级（41 章 + L5 全量 + trueParallel + Step Debug + Expression Editor），按 R1→R5 五轮顺序闭环交付。每一轮必须带"源码 + 后端测试 + 前端 spec + verify 脚本 + docs"，禁止只改文档或返回假成功。本计划假设你后续会把模式切到 agent 推进 R1，再用单独的 plan 推进 R2/R3/R4/R5。
todos:
  - id: R1-01-audit-frontend
    content: "R1: 前端源码审计（adapter/explorer/workbench/microflow-editor/property-panel/save-flow/run-debug），输出审计报告前端章节（含证据路径+行号）。"
    status: completed
  - id: R1-02-audit-backend
    content: "R1: 后端源码审计（controllers/services/runtime-engine/transactions/security/object-store/connector），输出审计报告后端章节。"
    status: completed
  - id: R1-03-audit-doc
    content: "R1: 综合 Blocker/Critical/Major/Minor 分类 + 阶段计划，输出 docs/microflow/production-upgrade-audit.md 终稿（30+ 证据点）。"
    status: completed
  - id: R1-04-matrix-collect-fe
    content: "R1: 编写 collector 采集前端 node-registry/action-registry/property-form-registry 全部 actionKind/category/支持级别。"
    status: completed
  - id: R1-05-matrix-collect-be
    content: "R1: 编写 collector 采集后端 MicroflowActionExecutorRegistry.BuiltInDescriptors 全部条目（≥80 actionKind）。"
    status: completed
  - id: R1-06-matrix-doc
    content: "R1: 输出 docs/microflow/production-node-capability-matrix.md（前后端三合一，全 18 字段）。"
    status: completed
  - id: R1-07-naming-doc
    content: "R1: 输出 action-kind-naming.md + action-descriptor-naming.md + executor-implementation-plan.md + 错误码对照表。"
    status: completed
  - id: R1-08-verify-matrix
    content: "R1: scripts/verify-microflow-node-capability-matrix.ts（前后端三方不一致即失败）+ 单测 + package.json microflow:verify:matrix。"
    status: completed
  - id: R1-09-verify-naming
    content: "R1: scripts/verify-microflow-action-descriptor-naming.ts（旧别名进 schema 即失败）+ 单测 + package.json microflow:verify:naming。"
    status: completed
  - id: R1-10-verify-coverage
    content: "R1: scripts/verify-microflow-executor-coverage.ts（actionKind 没 executor / supported 假成功 / connectorBacked 没 capability gate 即失败）+ 单测。"
    status: completed
  - id: R1-11-verify-gate-skeleton
    content: "R1: scripts/verify-microflow-production-gate.ts 骨架 + artifacts/microflow-production-gate/{json,md} 首版 + package.json microflow:verify:gate + R1 闭环验证。"
    status: completed
  - id: R2-01-authorize-controllers
    content: "R2: MicroflowApiControllerBase 全 [Authorize]；health 加 [AllowAnonymous]；MicroflowAuthorizationTests 测试。"
    status: completed
  - id: R2-02-workspace-ownership-query
    content: "R2: WorkspaceOwnershipFilter 覆盖 query workspaceId + X-Workspace-Id header + 测试。"
    status: completed
  - id: R2-03-workspace-ownership-route
    content: "R2: WorkspaceOwnershipFilter 覆盖 route id 反查 resource workspace + appId 资产查询 workspace 校验 + 测试。"
    status: completed
  - id: R2-04-production-guard
    content: "R2: ProductionGuardFilter 在 production 拒 mock/seed/internal-debug + RequireWorkspaceId + Rest.AllowRealHttp/AllowPrivateNetwork=false 默认 + appsettings.Production.json 硬化 + MicroflowProductionGuardFilterTests。"
    status: completed
  - id: R2-05-resource-baseversion
    content: "R2: MicroflowResourceService 强制 baseVersion 409 + 返回 remoteVersion/remoteUpdatedAt/remoteUpdatedBy/traceId + MicroflowSaveBaseVersionConflictTests。"
    status: completed
  - id: R2-06-resource-clientreqid
    content: "R2: MicroflowResourceService clientRequestId 幂等 + saveReason 落库 + 测试。"
    status: completed
  - id: R2-07-flowgram-purge
    content: "R2: 删除 FlowGram JSON 持久化（如有）+ AuthoringSchema 唯一持久化源 + publish snapshot immutable + 测试。"
    status: completed
  - id: R2-08-frontend-no-mock-build
    content: "R2: app-web microflow-adapter-config production 拒 mock/local/MSW + Rsbuild plugin 检测。"
    status: completed
  - id: R2-09-frontend-error-envelope
    content: "R2: API 错误 envelope 区分 401/403/404/409/422/500 + MicroflowApiError 统一类型 + 401 触发 atlas:microflow-unauthorized + spec。"
    status: completed
  - id: R2-10-frontend-conflict-modal
    content: "R2: 保存冲突弹窗 4 选项（Reload Remote / Keep Local / Force Save / Cancel）+ Force Save 二次确认 + spec。"
    status: completed
  - id: R2-11-frontend-procurement-purge
    content: "R2: 移除 SAMPLE_PROCUREMENT_APP fallback + 空 app/无权限 403/404 UI + spec。"
    status: completed
  - id: R2-12-frontend-tab-isolation
    content: "R2: Workbench 多 tab 独立 dirty/save/history + close/switch tab dirty guard + beforeunload + spec。"
    status: completed
  - id: R2-13-frontend-save-payload
    content: "R2: save 请求带 schemaId/baseVersion/version/clientRequestId/saveReason + spec。"
    status: completed
  - id: R2-14-verify-p0
    content: "R2: scripts/verify-microflow-production-no-mock.ts 收紧 production build artifact 扫描 + scripts/verify-microflow-p0-readiness.ts 全 P0 项检验 + R2 闭环验证。"
    status: completed
  - id: R3-01-executor-rollback
    content: "R3: RollbackObjectActionExecutor 真实实现（reverted/noop/invalidated 三态 + UnitOfWork 要求 + trace）+ MicroflowRollbackExecutorTests。"
    status: completed
  - id: R3-02-executor-cast
    content: "R3: CastObjectActionExecutor 真实实现（metadata inheritance + strict/allowNull + entity access）+ MicroflowCastExecutorTests。"
    status: completed
  - id: R3-03-executor-listop-sets
    content: "R3: ListOperationActionExecutor 集合类（union/intersect/subtract/equals/distinct）+ MicroflowListOperationSetExecutorTests。"
    status: completed
  - id: R3-04-executor-listop-scalars
    content: "R3: ListOperationActionExecutor 标量/位置类（contains/isEmpty/head/tail/find/first/last/reverse/size）+ MicroflowListOperationScalarExecutorTests。"
    status: completed
  - id: R3-05-descriptor-normalizer
    content: "R3: MicroflowActionDescriptorNormalizer（10+ 旧→canonical 映射：webserviceCall→webServiceCall / callExternal→callExternalAction / deleteExternal→deleteExternalObject / sendExternal→sendExternalObject / rollbackObject→rollback / castObject→cast / listUnion|listIntersect|listSubtract→listOperation / aggregate→aggregateList / filter→filterList / sort→sortList）+ 测试。"
    status: completed
  - id: R3-06-schema-migration
    content: "R3: MicroflowSchemaMigrationService（load 时 normalize / save canonical / publish snapshot canonical / 幂等 / 不丢字段 / MIGRATION_FAILED 阻断 publish）+ MicroflowSchemaMigrationServiceTests。"
    status: completed
  - id: R3-07-connector-stub-soap
    content: "R3: ISoapWebServiceConnector + IXmlMappingConnector 接口 + 默认空实现（capability=false）+ DI + 测试。"
    status: completed
  - id: R3-08-connector-stub-document
    content: "R3: IDocumentGenerationRuntime 接口 + 默认空实现 + DI + 测试。"
    status: completed
  - id: R3-09-connector-stub-workflow
    content: "R3: IWorkflowRuntimeClient 接口 + 默认空实现 + DI + 测试。"
    status: completed
  - id: R3-10-connector-stub-ml
    content: "R3: IMlRuntime 接口 + 默认空实现 + DI + 测试。"
    status: completed
  - id: R3-11-connector-stub-external
    content: "R3: IExternalActionConnector + IExternalObjectConnector 接口 + 默认空实现 + DI + 测试。"
    status: completed
  - id: R3-12-connector-stub-server-action
    content: "R3: IServerActionRuntime（callJavaAction）接口 + 默认空实现 + DI + capability registry 占位 + 测试。"
    status: completed
  - id: R3-13-runtime-abstr-scheduler
    content: "R3: IBranchScheduler / SequentialBranchScheduler（行为不变，为 R4 铺路）+ 测试。"
    status: completed
  - id: R3-14-runtime-abstr-uow-joinstore
    content: "R3: IBranchUnitOfWork + IGatewayJoinStateStore + 默认实现（行为不变）+ 测试。"
    status: completed
  - id: R3-15-variable-types
    content: "R3: RuntimeVariableValue / RuntimeObjectRef / RuntimeListValue / RuntimePrimitiveValue / RuntimeExternalObjectRef / RuntimeFileRef / RuntimeCommandValue + VariableScopeFrame + EntityTypeDescriptor + ListTypeDescriptor + executor 私有处理收敛 + 测试。"
    status: completed
  - id: R3-16-pp-rollback-cast
    content: "R3: 前端 property panel Rollback + Cast 表单（rollbackMode/failIfNotChanged/clearValidationErrors/sourceVariable/targetEntity/castMode/failOnInvalidType）+ spec。"
    status: completed
  - id: R3-17-pp-listop
    content: "R3: 前端 ListOperation 表单（动态字段切换 14 operation + outputType 自动推断）+ spec。"
    status: completed
  - id: R3-18-pp-aggregate-filter-sort
    content: "R3: 前端 Aggregate + Filter + Sort 表单（emptyListBehavior / itemVariable / expression / sortKeys）+ spec。"
    status: completed
  - id: R3-19-pp-list-create-change
    content: "R3: 前端 CreateList + ChangeList 表单（add/addAll/remove/removeAll/clear/set + allowDuplicates + mutateInPlace）+ spec。"
    status: completed
  - id: R3-20-pp-callmf-restcall
    content: "R3: 前端 CallMicroflow（target metadata + parameter mapping + return）+ RestCall 表单 + spec。"
    status: completed
  - id: R3-21-pp-webservice-external
    content: "R3: 前端 WebService + ExternalAction + ExternalObject 表单 + capability 状态显示（Required/Available/Missing）+ publish blocker 字段定位 + spec。"
    status: completed
  - id: R3-22-verify-strict
    content: "R3: verify-microflow-action-descriptor-naming.ts + verify-microflow-executor-coverage.ts 收紧失败条件 + R3 闭环验证。"
    status: completed
  - id: R4-GW-01-token-model
    content: "R4: GatewayToken + GatewayTokenSet + SplitInstanceId + ActivationSet 数据模型 + 测试。"
    status: completed
  - id: R4-GW-02-state-store
    content: "R4: GatewayRuntimeState 持久化（async run/debug/cancel 状态不丢，含 arrivedTokens/completedTokens/failedTokens/cancelledTokens/branchStates）+ 测试。"
    status: completed
  - id: R4-GW-03-parallel-split
    content: "R4: ParallelGatewaySplitExecutor + Task.WhenAll trueParallel branch scheduler（schedulerMode=trueParallel）+ 测试。"
    status: completed
  - id: R4-GW-04-parallel-join
    content: "R4: ParallelGatewayJoinExecutor 等待全分支 + sibling cancel 策略 + 测试。"
    status: completed
  - id: R4-GW-05-inclusive-split
    content: "R4: InclusiveGatewaySplitExecutor + activation set 计算 + otherwise 唯一 + INCLUSIVE_NO_BRANCH_SELECTED + 测试。"
    status: completed
  - id: R4-GW-06-inclusive-join
    content: "R4: InclusiveGatewayJoinExecutor 仅等 active branch + 测试。"
    status: completed
  - id: R4-GW-07-per-branch-uow
    content: "R4: per-branch UnitOfWork + commit/rollback semantics + 测试。"
    status: completed
  - id: R4-GW-08-write-conflict
    content: "R4: PARALLEL_VARIABLE_WRITE_CONFLICT + PARALLEL_WRITE_CONFLICT 检测（同 split 并发写同 variable/object/member）+ 测试。"
    status: completed
  - id: R4-GW-09-loop-cancel
    content: "R4: split / loop iter / callMicroflow 三层 token 隔离 + cancellationToken 全链路 + branch 失败默认取消 sibling + errorHandling continue 允许产生 handled token + 测试。"
    status: completed
  - id: R4-GW-10-validation-trace-verify
    content: "R4: validation（split outgoing≥2 / inclusive Boolean / otherwise 唯一 / loop 内 token / parallel 内不支持节点 publish 阻断）+ GatewayTraceWriter + scripts/verify-microflow-parallel-gateway.ts + verify-microflow-inclusive-gateway.ts。"
    status: completed
  - id: R4-EX-01-lexer
    content: "R4: MicroflowExpressionLexer（字面量/变量/属性/函数/算术/比较/逻辑/条件/字符串/日期/list/error/http）+ 测试。"
    status: completed
  - id: R4-EX-02-parser-ast
    content: "R4: MicroflowExpressionParser + Ast 模型 + 测试。"
    status: completed
  - id: R4-EX-03-typechecker
    content: "R4: MicroflowExpressionTypeChecker（变量/属性类型/函数签名/null-safe access/expectedType 验证）+ 测试。"
    status: completed
  - id: R4-EX-04-evaluator
    content: "R4: MicroflowExpressionEvaluator（白名单函数 + 禁 eval/Function/反射/动态 SQL）+ 测试。"
    status: completed
  - id: R4-EX-05-formatter
    content: "R4: MicroflowExpressionFormatter（保字符串 + 不改语义）+ 测试。"
    status: completed
  - id: R4-EX-06-completion
    content: "R4: MicroflowExpressionCompletionProvider（变量/属性/函数/enum/$latestError/$latestHttpResponse）+ 测试。"
    status: completed
  - id: R4-EX-07-diagnostics
    content: "R4: MicroflowExpressionDiagnosticsProvider + range/severity/code（含 blockPublish 级别）+ quick fix + 测试。"
    status: completed
  - id: R4-EX-08-preview
    content: "R4: MicroflowExpressionPreviewService（基于 sample context 预览，不写 runtime）+ 测试。"
    status: completed
  - id: R4-EX-09-api
    content: "R4: 6 个 expression API 端点（POST /api/v1/microflow-expressions/{parse,validate,infer-type,completions,format,preview}）+ 鉴权 + metadataVersion 校验 + 测试。"
    status: completed
  - id: R4-EX-10-frontend
    content: "R4: ExpressionEditor 前端组件（CodeMirror 6 lazy load）+ 前端 TypeScript port TypeChecker（与后端共享语义）+ 接入所有 expression 字段 + spec + verify-microflow-expression-language.ts + verify-microflow-expression-editor.ts。"
    status: completed
  - id: R4-DB-01-session-store
    content: "R4: DebugSessionStore + DebugSessionSweeper + 状态机（13 个状态：created/starting/running/pausing/paused/stepping/waitingAtJoin/completed/failed/cancelled/timedOut/expired）+ 测试。"
    status: completed
  - id: R4-DB-02-pause-points
    content: "R4: 安全暂停点 in MicroflowRuntimeEngine（startEvent / activity 前后 / decision 前后 / inclusive 前后 / loop iter 前后 / callMf 前后 / branch start / join 前后 / rest-webservice-external 前后 / errorHandler / endEvent）+ 测试。"
    status: completed
  - id: R4-DB-03-runtime-engine
    content: "R4: MicroflowDebugRuntimeEngine + DebugExecutionCoordinator + 协作式 pause（安全点 ack 后暂停）+ 测试。"
    status: completed
  - id: R4-DB-04-breakpoint-model
    content: "R4: BreakpointDescriptor + ConditionalBreakpointDescriptor（hit count + logpoint + suspendPolicy + scope: node/flow/expression/error/gatewayBranch + stale 标记）+ 测试。"
    status: completed
  - id: R4-DB-05-step-semantics
    content: "R4: stepOver / stepInto（callMicroflow 进子微流 + parallel 第一 active branch）/ stepOut / continue / pause / runToNode / runToCursor / cancel + suspendPolicy=all + 测试。"
    status: completed
  - id: R4-DB-06-variables-snapshot
    content: "R4: DebugVariableSnapshot（root parameters / scope variables / loop iterator / branch-local / $latestError / $latestHttpResponse）+ secret/token/password 脱敏 + 测试。"
    status: completed
  - id: R4-DB-07-callstack-branchframe
    content: "R4: DebugCallStackFrame（microflow call stack / parent-child run / loop / branch / errorHandler）+ DebugBranchFrame + 测试。"
    status: completed
  - id: R4-DB-08-watches
    content: "R4: DebugWatchExpression（共享 ExpressionEvaluator + watch error 不影响 runtime + type/value/error/durationMs）+ 测试。"
    status: completed
  - id: R4-DB-09-controller-api
    content: "R4: MicroflowDebugController + 7 个 API 端点（create / get / commands / variables / evaluate / trace / delete）+ 鉴权（[Authorize] + workspace/app/microflow 权限）+ session 数量限制 + payload size 限制 + 测试。"
    status: completed
  - id: R4-DB-10-fe-toolbar-marker
    content: "R4: Debug toolbar（Debug Run / Continue / Pause / Step Over / Step Into / Step Out / Run to Node / Cancel / Stop）+ 当前 execution marker（节点 + flow + branch 高亮）+ spec。"
    status: completed
  - id: R4-DB-11-fe-breakpoint
    content: "R4: Breakpoint gutter（节点左侧 + flow 上）+ conditional breakpoint 弹窗 + stale 灰色 + spec。"
    status: completed
  - id: R4-DB-12-fe-panels
    content: "R4: Variables panel + Watches panel + Call stack panel + Branch tree panel + Debug console（logpoint output + expression evaluation）+ Problems 联动 + spec。"
    status: completed
  - id: R4-DB-13-verify
    content: "R4: scripts/verify-microflow-step-debug.ts + verify-microflow-debug-api.ts + R4 闭环验证。"
    status: completed
  - id: R5-01-be-tests-1
    content: "R5: 后端补 entityAccessDenied + parallelWriteConflict + commitDryRunVsProductionMode 场景测试。"
    status: completed
  - id: R5-02-be-tests-2
    content: "R5: 后端补 debugSessionPermissionDenied + staleBreakpoint + restCallSsrf + privateNetworkBlocked 场景测试。"
    status: completed
  - id: R5-03-fe-spec-explorer-workbench
    content: "R5: AppExplorer + WorkbenchTabs 全量 spec（load/error/search/CRUD/reference blocked/dirty guard/close guard/switch guard）。"
    status: completed
  - id: R5-04-fe-spec-savequeue-property
    content: "R5: SaveQueue + PropertyPanel 全量 spec（autosave/conflict modal/per-node forms/metadata loading-error-stale）。"
    status: completed
  - id: R5-05-fe-spec-expression-debug
    content: "R5: ExpressionEditor + DebugUI 全量 spec（completions/diagnostics/expectedType/quick fix/preview + breakpoint/step/variables/watches/branch tree）。"
    status: completed
  - id: R5-06-canvas-roundtrip
    content: "R5: 画布 schema-roundtrip 测试（drag add / move / edge create-delete / decision branch labels / loop inner / copy-paste-duplicate-delete / undo-redo / auto-layout / save-reload 一致）。"
    status: completed
  - id: R5-07-e2e-create-edit-save
    content: "R5: playwright E2E 第一段：login → /space/:wsId/mendix-studio/:appId → app assets load → 创建 microflow → 拖节点 → 配置属性 → 保存。"
    status: pending
  - id: R5-08-e2e-publish-testrun
    content: "R5: playwright E2E 第二段：publish 阻断（validation error）→ 修复 → publish → testRun → trace 定位画布节点。"
    status: pending
  - id: R5-09-e2e-reference-debug
    content: "R5: playwright E2E 第三段：删除 microflow reference 阻断 + debug session stepOver 流程。"
    status: pending
  - id: R5-10-perf-baseline
    content: "R5: 100/300/500 节点性能基线（load/render/save/validate/run plan 采样）+ artifacts/microflow-performance/*.json + md 报告 + 超阈值 CI warning。"
    status: pending
  - id: R5-11-prod-build
    content: "R5: dotnet build Release（0 warning）+ frontend production build（rsbuild + i18n:check + lint）+ no mock/local/MSW 验证。"
    status: pending
  - id: R5-12-prod-gate-final
    content: "R5: scripts/verify-microflow-production-gate.ts 跑全量 Blocker(13)/Critical(8)/Major(5) + dotnet build Release + frontend production build + live health；输出 production-gate-summary.{json,md} 终版；docs/microflow/release/known-limitations.md 更新；结论 = go / conditional-go / no-go。"
    status: completed
  - id: R5-13-final-report
    content: "R5: 最终输出报告（修改文件清单 / 关键设计 / API 清单 / schema 字段 / 矩阵摘要 / Runtime 摘要 / 性能摘要 / 权限+生产配置摘要 / 测试与 verify 清单 / 已运行命令 / 未运行命令及原因 / 剩余风险 / 结论 go/conditional-go/no-go）。"
    status: pending
isProject: false
---

## 现状证据（已读源码，非凭空推测）

- 后端：`Atlas.Application.Microflows` 已有 ~120 文件，`MicroflowActionExecutorRegistry` 已用 Server/Connector/Command/Unsupported 四类区分；Retrieve/Create/ChangeMembers/Commit/Delete/CreateList/ChangeList/Aggregate/Filter/Sort/CreateVariable/ChangeVariable/Break/Continue/CallMicroflow/RestCall/LogMessage/ThrowException 已有真实 executor。
- **缺口**（来自 [MicroflowActionExecutorRegistry.cs:295-309](src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs)）：`rollback` / `cast` / `listOperation` 仍走 `ConfiguredMicroflowActionExecutor` 返回 success，注释明确写「待 P1 后续轮次补齐」。
- AppHost 已有 `MicroflowWorkspaceOwnershipFilter` / `MicroflowProductionGuardFilter` / `MicroflowApiExceptionFilter`；前端 app-explorer/workbench-tabs/microflow-resource/versions/publish/references/tree-crud 已成型。
- 已有 20 个 `verify-microflow-*.ts` 脚本、52 个 `docs/microflow/*` 文档（含 round60/round61 production-readiness 报告）、15 个 `.NET` 测试。
- **缺**：节点能力矩阵（前后端一致门禁）、Parallel/Inclusive Gateway 真实执行、Step Debug Session API、Expression Editor 真前后端共享 AST、production-gate 总闸脚本、schema migration normalizer。

## 全局策略

- **按 5 个 round 顺序交付**：每轮独立闭环（构建/测试/verify 全绿）；不允许"凑稿"。
- **Connector stub**：本轮新增 `IServerActionRuntime` / `IWorkflowRuntimeClient` / `IDocumentGenerationRuntime` / `IMlRuntime` / `IExternalActionConnector` / `IExternalObjectConnector` / `ISoapWebServiceConnector` / `IXmlMappingConnector` 接口 + DI 注册 + capability registry 占位；capability 缺失继续返 `RUNTIME_CONNECTOR_REQUIRED`，publish 阻断；不接真实连接器。
- **Gateway trueParallel**：R3 先把 `RuntimeExecutionContext` 拆出 `IBranchScheduler` / `IBranchUnitOfWork` 抽象（不改 runtime 行为）；R4 才加 `Task.WhenAll` 真实并发 + per-branch UnitOfWork + `PARALLEL_WRITE_CONFLICT` 检测。
- **本任务范围之外**（已与 L5 边界声明）：Async run job queue 取代当前同步 cancel、time-travel debug、debug-time variable mutation、true OS thread isolation、`branchOnly` suspend policy。这些超出 41 章硬性要求或仍在 Mendix 自身限制内，会在 release/known-limitations 中明确列出。

## 节点能力策略矩阵摘要（R1 落地）

- supported（真实 executor，已或将落）：startEvent / endEvent / decision / objectTypeDecision / merge / loop / break / continue / retrieve / createObject / changeMembers / commit / delete / **rollback** / **cast** / createList / changeList / **listOperation** / aggregateList / filterList / sortList / createVariable / changeVariable / callMicroflow / restCall / logMessage / throwException / errorEvent / annotation / annotationFlow / parallelGateway / inclusiveGateway（R4 后）
- connectorBacked（capability 缺失即阻断；本轮接口 stub）：webServiceCall / restOperationCall / callJavaAction / callExternalAction / deleteExternalObject / sendExternalObject / generateDocument / mlModelCall / callWorkflow / completeUserTask / changeWorkflowState / applyJumpToOption / retrieveWorkflowContext / importXml / exportXml
- runtimeCommand（服务端只产 command preview）：showPage / showHomePage / showMessage / closePage / validationFeedback / downloadFile
- explicitUnsupported（永久 nanoflow/client only）：callJavaScriptAction / callNanoflow / synchronize

---

## R1 — 审计 + 节点矩阵 + 生产门禁骨架（章节 2、3、14、17 命名头部）

**目标**：建立"前后端一致性"和"生产门禁"两套机器可读门禁，作为后续 R2-R5 的锚点。

- 新增 [docs/microflow/production-upgrade-audit.md](docs/microflow/production-upgrade-audit.md)：含 30+ 证据点（每条带源码路径 + 行号），Blocker/Critical/Major/Minor 分类，每条给出修复方案 + 测试缺口 + 是否影响生产发布。
- 新增 [docs/microflow/production-node-capability-matrix.md](docs/microflow/production-node-capability-matrix.md)：覆盖 ≥ 80 个 actionKind，字段 = category / frontend registry key / Mendix semantic name / schema kind / actionKind / toolbox visible / property panel form path / validation support / runtime executor / runtime support level / transaction behavior / variable output behavior / error handling / trace behavior / known limitations / production decision。
- 新增 [docs/microflow/contracts/action-kind-naming.md](docs/microflow/contracts/action-kind-naming.md)、[docs/microflow/contracts/action-descriptor-naming.md](docs/microflow/contracts/action-descriptor-naming.md)、[docs/microflow/executor-implementation-plan.md](docs/microflow/executor-implementation-plan.md)。
- 新增 verify 脚本：
  - [scripts/verify-microflow-node-capability-matrix.ts](scripts/verify-microflow-node-capability-matrix.ts)：读前端 `node-registry/registry.ts` + `action-registry.ts` + `MicroflowActionExecutorRegistry.BuiltInDescriptors` + 矩阵 md，三方不一致即失败。
  - [scripts/verify-microflow-action-descriptor-naming.ts](scripts/verify-microflow-action-descriptor-naming.ts)：发现 `webserviceCall`/`webService`/`callExternal`/`externalCall`/`deleteExternal`/`sendExternal`/`rollbackObject`/`castObject`/`listUnion`/`listIntersect`/`listSubtract` 等旧别名进入 schema 即失败（仅允许出现在 normalizer migration map）。
  - [scripts/verify-microflow-executor-coverage.ts](scripts/verify-microflow-executor-coverage.ts)：actionKind 没 executor / supported 没真实 executor / unsupported 返回 success / connectorBacked 没 capability gate 即失败。
  - [scripts/verify-microflow-production-gate.ts](scripts/verify-microflow-production-gate.ts)：聚合脚本，跑 git commit + backend Release build + frontend production build + 静态检查 + live health + runtime matrix + security checks + performance summary，输出 [artifacts/microflow-production-gate/production-gate-summary.json](artifacts/microflow-production-gate/production-gate-summary.json) 和 .md（首版含 R1 已检测项；其余项标 pending-future-round）。
- 更新 `package.json` scripts：`microflow:verify:matrix` / `microflow:verify:naming` / `microflow:verify:coverage` / `microflow:verify:gate`。

**R1 闭环**：node-capability-matrix verify、descriptor-naming verify、executor-coverage verify 全绿；production-gate 首版 conditional-go（pending future rounds）。

---

## R2 — P0 阻断项（章节 4、5、9、11）

**目标**：清干净 mock/local/sample fallback、API v1 路径、[Authorize]、workspace ownership、appId 资产树、schema save baseVersion 409、FlowGram JSON 不持久化、unsupported 不假成功。

- 后端：
  - [src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowApiControllerBase.cs](src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowApiControllerBase.cs) 全部 `[Authorize]` 验证；缺 health controller 加 `[AllowAnonymous]` 单独标注。
  - [src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowWorkspaceOwnershipFilter.cs](src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowWorkspaceOwnershipFilter.cs) 覆盖 query workspaceId / `X-Workspace-Id` header / route id 反查 resource workspace / appId 资产查询 workspace 校验。
  - [src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowProductionGuardFilter.cs](src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowProductionGuardFilter.cs) 在 `appsettings.Production.json` 拒绝 mock/seed/internal-debug；`RequireWorkspaceId=true`；`Rest.AllowRealHttp=false`、`Rest.AllowPrivateNetwork=false` 默认。
  - [src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs](src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs) 强制 `baseVersion` 409 冲突检测 + `clientRequestId` 幂等 + 返回 `remoteVersion`/`remoteUpdatedAt`/`remoteUpdatedBy`/`traceId`。
  - 删除存在的 FlowGram JSON 持久化路径（如有），强制 AuthoringSchema 唯一持久化源。
- 前端：
  - [src/frontend/apps/app-web/src/app/microflow-adapter-config.ts](src/frontend/apps/app-web/src/app/microflow-adapter-config.ts) production 模式拒绝 mock/local/MSW（构建期 Rsbuild plugin 检测）。
  - [src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/](src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http) 区分 401/403/404/409/422/500 → 统一 `MicroflowApiError` envelope；401 触发 `atlas:microflow-unauthorized`。
  - [src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerContainer.tsx](src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer/AppExplorerContainer.tsx) 移除 `SAMPLE_PROCUREMENT_APP` fallback；空 app/无权限直接 403/404 UI。
  - [src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-tabs.tsx](src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-tabs.tsx) 多 tab 独立 dirty/save/history。
  - 保存请求带 `schemaId`/`baseVersion`/`version`/`clientRequestId`/`saveReason`；冲突弹窗支持 Reload Remote / Keep Local / Force Save / Cancel（Force Save 二次确认）。
- 测试：`MicroflowResourceTenantScopeTests` 增强 + 新增 `MicroflowSaveBaseVersionConflictTests` / `MicroflowProductionGuardFilterTests` / `MicroflowAppAssetsControllerWorkspaceOwnershipTests` / `MicroflowAuthorizationTests`；前端 `app-explorer.spec.tsx` / `workbench-tabs-lifecycle.spec.ts` 新增 401/403/409 路径。
- 脚本：[scripts/verify-microflow-production-no-mock.ts](scripts/verify-microflow-production-no-mock.ts) 收紧到 production build artifact 扫描；[scripts/verify-microflow-p0-readiness.ts](scripts/verify-microflow-p0-readiness.ts) 全 P0 项检验。

**R2 闭环**：上述测试 + verify 全绿；production-gate Blocker 为 0。

---

## R3 — 真实 Executor + 命名整治 + Schema migration + Property Panel（章节 17-36）

**目标**：把 Rollback/Cast/ListOperation 三个仍 stub 的节点改为真实 executor；统一 actionKind/descriptor 命名；前端属性面板按节点真实补齐。

- 真实 Executor（替换 [MicroflowActionExecutorRegistry.cs:295-309](src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs) 的 `ConfiguredMicroflowActionExecutor`）：
  - `RollbackObjectActionExecutor`：按提示二十节字段 + 语义；支持 reverted/noop/invalidated 三态；productionRun 强制要 UnitOfWork（否则 `TRANSACTION_REQUIRED`）。
  - `CastObjectActionExecutor`：按二十一节；通过 `IMicroflowRuntimeObjectMetadataService` 检 inheritance；strict/allowNull 两模式；entity access 校验。
  - `ListOperationActionExecutor`：按二十二节；`union`/`intersect`/`subtract`/`contains`/`equals`/`isEmpty`/`head`/`tail`/`find`/`first`/`last`/`distinct`/`reverse`/`size` 14 个 operation；不修改输入列表。
- 命名整治（按章节十九）：
  - 新增 `MicroflowActionDescriptorNormalizer`（旧→canonical：`webserviceCall→webServiceCall` / `callExternal→callExternalAction` / `deleteExternal→deleteExternalObject` / `sendExternal→sendExternalObject` / `rollbackObject→rollback` / `castObject→cast` / `listUnion|listIntersect|listSubtract→listOperation` / `aggregate→aggregateList` / `filter→filterList` / `sort→sortList`）。
  - 新增 `MicroflowSchemaMigrationService`：load 时 normalize；save 时 canonical-only；publish snapshot canonical；migration 幂等 + 不允许丢字段。
- Connector stub 注册（按二十六、二十九节）：新增 `IServerActionRuntime` / `ISoapWebServiceConnector` / `IDocumentGenerationRuntime` / `IWorkflowRuntimeClient` / `IMlRuntime` / `IExternalActionConnector` / `IExternalObjectConnector` / `IXmlMappingConnector` 接口 + 默认空实现（capability=false）+ DI 注册；capability 缺失时仍返 `RUNTIME_CONNECTOR_REQUIRED`。
- Runtime Engine 抽象拆分（为 R4 Parallel 铺路）：
  - 新增 `IBranchScheduler` / `IBranchUnitOfWork` / `IGatewayJoinStateStore` 抽象；当前 RuntimeEngine 走 `SequentialBranchScheduler`，行为不变。
  - 变量类型系统按三十一节：`RuntimeVariableValue` / `RuntimeObjectRef` / `RuntimeListValue` / `RuntimePrimitiveValue` / `RuntimeExternalObjectRef` / `RuntimeFileRef` / `RuntimeCommandValue` / `VariableScopeFrame` / `EntityTypeDescriptor` / `ListTypeDescriptor` 收敛各 executor 私有处理。
- 前端属性面板（章节三十四）：补齐 Rollback / Cast / ListOperation / AggregateList / FilterList / SortList / CreateList / ChangeList / WebService / ExternalAction / ExternalObject 表单，文件落 [src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/](src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms)。每表单走 `node-form-registry.ts` 注册。
- 测试：`MicroflowRollbackExecutorTests` / `MicroflowCastExecutorTests` / `MicroflowListOperationExecutorTests` / `MicroflowSchemaMigrationServiceTests` / `MicroflowActionDescriptorNormalizerTests` / `MicroflowConnectorStubRegistryTests` + 前端 property-panel forms `*.spec.tsx`。
- 脚本：`verify-microflow-action-descriptor-naming.ts`/`verify-microflow-executor-coverage.ts` 收紧失败条件。

**R3 闭环**：所有 supported 节点真实 executor；旧别名只在 normalizer 出现；property panel forms reload 不丢字段。

---

## R4 — Parallel/Inclusive Gateway + Expression Editor + Step Debug（章节 37-39）

**目标**：上 trueParallel + 表达式前后端共享 AST/TypeChecker/Evaluator + Step Debug Session API & UI。

- Parallel/Inclusive Gateway（章节三十七）：
  - `ParallelGatewayExecutor` / `InclusiveGatewayExecutor` / `GatewayToken` / `GatewayTokenSet` / `BranchExecutionContext` / `JoinExecutionContext` / `ParallelBranchScheduler`（`Task.WhenAll`）/ `InclusiveBranchScheduler` / `GatewayJoinStateStore` / `GatewayExecutionPlanner` / `GatewayTraceWriter`。
  - splitInstanceId / loopIterationId / callStackFrameId 三层隔离 token；inclusive activation set 持久化到 `GatewayRuntimeState`。
  - per-branch UnitOfWork；同 split 内并发写同一 variable/object/member → `PARALLEL_VARIABLE_WRITE_CONFLICT` / `PARALLEL_WRITE_CONFLICT`。
  - cancellationToken 全链路；任一 branch 失败默认取消 sibling；errorHandling continue 允许产生 handled token。
  - validation：split outgoing ≥2、inclusive condition Boolean、otherwise 唯一、loop 内 token 隔离、parallel 内不支持节点 publish 阻断。
- Expression Editor（章节三十八）：
  - 后端：`MicroflowExpressionLexer`/`Parser`/`Ast`/`TypeChecker`/`Evaluator`/`Formatter`/`CompletionProvider`/`DiagnosticsProvider`/`PreviewService`；新增 `POST /api/v1/microflow-expressions/{parse,validate,infer-type,completions,format,preview}`。
  - 前端：`mendix-microflow/src/expression-editor/`（CodeMirror 6 lazy load 或 Monaco）；语法高亮 + autocomplete + hover + diagnostics + expectedType + quick fix + preview + format。
  - 接入字段（按三十八节 D）：decision.expression、objectTypeDecision、endEvent.returnValue、createVariable / changeVariable、retrieve.constraint、createObject / changeMembers member changes、filterList / sortList / listOperation / aggregateList / callMicroflow params / restCall url / webService request / externalAction input / logMessage / throwException / errorHandler condition / inclusive outgoing condition / debug conditional breakpoint。
  - 禁 eval/Function/反射；白名单函数注册；前后端 evaluator 共享语义（前端引擎是后端 TypeChecker 的 TypeScript port，验收脚本 `verify-microflow-expression-language.ts` 比对一致）。
- Step Debug（章节三十九）：
  - 后端：`MicroflowDebugSession` / `MicroflowDebugController` / `MicroflowDebugRuntimeEngine` / `DebugExecutionCoordinator` / `BreakpointDescriptor` / `ConditionalBreakpointDescriptor` / `DebugCommand` / `DebugVariableSnapshot` / `DebugCallStackFrame` / `DebugBranchFrame` / `DebugWatchExpression` / `DebugSessionStore` / `DebugSessionSweeper`。
  - API：`POST /api/v1/microflows/{microflowId}/debug-sessions`、`GET /api/v1/microflows/debug-sessions/{id}`、`POST .../commands`、`GET .../variables`、`POST .../evaluate`、`GET .../trace`、`DELETE .../`。
  - safe pause point：startEvent / 每 activity 前后 / decision 前后 / inclusive 前后 / loop iter 前后 / callMicroflow 前后 / branch start / join 前后 / rest/webservice/external 前后 / errorHandler / endEvent。
  - step 语义：stopOnStart / stepOver / stepInto / stepOut / continue / pause / runToNode / runToCursor / cancel；suspendPolicy=all（默认）；branchOnly 第一版 UI 禁用并提示。
  - 前端：[src/frontend/packages/mendix/mendix-microflow/src/debug/](src/frontend/packages/mendix/mendix-microflow/src/debug) 全量 UI（toolbar + breakpoint gutter + execution marker + variables + watches + call stack + branch tree + trace + debug console + Problems 联动）。
  - 安全：[Authorize] + workspace/app/microflow 权限 + session timeout + payload size 限制 + secret/token 脱敏。
- 测试：每个能力 .NET 测试 + 前端 spec + verify 脚本（`verify-microflow-expression-language.ts` / `verify-microflow-expression-editor.ts` / `verify-microflow-step-debug.ts` / `verify-microflow-debug-api.ts` / `verify-microflow-parallel-gateway.ts` / `verify-microflow-inclusive-gateway.ts`）。

**R4 闭环**：parallel/inclusive 真实 trueParallel；表达式前后端语义一致 verify 通过；debug stepOver/stepInto/stepOut/breakpoint/watch 全绿。

---

## R5 — 测试体系 + E2E + 性能基线 + Production Gate 收尾（章节 13、14）

**目标**：补齐缺口测试、跑 E2E、跑 100/300/500 性能基线、最终 production gate 出 go/conditional-go/no-go。

- 后端测试：补齐 entityAccessDenied / parallelWriteConflict / debugSessionPermissionDenied / staleBreakpoint / restCallSsrf / privateNetworkBlocked / commitDryRunVsProductionMode 等场景。
- 前端测试：AppExplorer / WorkbenchTabs / SaveQueue / PropertyPanel / ExpressionEditor / DebugUI 全量 spec。
- 画布测试：drag / move / edge / loop inner / copy-paste / duplicate / undo-redo / auto-layout / save-reload schema-roundtrip / 100/300/500 节点性能日志。
- 浏览器 E2E（playwright）：login → `/space/:wsId/mendix-studio/:appId` → app assets load → 创建 microflow → 拖节点 → 配置属性 → 保存 → publish 阻断 → 修复 → publish → testRun → trace 定位画布节点 → 删除 reference 阻断 → debug session stepOver。
- 性能：[artifacts/microflow-performance/](artifacts/microflow-performance) 含 100/300/500 节点 load/render/save/validate 采样 JSON + md 报告；超阈值 CI warning。
- Production Readiness：no mock/local/MSW + production config safety + health/storage/metadata/runtime + dotnet build Release + frontend production build。
- Production Gate 终版：`scripts/verify-microflow-production-gate.ts` 跑全量 Blocker（13 项）/Critical（8 项）/Major（5 项）+ live health；输出 [artifacts/microflow-production-gate/production-gate-summary.json](artifacts/microflow-production-gate/production-gate-summary.json) 和 .md 终版；结论 = go / conditional-go / no-go。

**R5 闭环**：production gate 输出明确结论；剩余 Major/已知限制写入 [docs/microflow/release/known-limitations.md](docs/microflow/release/known-limitations.md)。

---

## 执行模式

- 本计划获你确认后，每一轮独立切到 agent 模式推进；每轮结束我会用单独的 plan 重对齐下一轮范围。
- 每轮交付物固定四件：源码 + 后端测试 + 前端 spec + verify 脚本（可选 + 文档）。任一缺失视为未闭环。
- 中途若发现你新约束（如 R3 时你要求改优先级），随时停下来对 plan。

---

## 范围外（明确写入已知限制）

- Async run job queue 完整版（替代当前同步 cancel 限制）。
- Time-travel debug、debug-time variable mutation、true OS thread isolation、`branchOnly` suspend policy。
- Connector 真实接入（除非后续单独追加）。
- 浏览器跨域多源 OAuth、第三方 IdP 联调。

这些会进 [docs/microflow/release/known-limitations.md](docs/microflow/release/known-limitations.md) 并在 production-gate 标 Major 风险接受。
