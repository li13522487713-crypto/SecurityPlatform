# M7 立项评估：存量 `WorkflowV2*` 全量重命名

## 1. 立项背景

`docs/contracts.md` 第 157-164 行「M1 新增 DTO 命名规范」段落已明确承诺：

> 仓库中已存量的 `WorkflowV2*`（控制器 `WorkflowV2Controller`、服务接口 `IWorkflowV2QueryService` / `IWorkflowV2CommandService` / `IWorkflowV2ExecutionService`、路径 `api/v2/workflows`、模型 `WorkflowV2*Dto` / `WorkflowV2*Request`）作为遗留命名保留，不在本里程碑批量重命名（影响 200+ 文件 + 既有 e2e + 契约 + .http，必须独立里程碑评估）；后续 M7+ 由独立 issue 统一重命名为正式名称。

本文件作为 M7 立项前置评估，不在本里程碑实施任何代码改动。M1 已完成的 3 个对外 DTO（`WorkflowVariableTreeDto` / `WorkflowNodeExecutionHistoryDto` / `WorkflowHistorySchemaDto`）只是局部规范；存量 `WorkflowV2*` 标识符仍广泛存在于后端类型、HTTP 路由、前端 IDL、E2E、文档、`.http` 与能力路由中。

## 2. 范围清单（按发现位置归类）

### 2.1 后端类型 / 服务 / 控制器（命名直接含 `WorkflowV2`）

- `Atlas.PlatformHost.Controllers.WorkflowV2Controller`、`Atlas.AppHost.Controllers.WorkflowV2Controller`
- `IWorkflowV2QueryService` / `IWorkflowV2CommandService` / `IWorkflowV2ExecutionService` 及 3 个 `WorkflowV2*Service` 实现
- `WorkflowV2Models.cs`：`WorkflowV2CreateRequest` / `WorkflowV2ListItem` / `WorkflowV2DetailDto` / `WorkflowV2RunTraceDto` / `WorkflowV2DependencyDto` / `WorkflowV2RunRequest` / `WorkflowV2ResumeRequest` / `WorkflowV2SaveDraftRequest` / `WorkflowV2UpdateMetaRequest` / `WorkflowV2PublishRequest` / `WorkflowV2NodeDebugRequest`
- `WorkflowV2Validators.cs`：`WorkflowV2CreateRequestValidator` / `WorkflowV2SaveDraftRequestValidator` / `WorkflowV2UpdateMetaRequestValidator` / `WorkflowV2PublishRequestValidator` / `WorkflowV2RunRequestValidator` / `WorkflowV2NodeDebugRequestValidator` / `WorkflowWorkbenchExecuteRequestValidator`
- 文件名含 `WorkflowV2` 但内部类型不含：`IWorkflowV2Repositories.cs`、`WorkflowV2Enums.cs`
- 私有方法名：`AppMigrationService.PurgeTenantWorkflowV2InAppAsync`
- 历史兼容层基类：`Atlas.Presentation.Shared.Controllers.Ai.CozeWorkflowCompatControllerBase`（现已删除，相关 DTO 已拆出共享 contracts）
- 历史兼容层薄封装：PlatformHost 与 AppHost 的 `CozeWorkflowCompatController.cs`（现已删除）

### 2.2 引用方但类名本身不含 `WorkflowV2`

- 控制器：`AiVariablesController`（PlatformHost + AppHost）、`AiAppsController`、`WorkflowWorkbenchController`、`LowCodeActionsController`
- 接口：`IOpenWorkflowService`、`IReferenceGraphService`
- 平台服务：`WorkspacePortalService`、`WorkspaceIdeService`、`RuntimeExecutionCommandService`

### 2.3 资源 / i18n key

- `Messages.zh-CN.resx` / `Messages.en-US.resx` 当前命中：`WorkflowV2ResumeInvalidState`
- 本里程碑（Validator 修复）将新增：`WorkflowV2NameFormat` / `WorkflowV2NameLength`
- M7 需统一替换 key 命名（建议去掉 `V2` 前缀或对齐新名）

### 2.4 路由族

- 核心 REST：`api/v2/workflows`（创建、详情、列表、保存草稿、更新元数据、发布、版本、依赖、版本 diff、运行、节点调试 等）
- developer 兜底（注意大小写）：`/api/workflowV2/{**path}`（`DeveloperWorkflowV2PostFallback` / `DeveloperWorkflowV2GetFallback`）
- 能力路由：`/apps/{appId}/workflow-v2`（见 `CapabilityRegistryTests`）
- 当前状态更新：`/api/v2/workflows*` 已在现行代码中删除，`rsbuild.config.ts` 与 `WorkspaceIdeService` 也已不再指向该路由
- 当前主链路：编辑态 `api/app-web/workflow-sdk/*`，运行态 `api/runtime/workflows/{id}:invoke*`
- 工具：`scripts/export-workflow-golden-sample.ps1`（金样本导出脚本）

### 2.5 测试

- `tests/Atlas.SecurityPlatform.Tests/Services/WorkflowV2ExecutionServiceTests.cs`
- `tests/Atlas.SecurityPlatform.Tests/Integration/WorkflowV2IntegrationTests.cs`
- `tests/Atlas.SecurityPlatform.Tests/Integration/WorkflowV2ApiContractTests.cs`
- `tests/Atlas.SecurityPlatform.Tests/Validators/WorkflowV2CreateRequestValidatorTests.cs`（本里程碑配套补 `WorkflowV2UpdateMetaRequestValidatorTests.cs`）
- `tests/Atlas.SecurityPlatform.Tests/Services/WorkflowDtoMappingTests.cs`
- `tests/Atlas.SecurityPlatform.Tests/Services/CapabilityRegistryTests.cs`（含路由模板 `/apps/{appId}/workflow-v2`）

### 2.6 前端 IDL（自动生成）

- `src/frontend/packages/arch/idl/workflow_api/namespaces/workflow.ts`、`src/frontend/packages/arch/idl/workflow_api/index.ts`：含 `WorkflowV2`、`CreateWorkflowV2Request`、`PublishWorkflowV2`、`QueryWorkflowV2` 等
- `src/frontend/packages/arch/idl/developer_api/namespaces/developer_api.ts`、`developer_api/index.ts`：`genBaseURL('/api/workflowV2/...')` 等
- `src/frontend/packages/arch/api-schema/marketplace/public_api.ts`、`product_api/public_api.ts`、`marketplace_operation/public_api.ts`：注释含 `workflowV2`
- `src/frontend/packages/arch/bot-space-api/src/index.ts`、`__tests__/index.test.ts`

### 2.7 E2E

- `src/frontend/e2e/app/workflow-v2-acceptance.spec.ts`（文件名含 `workflow-v2`）
- `src/frontend/e2e/app/e2e-all.ordered.spec.ts`（引用上文件）
- 内容含 `/api/v2/workflows` 但文件名不含 V2：`console-workspace-workflow.smoke.spec.ts`、`workflow-complete-flow.spec.ts`、`workflow-publish.spec.ts`、`workflow-editor.spec.ts`、`workflow-run.spec.ts`

### 2.8 文档

- `docs/contracts.md`（多个章节：Workflow V2 API、`WorkflowV2DependencyDto` 等）
- `docs/coze-api-gap.md`（多处 `IWorkflowV2*`、`WorkflowV2CommandService`、`workflow_v2` 开关描述）
- `docs/coze-workflow-migration.md`（`workflowV2Api`）
- `docs/workflow-editor-validation-matrix.md`（现已改标为历史 v2 契约说明）
- `docs/plan-coze-atlas-round2.md`（列举 `WorkflowV2Controller`）
- `AGENTS.md`（「Workflow V2（Coze 复刻）补充约束」）
- `CLAUDE.md`（「Workflow V2 (Coze Parity) Update」）

### 2.9 `.http` 测试样例

- `src/backend/Atlas.PlatformHost/Bosch.http/Workflows-V2.http`
- `src/backend/Atlas.AppHost/Bosch.http/Workflows-V2.http`
- 历史 compat `.http`：`src/backend/Atlas.PlatformHost/Bosch.http/CozeWorkflowCompat.http`（现已删除）

### 2.10 数据库 / Hangfire / 持久化

- 当前 **未发现** `[SugarTable("workflow_v2*")]` 声明，工作流数据落在 `WorkflowMeta` / `WorkflowDraft` / `WorkflowVersion` / `WorkflowExecution` / `WorkflowNodeExecution` 等无 V2 字样的实体上 → **类型重命名不需要数据库迁移脚本**。
- 当前 **未发现** 含 `WorkflowV2` 的 Hangfire JobId（`WorkflowExecutionCleanupJob` 的 DisplayName 不含 V2）→ **在途任务无影响**。
- 当前 **未检出** `localStorage` / `sessionStorage` 含 `workflowV2` 的前端持久化 key → **客户端缓存无破坏性影响**。

## 3. 命名映射草案（供 M7 立项决策）

### 3.1 候选映射

- **候选 A（推荐）：`WorkflowV2*` → `DagWorkflow*` / `CozeDagWorkflow*`**
  - 与 `Atlas.Domain.Workflow`（旧 WorkflowCore，含 `PersistedWorkflow`、`IWorkflowQueryService`）零冲突
  - 语义清晰，强调 DAG 引擎来源
  - 需评估：是否在「Coze 兼容层 + 自研 DAG」二选一里取「DagWorkflow」更中性
- **候选 B：`WorkflowV2*` → `Workflow*`**
  - 最短，但与现有 `Atlas.Domain.Workflow`、`api/v1/workflows`（`WorkflowController`）严重冲突
  - 风险：命名歧义放大，Maintainer 无法快速区分两套模型
- **候选 C：`WorkflowV2*` → `CanvasWorkflow*` / `WorkflowDesign*`**
  - 强调编辑器画布属性
  - 风险：与「设计态」其它概念可能重叠

### 3.2 路由命名策略（与类型命名独立）

- **策略 1（推荐）：保留 `api/v2/workflows` 作为 API 版本号**
  - `v2` 释义为 HTTP API 版本号（与 `v1`、未来 `v3` 平行），不再绑定「产品 V2」语义
  - 文档需明确「`v2` 不再代表产品迭代版本，仅是 API 路由版本」
  - 改动面最小，不破坏现有客户端
- **策略 2：路由迁移到 `api/v3/workflows` 或 `api/dag-workflows`**
  - 与类型重命名同步，但破坏性大
  - 必须配双路由并行 + 弃用窗口（参见 AGENTS.md「弃用流程」）
- **策略 3：路由保留 `api/v2/workflows`，但兜底 `/api/workflowV2/{**path}` 改为 `/api/dagWorkflow/{**path}`**
  - 折中方案，仅清理大小写不一致的 developer 兜底

### 3.3 命名冲突已知项

- `Atlas.Domain.Workflow` / `Atlas.Application.Workflow`：旧 WorkflowCore（`PersistedWorkflow`、`IWorkflowQueryService` 等）已占用 `Workflow` 命名空间
- `api/v1/workflows`（`WorkflowController`）、`api/v1/ai-workflows`（`AiWorkflowsController`）、`api/v2/workflows`（`WorkflowV2Controller`）三者并行
- M7 必须输出全局路由命名表，避免同名跨版本歧义

## 4. 子里程碑拆分

### M7-A：后端类型层重命名（仅 `.cs`，不动路由）

- 范围：`WorkflowV2Controller`（类名）、`IWorkflowV2*Service` 接口、`WorkflowV2*Service` 实现、`WorkflowV2Models.cs` 全部 record、`WorkflowV2Validators.cs` 全部 Validator、`WorkflowV2Enums.cs` / `IWorkflowV2Repositories.cs` 文件重命名、`AppMigrationService.PurgeTenantWorkflowV2InAppAsync`
- 所有引用方同步替换：`AiVariablesController` / `AiAppsController` / `WorkflowWorkbenchController` / `IOpenWorkflowService` / `IReferenceGraphService` / `WorkspacePortalService` / `WorkspaceIdeService` / `RuntimeExecutionCommandService` / `LowCodeActionsController` / `CozeWorkflowCompatControllerBase`
- 资源 key：`WorkflowV2ResumeInvalidState` / `WorkflowV2NameFormat` / `WorkflowV2NameLength` 改名（同时改 `ApiResponseLocalizer.T(..., "...")` 调用方）
- 路由 / `.http` / E2E / 文档 / IDL：**不动**
- 验证：`dotnet build` 0 警告 + 全量 `dotnet test`（不含 Integration） + `dotnet test --filter "Integration"`

### M7-B：路由 + 资源策略落地

- 决策路由策略（3.2 三选一）
- 若选策略 2/3：补双路由 + 弃用窗口；同步更新 `rsbuild.config.ts` proxy、`scripts/export-workflow-golden-sample.ps1`、`WorkspaceIdeService` 拼接 URL、`LowCodeActionsController` 拼接 URL、`PlatformHost` / `AppHost` 各自 `Workflows-V2.http` 重命名为 `Workflows.http` 或保留双份
- 同步能力路由：`/apps/{appId}/workflow-v2` 是否改名（要评估 `Capability` 数据迁移：若生产 DB 中有存量能力行，需脚本）
- 验证：`dotnet build` + 全量 `dotnet test` + `cd src/frontend && pnpm run build && pnpm run test:e2e:app`

### M7-C：前端 IDL + 生成物

- 评估 `packages/arch/idl` 是否由 codegen 管线生成；若是，调整生成模板/IDL 源；若否，手工替换
- 替换面：`workflow_api/namespaces/workflow.ts`、`developer_api/namespaces/developer_api.ts` 等
- E2E 文件名：`workflow-v2-acceptance.spec.ts` 是否改名（与策略 2/3 联动）
- 验证：`cd src/frontend && pnpm run build && pnpm run test:unit && pnpm run test:e2e:app && pnpm run i18n:check`

## 5. 风险与兼容策略

- **URL 兼容**：`api/v2/workflows` 已被 REST 客户端、E2E、proxy、低代码、文档、`.http` 大量引用；若选策略 2/3，必须双路由并行至少 6 个月（与 AGENTS.md「弃用窗口期不少于 6 个月」对齐），并在变更日志显式标注 Deprecated。
- **数据库**：当前 0 条 `[SugarTable("workflow_v2*")]` 声明，**类型重命名零迁移成本**。
- **Hangfire**：当前 0 个含 `WorkflowV2` 的 JobId，**在途任务零影响**。
- **能力注册**：`/apps/{appId}/workflow-v2` 路由模板若改名，需评估 `Capability` 表存量行是否需迁移脚本（M7-B 决策点）。
- **前端持久化**：当前未检出 `localStorage`/`sessionStorage` 含 `workflowV2`，无破坏性。
- **前端 IDL**：`packages/arch` 多为 Coze 上游 IDL 自动生成；若 codegen 来源不可控（来自 `coze-studio` 仓库），M7-C 需先补「本地化命名映射层」而非直接改生成物。
- **AGENTS.md / CLAUDE.md**：含 V2 字样的指导段落必须同步刷新，否则后续 AI 助理会误用旧命名。

## 6. 验证矩阵

每个子里程碑都必须执行以下验证：

- 后端：
  - `dotnet build` 必须 0 错误 0 警告
  - `dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"`
  - `dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~Integration"`
  - `dotnet test tests/Atlas.WorkflowCore.Tests`
  - 关键 `.http` 烟测：Login → Create → Save Draft → Publish → Run → Trace 全链路
- 前端：
  - `cd src/frontend && pnpm run build`
  - `cd src/frontend && pnpm run test:unit`
  - `cd src/frontend && pnpm run test:e2e:app`
  - `cd src/frontend && pnpm run i18n:check`
- 文档对齐：
  - `docs/contracts.md` 与代码一致
  - `AGENTS.md` / `CLAUDE.md` 同步刷新
  - `docs/coze-api-gap.md` / `docs/workflow-editor-validation-matrix.md` 一致

## 7. 立项前置条件

进入 M7 前必须先完成以下决策与确认：

1. **API 版本策略澄清**：`v2` 是 HTTP API 版本号还是产品 V2 名（决定 3.2 策略）
2. **类型命名定稿**：3.1 候选 A/B/C 二选一（建议 A）
3. **IDL codegen 管线确认**：`packages/arch/idl` 是上游同步还是本地维护（决定 M7-C 改动方式）
4. **能力注册数据迁移评估**：生产环境是否有存量 `workflow-v2` 路由模板的能力行（决定能力路由是否动）
5. **`v2` → 新版本号（如 `v3`）的取舍**：若同步推进类型 + 路由重命名，是否一次性升级到 `v3` 并废弃 `v2`，还是仅做类型重命名

## 8. 与本里程碑（Validator 修复）的关系

本里程碑（WorkflowV2 名称校验修复）严格遵循以下边界：

- **不**触发任何 `WorkflowV2*` 标识符重命名
- **不**改路由 `api/v2/workflows`
- **仅**收紧 `WorkflowV2CreateRequestValidator` / `WorkflowV2UpdateMetaRequestValidator` 的 `Name` 字段校验，并新增 i18n 资源 key、补回归测试、修正违规 `.http` 样例、在 `docs/contracts.md` 增加「WorkflowV2 名称规则」契约小节

新增的 resx key（`WorkflowV2NameFormat` / `WorkflowV2NameLength`）将在 M7-A 阶段随类型重命名一并刷新。
