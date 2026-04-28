# Repository Guidelines (AGENTS.md)

本文件是 Atlas Security Platform 的仓库级 AI 协作规则。所有助理回复必须使用中文。若本文与当前代码、`package.json`、`.csproj`、`.cursor/environment.json` 或实际目录冲突，以当前仓库实际状态为准。

## 1. 项目事实

- 项目：Atlas Security Platform，等保2.0 安全支撑平台。
- 后端：.NET 10、ASP.NET Core、SqlSugar、SQLite、Hangfire、YARP、OpenTelemetry、Semantic Kernel、WorkflowCore / DSL、MassTransit、Qdrant / MinIO。
- 前端：React 18、TypeScript、Semi Design、Rsbuild、pnpm monorepo。
- 当前运行拓扑：后端统一收敛到 `src/backend/Atlas.AppHost`，端口 `5002`；前端只有 `src/frontend/apps/app-web`，端口 `5181`。
- 历史：`Atlas.PlatformHost` 已从本仓库物理删除，不再存在兼容宿主；`Atlas.WebApp` 已删除，不再使用 Legacy 启动与构建命令。
- 所有运行时 / API / 迁移 / 数据源逻辑必须落在 `Atlas.AppHost` 或下层 Application / Infrastructure / Presentation.Shared，不得再引入第二套 Web 宿主。
- 关键文档：`docs/contracts.md`、`docs/plan-*.md`、`docs/workflow-editor-validation-matrix.md`、`docs/coze/`、`等保2.0要求清单.md`。

## 2. 目录与边界

- 方案文件：`Atlas.SecurityPlatform.slnx`。
- 后端路径：`src/backend/**`。
- 前端路径：`src/frontend/**`。
- 后端分层：`Atlas.Core` → `Atlas.Shared.Contracts` → `Atlas.Domain*` → `Atlas.Application*` → `Atlas.Infrastructure*` → Host。
- 后端模块：Workflow、LogicFlow、BatchProcess、AgentTeam、Approval、Assets、Audit、Alert 等。
- 前端结构：`apps/*` 只做宿主、路由、页面编排和环境适配；跨页面/模块复用能力沉淀到 `packages/*`。
- 重点前端包：`app-shell-shared`、`schema-protocol`、`shared-react-core`、`coze-shell-react`、`library-module-react`、`module-admin-react`、`module-explore-react`、`module-studio-react`、`workflow`。
- 共享契约优先沉淀到 `Atlas.Shared.Contracts`、前端 workspace packages 与 `docs/contracts.md`。

## 3. 权威顺序

1. 本文件 `AGENTS.md`
2. 当前代码、`package.json`、`.csproj`、`.cursor/environment.json`
3. `docs/contracts.md` 与 `docs/plan-*.md`
4. `CLAUDE.md` / `README.md`

`README.md` 与 `CLAUDE.md` 可能残留 Vue、`platform-web`、`Atlas.WebApi` 等旧信息，不得覆盖当前实现判断。

## 4. 工作流

- 修改前先阅读相关文件，确认现有架构、契约、计划与实现模式。
- 先分析再实施；先给出最小可行方案，再做最小化修改。
- 禁止擅自扩需求、重构无关模块、替换技术栈或引入未要求依赖。
- 遇到缺陷、重复逻辑、边界不清时，先做系统性诊断，再局部修补。
- 新增功能按最小闭环推进：后端接口 → 前端 API 客户端 → 前端页面/组件 → i18n → `.http` 示例 → 必要测试 → 文档。
- 每个用户需求都必须单独闭环：需求理解 → 影响面确认 → 实施 → 自审 → 构建/测试验证 → 规则对照 → 结果说明。未完成闭环前不得停止或声称完成。
- 自审必须循环执行：若自审、构建/测试或规则对照发现缺口，必须回到“影响面确认 → 实施/修正 → 验证 → 再自审”，直到闭环通过或明确不可继续的阻塞。
- 多个需求并行出现时，必须逐项建立闭环清单；每一项都要标明已完成、验证方式、剩余风险或阻塞原因。
- 不得声称“完成 / 修复 / 可用”，除非相关验证已通过；无法完整完成时必须说明已完成部分、阻塞点、风险与下一步。

## 5. 多代理策略

按技术域与复杂度分层 fan-out。子代理默认只读，用于探查、证据收集、复现、风险审查与边界确认；主代理统一收敛后再实现。具体子代理配置在 `.codex/agents/*.toml`。

- `L0`：纯问答、说明、文档解释、根因明确且改动点单一的小任务；不启动子代理。
- `L1`：单栈、小范围、根因基本明确；启动 1 个最贴近领域的探查代理。
- `L2`：单栈但根因不清，或涉及运行时/测试/配置；启动 2 个子代理。
- `L3`：跨前后端、跨模块、权限、登录、联调、回归或间歇性问题；启动 3 个子代理。
- `L4`：多子系统、架构迁移、安全、性能、并发或数据一致性专题；启动 4-5 个子代理。

固定角色：

- `frontend_surface_explorer`：前端页面、路由、宿主、workspace packages、i18n、浏览器交互。
- `backend_contract_explorer`：控制器、DTO、服务、仓储、认证、配置、后台作业与 API 契约。
- `integration_boundary_checker`：前后端契约、登录链路、`.http`、mock/adapter、一致性。
- `root_cause_explorer`：入口、调用链、配置开关、特性开关、真实执行路径。
- `repro_tester`：复现、日志、堆栈、失败测试、边界场景。
- `risk_reviewer`：正确性、回归、安全、权限、缺失测试。

主代理必须先定级，再决定 fan-out；已启动的子代理必须全部返回后再合并判断。若结论冲突，先补证并消解冲突，再进入实现。不得让多个代理并行修改同一文件或同一逻辑区域。

## 6. 常用命令

后端：

```bash
dotnet restore
dotnet build
dotnet run --project src/backend/Atlas.AppHost
dotnet test tests/Atlas.WorkflowCore.Tests
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~Integration"
```

前端：

```bash
cd src/frontend
pnpm install
pnpm run dev:app-web
pnpm run build
pnpm run build:app-web
pnpm run test:unit
pnpm run test:e2e:app
pnpm run i18n:check
pnpm run lint
pnpm run format
```

默认开发账号：

- 租户 ID：`00000000-0000-0000-0000-000000000001`
- 用户名：`admin`
- 密码：`P@ssw0rd!`

## 7. 编码规范

- .NET：4 空格缩进，PascalCase 类型/公开成员，camelCase 局部变量，file-scoped namespace，启用 nullable。
- React/TSX：2 空格缩进，kebab-case 页面/路由文件，PascalCase 组件导出，hooks 用 `useXxx`。
- 后端 I/O 必须 async/await；Controller 只做编排，不直接访问数据库。
- 注释只解释原因、约束和背景；禁止复述代码行为或留下无上下文 TODO。
- 新增文件必须加入对应项目文件或 workspace 配置。
- 构建必须 0 错误 0 警告。

## 8. 前端强约束

- 所有用户可见 UI 必须使用 `@douyinfe/semi-ui ^2.82.0` 及 Semi 相关包；禁止引入 Ant Design、MUI、Chakra UI、Element Plus 等其他组件库。
- `app-web` 已完成 Semi 化；禁止新增 `atlas-button`、`atlas-input`、`atlas-pill`、`atlas-tab`、`atlas-form-field`、`atlas-result-card`、`atlas-setup-*`、`atlas-loading-page`、`atlas-status-card` 等自绘 className。
- 新增 UI 优先使用 `apps/app-web/src/app/_shared/` 的 PageShell、FormCard、SectionCard、StateBadge、StepsBar、ResultCard、InfoBanner 或直接使用 Semi 组件。
- `app.css` 只保留全局变量、reset、body 背景、`.app-nav-glyph` 与 `.coze-*` 命名空间布局规则。
- 所有用户可见文案必须走 i18n；语言持久化键为 `atlas_locale`，值为 `zh-CN` 或 `en-US`。
- 包级 UI 文案禁止硬编码 CJK。组件库类包用 Required Labels 模式；业务模块类包用 `copy.ts` 字典模式。`copy.ts` 若含 CJK，必须在 `i18n-baseline.json` 中白名单。
- 搜索下拉框默认展示 20 条结果，必须提供搜索框并支持远程检索。

## 9. 后端与 API 强约束

- 公共 API 输入输出必须使用显式强类型 DTO、实体与接口；禁止反射、`dynamic`、运行时编译、表达式树等弱类型绕行。
- 禁止在循环内执行数据库操作；使用批量查询/更新/删除与字典/集合聚合。
- 控制器遵循 RESTful：资源名复数，路径表示资源层级，HTTP 动词表达动作；禁止 `/create`、`/update` 等动词路径。
- 所有 API 路由必须包含版本前缀，例如 `api/v1`。
- 新增或修改 API 端点必须更新 `src/backend/Atlas.AppHost/Bosch.http/` 下的 `.http` 示例。
- `/api/v1/setup-console/*`、`/api/v1/setup-console/migration/*` 与 `/api/v1/tenant-datasources/*` 的权威宿主是 `Atlas.AppHost`。setup-console 端点必须保持 `X-Setup-Console-Token`、IP 限流、审计 IP/UA、免普通登录态的安全模型。
- 修改 API 契约时同步更新后端 DTO、前端类型/API client、mock/adapter、测试和 `docs/contracts.md`。
- 当前仓库已废止 `Idempotency-Key` 与 `X-CSRF-TOKEN` 作为写接口公共前置要求；不得把它们写回实现、测试或契约文档。

## 10. 安全与合规

- 设计与实现必须符合等保2.0 要求，安全控制为必选项。
- 禁止提交密钥；使用环境变量或安全密钥存储。
- SqlSugar + SQLite 场景下执行最小权限数据访问，敏感字段按清单要求加密存储。
- 权限、认证、租户隔离、审计、写接口和后台作业变更必须优先评估安全影响。

## 11. 专题规则

### DAG 工作流

- `DagWorkflow*` / REST `api/v2/workflows` 必须与 LogicFlow 表达式能力对齐；节点表达式统一通过 `NodeExecutionContext.EvaluateExpression()`。
- `app-web` 工作流页面优先复用 `@coze-workflow/playground-adapter`、`@coze-studio/workspace-adapter` 与 `src/frontend/packages/workflow/**`；禁止再引入 Atlas 自研桥接分叉。
- Coze 画布编辑器保存的工作流必须是 Coze 原生合法 schema：`nodes[].id`、`nodes[].type`、`nodes[].meta.position`、`nodes[].data.nodeMeta`、`edges[].sourceNodeID`、`edges[].targetNodeID` 等字段必须真实持久化；读取接口必须原样读出该 schema，禁止在读取阶段临时转换、伪装或补坐标。
- Coze 工作流新建、保存、测试脚本和夹具至少必须持久化开始节点与结束节点，并带合法坐标；任何工作流卡片不得因缺失 `meta.position` 叠加在同一位置。
- `Atlas runtime schema`（如 `schemaVersion/nodes[key,type,config,layout]/edges[sourceNodeKey,targetNodeKey]`）仅允许用于 Atlas 微审批流、运行时内部 DSL 或显式标记的兼容执行层；禁止写入 Coze 画布编辑器工作流草稿，禁止把它作为 Coze Studio 的设计态持久化格式。
- 若需要让 Coze 原生 schema 进入 Atlas 运行时执行，必须通过明确的编译/适配层把 `data.inputs`、`data.outputs`、`meta.position` 与 `edges` 转成运行时 `CanvasSchema`；不得反向要求 Coze 编辑器读取 Atlas runtime schema。
- DAG 运行时需保障 Batch 子图、Loop + Break/Continue、Selector 分支裁剪、Resume（基于 preCompletedNodeKeys）。
- 新增/修改节点能力必须同步节点目录/模板 API、前端节点面板与属性表单、i18n、单测/E2E、`.http` 示例和 `docs/workflow-editor-validation-matrix.md`。

### Mendix 微流 Runtime

- RestCall Runtime 默认不得真实访问网络；只有显式 `allowRealHttp=true` 且通过 `MicroflowRestSecurityPolicy` 后，才允许经 `IMicroflowRuntimeHttpClient` / `IHttpClientFactory` 执行真实 HTTP。
- RestCall 必须使用 ExpressionEvaluator、VariableStore、ActionExecutorRegistry 和 Runtime HTTP security policy；不得在 executor 中散落裸 `HttpClient`。
- LogMessage 必须写入结构化 RuntimeLog，不得直接 `Console.WriteLine`；template arguments 必须通过 ExpressionEvaluator。
- SOAP/XML/Document/Workflow/ML/external object 等 connector-backed integration 在 connector 缺失时必须返回 `RUNTIME_CONNECTOR_REQUIRED`，禁止 silent success。

### 知识库 v5

- 知识库 API 唯一路由前缀为 `api/v1/knowledge-bases`。
- 基础 CRUD 由 `KnowledgeBasesController` 提供；v5 扩展由 `KnowledgeBasesV5Controller` 同前缀挂载，禁止重复实现。
- 前端统一通过 `@atlas/library-module-react` 的 `LibraryKnowledgeApi` 消费；`apps/app-web/src/services/api-knowledge.ts` 与 `mock/adapter.ts` 必须保持同型；切换走 `VITE_LIBRARY_MOCK`。
- 新增 SqlSugar 实体/仓储必须挂入 `Atlas.Infrastructure.Services.AtlasOrmSchemaCatalog`。
- ParsingStrategy、ChunkingProfile、RetrievalProfile、RetrievalCallerContext 是前后端共用契约，扩展字段必须双侧同步。
- `KnowledgeRetriever` / `KnowledgeIndexer` DAG 节点扩展必须同步 `BuiltInWorkflowNodeDeclarations` 和 `docs/workflow-editor-validation-matrix.md`。
- 所有知识库 parse/index 任务必须经 `IKnowledgeParseJobService` / `IKnowledgeIndexJobService` 走 Hangfire `IBackgroundJobClient.Enqueue<TRunner>`；禁止在 KB 处理中直接调用 `IBackgroundWorkQueue.Enqueue`。
- Hangfire runner 必须带 `[AutomaticRetry(Attempts=3)]`；失败时 runner 内部 `IncrementAttempts()`，达 MaxAttempts 写 `DeadLetter`。
- 详情以 `docs/plan-knowledge-platform-v5.md` 与 `docs/contracts.md` 为准。

### AI 数据库（Coze 复刻）

- API 前缀：`api/v1/ai-databases`；详情 DTO 含 `fields[]`、`channelConfigs[]`；记录读写区分 `environment`（Draft=1 / Online=2）。
- 物理存储：`AiDatabasePhysicalTableService` 按租户 + 库 ID 维护 `draft` / `online` 两张表；行数据为 `atlas_data_json` + owner/channel 元数据列。
- 访问策略：`AiDatabaseAccessPolicy` 实现单用户 `OwnerUserId` 过滤与 `ChannelScope`（完全共享 / 渠道隔离 / 站内共享）；写操作前按 `AiDatabaseChannelConfig` 校验渠道对测试/线上域的开关。
- 渠道注册表：`Atlas.Infrastructure.Channels.ChannelCatalog` 与 `AiDatabaseChannelCatalog` 保持一致；宿主通过 `AddAtlasInfrastructureChannels` 绑定微信等选项类（敏感凭据走配置/密钥存储）。
- 渠道凭据入口：工作区设置页 `WorkspaceSettingsPublishPage` 的 `channels` Tab 复用 `module-studio-react` 的 `ChannelsListPanel` / `ChannelDetailRouter`；飞书、微信公众号、微信小程序、微信客服凭据统一走 `/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/*-credential`，由前端页面可配置并加密落库。
- 前端：`module-studio-react` 数据库详情页文案走 `copy.ts` 的 `databaseDetail`；资源库创建数据库默认 `SingleUser` + `ChannelIsolated`。
- 计划与矩阵：见 `docs/plan-ai-database.md`；契约细节以 `docs/contracts.md`「AI 数据库补充契约」为准。

### 表格个人视图

- 员工、角色、权限、菜单、部门、职位、项目、应用管理页面已接入表格个人视图能力。
- 视图只绑定当前登录用户，后台以 `tenant_id + user_id` 识别，前端不可传用户标识。
- 默认配置由 `TableViewDefaultOptions` / `TableViewDefaults` 定义；调整时同步后端配置与 `docs/contracts.md`。

### 登录与入口 UX

- 涉及登录页或入口体验时，先检查 `src/frontend/apps/app-web` 现有实现、i18n、认证接口和相关计划文档。
- 中英混杂先检查 `atlas_locale` 与语言切换器，再确认是否为最新构建产物。

## 12. 验证与收尾

- 收尾前必须执行三项对照：
  1. 自审对照：检查需求是否逐项满足，是否引入回归、权限/安全风险、契约漂移、重复实现或无关改动。
  2. 构建对照：按影响面运行最小必要的 build/test/lint/i18n/check；无法运行时必须说明原因、替代验证和残余风险。
  3. 规则对照：逐项核对 `AGENTS.md`、相关计划文档、`docs/contracts.md`、前后端强约束和专题规则。
- 任一对照不通过时不得收尾；必须继续修正并重新执行三项对照。只有三项对照均通过，或存在已说明且无法由当前任务消除的外部阻塞，才允许停止。
- 后端变更至少运行相关 `dotnet build` / `dotnet test`。
- 前端变更至少运行相关 `pnpm run build`、`pnpm run test:unit`、`pnpm run i18n:check`。
- API 变更必须验证 `.http` 示例、契约、前端 client/mock 与必要测试。
- UI 变更需附截图或说明浏览器验证结果。
- 提交信息使用 conventional commits，例如 `feat:`、`fix:`、`docs:`。
- 架构或契约变更必须同步 `AGENTS.md` 与 `docs/contracts.md`。
- 最终回复必须包含闭环结果：改了什么、验证了什么、未验证什么、是否仍有风险或下一步。

## 13. 长任务规则

- 长任务先拆为小里程碑，每个里程碑按“分析 → 实施 → 验证 → 记录结果”闭环推进。
- 开始编码前说明任务理解、范围边界、里程碑、涉及文件和验证方式。
- 当前里程碑验证通过后默认继续下一步；除非遇到阻塞、约束冲突或上下文不足。
- 上下文不足时立即停止，明确已完成、未完成、阻塞原因和建议下一步；不得草草收场或伪造完成。

## 14. 跨项目精准复刻专项

当任务目标是把另一个项目的前端功能、交互或业务逻辑复刻到本项目时，默认按长任务处理。无论旧项目使用何种语言或框架，都必须先完成只读盘点和矩阵确认，再进入实现。

### 阶段 1：旧项目只读盘点

- 识别旧项目技术栈、目录结构、启动方式、前端入口、路由系统、API client、权限体系、状态管理、i18n 与构建产物。
- 按模块梳理所有前端功能：页面、路由、组件、列表、表单、弹窗、按钮操作、空状态、异常状态、权限入口、批量操作、导入导出、后台任务入口。
- 对每个功能追踪 API 调用：URL、方法、请求参数、响应结构、分页/排序/筛选、错误处理、前端校验和状态更新。
- 继续追踪旧项目后端实现：入口、服务逻辑、数据模型、数据库表、权限、租户、审计、异步任务、外部依赖。
- 每条结论必须带证据文件路径和关键符号；不得凭猜测补齐功能。

### 阶段 2：复刻矩阵

实现前必须输出并等待用户确认“功能复刻矩阵”。矩阵至少包含：

- 功能名称
- 旧项目前端入口
- 旧项目交互流程
- 旧项目 API 契约
- 旧项目后端实现路径
- 当前 Atlas 前端落点
- 当前 Atlas 后端落点
- 契约差距
- UI / i18n / 权限 / 数据 / 作业风险
- 建议复刻方式
- 验证方式

矩阵未完成或未经用户确认前，不得开始写代码。

### 阶段 3：Atlas 适配实现

- 不直接照搬旧项目架构；必须映射到本项目 .NET 分层、React app-web、workspace packages、Semi Design、i18n、API 契约与安全规则。
- 若旧项目逻辑与本项目架构、权限、租户、安全或契约冲突，先标记冲突并给出适配方案。
- 每个复刻 case 必须独立闭环：后端契约/服务 → 前端 client/页面/组件 → i18n → `.http` → 测试 → 文档。
- 每个 case 验证通过后，才进入下一个 case。
