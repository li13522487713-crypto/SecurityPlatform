# Repository Guidelines (AGENTS.md)

本文件为 AI 助理提供仓库级开发指南。详细技术说明可参考 `CLAUDE.md`，但若与当前代码、`package.json`、`.csproj`、`.cursor/environment.json` 或实际目录结构冲突，以当前仓库实际状态为准。

**语言要求：** 所有助理回复必须使用中文。

## 项目概览

**Atlas Security Platform** — 符合等保2.0（GB/T 22239-2019）的安全支撑平台，当前已演进为"平台控制面 + 应用运行时 + React 应用壳前端 + 多 package 能力层"的架构，支持多租户、AI 工作流、知识库与严格安全控制。

- 后端：.NET 10 + ASP.NET Core + SqlSugar + SQLite，结合 Hangfire、YARP、OpenTelemetry、Semantic Kernel、WorkflowCore / WorkflowCore.DSL、MassTransit、Qdrant / MinIO 等运行时能力
- AppWeb：React 18 + TypeScript + Semi Design + Rsbuild，支持 `platform` / `direct` 双运行模式
- 前端共享：pnpm monorepo，采用"`app-web` 单宿主 + `packages/*` 多包能力层"的组织方式
- 关键文档：`等保2.0要求清单.md`、`docs/contracts.md`、`docs/workflow-editor-validation-matrix.md`、`docs/plan-*.md`、`docs/coze/`

## 架构与目录

- 方案：`Atlas.SecurityPlatform.slnx`，代码位于 `src/backend/`、`src/frontend/`
- 后端分层：`Atlas.Core` → `Atlas.Shared.Contracts` → `Atlas.Domain*` → `Atlas.Application*` → `Atlas.Infrastructure*` → `Atlas.PlatformHost` / `Atlas.AppHost`
- 后端纵向模块：在基础层之上按能力拆分为 `Workflow`、`LogicFlow`、`BatchProcess`、`AgentTeam`、`Approval`、`Assets`、`Audit`、`Alert` 等模块
- 前端结构：`src/frontend/apps/*` 为宿主壳应用，`src/frontend/packages/*` 为共享核心、协议、编辑器、能力层与业务模块包
- 前端当前重点包：
  - 宿主与路由：`app-shell-shared`
  - 协议与共享：`schema-protocol`、`shared-react-core`
  - Shell 与业务模块：`coze-shell-react`、`library-module-react`、`module-admin-react`、`module-explore-react`、`module-studio-react`、`workflow`
- 运行拓扑：当前开发与运行默认统一收敛到 `AppHost`（`5002`）；`PlatformHost` 已废弃，仅保留历史目录与兼容资产；当前前端宿主只有 `app-web`
- 共享契约：`docs/contracts.md` 定义 API / 画布 / 运行时契约，跨宿主共享类型优先沉淀到 `Atlas.Shared.Contracts` 与前端 workspace packages

完整结构与依赖以仓库实际目录、各项目 `.csproj` / `package.json`、`.cursor/environment.json` 以及 `docs/contracts.md` 为准。

## 文档优先级

- 指令冲突时按以下优先级判断：`AGENTS.md` > 当前代码 / `package.json` / `.csproj` / `.cursor/environment.json` > `docs/contracts.md` 与 `docs/plan-*.md` > `CLAUDE.md` / `README.md`
- `README.md` 与 `CLAUDE.md` 中仍可能残留旧前端形态（如 Vue / `platform-web` / `Atlas.WebApi` 等历史信息），禁止直接据此覆盖当前实现判断。
- 涉及运行命令、端口、宿主数量、workspace 包名时，必须先以实际目录和脚本为准再执行。

## 构建与开发命令

### 后端
```bash
dotnet build                                    # 必须 0 错误 0 警告
dotnet run --project src/backend/Atlas.AppHost        # 应用后端 http://localhost:5002
dotnet restore
```

### 前端（pnpm monorepo）
```bash
cd src/frontend
pnpm install                    # 安装所有 workspace 依赖
pnpm run dev:app-web            # AppWeb 开发服务器 http://localhost:5181
pnpm run dev:app-web:platform   # AppWeb 以平台语义模式启动（开发代理仍统一命中 AppHost）
pnpm run dev:app-web:direct     # AppWeb 以直连语义模式启动（开发代理同样命中 AppHost）
pnpm run build                  # 构建前端（当前默认构建 app-web）
pnpm run build:app-web          # 仅构建 AppWeb
pnpm run test:unit              # 运行前端单元测试
pnpm run test:e2e:app           # 运行应用壳 E2E
pnpm run i18n:check             # 校验中英文词条与 i18n 对齐
pnpm run lint                   # Lint 所有项目
pnpm run format                 # 格式化所有项目
```

### 前端（Legacy）
`Atlas.WebApp` 已删除（2026-04-05），不再支持 Legacy 启动与构建命令。

### API 测试
- 当前开发与验收默认使用 `AppHost` 的 `.http` 文件：`src/backend/Atlas.AppHost/Bosch.http/`
- `src/backend/Atlas.PlatformHost/Bosch.http/` 仅保留历史兼容资产；除非明确维护历史接口，否则不要再作为新增/修改接口的主契约入口
- 每个新增或修改的接口需优先在 `AppHost` 下创建或更新 `.http` 文件；若确有历史兼容需求，再同步补充 `PlatformHost`

## 编码规范

- **.NET：** 4 空格缩进，PascalCase 类型/公开成员，camelCase 局部变量；File-scoped namespaces；启用 Nullable reference types。
- **React/TSX：** 2 空格缩进，kebab-case 页面/路由文件，PascalCase 组件导出；hooks 用 `useXxx`；TypeScript 严格模式，禁止 `any`。
- **前端 UI 框架（强制约束）：** 所有前端应用与 workspace 包的用户可见 UI **必须且只能使用 `@douyinfe/semi-ui ^2.82.0`**（含 `@douyinfe/semi-icons`、`@douyinfe/semi-foundation`、`@douyinfe/semi-illustrations`）作为组件库。**禁止**引入 Ant Design、MUI、Chakra UI、Element Plus 或任何其他第三方组件库；新增 `apps/*` 或 `packages/*` 若包含可交互 UI，必须在其 `package.json` `dependencies` 中声明 `@douyinfe/semi-ui ^2.82.0`，否则 PR 不予合并。
- **异步：** 所有 I/O 必须 async/await；控制器必须通过 Repository / Service，不得直接访问数据库。
- **注释：** 只说明"为什么"与约束背景，禁止复述"做了什么"；禁止无上下文 TODO。
- **前端分层：** 跨页面/模块/壳能力优先沉淀到 `packages/*`，`apps/*` 只负责装配、路由、页面编排与环境适配。
- **语法：** 始终使用对应技术栈的最新稳定语法特性（C# 13 / ES2024+）。

完整约定见 `CLAUDE.md` 的 Coding Standards 章节。

## 开发核心原则

### 任务拆分与闭环
- 每个需求必须拆分为粒度极细的小 case，每个 case 可独立完成并独立验证（有明确的完成标准）。
- 禁止在单个 case 中同时修改前端 + 后端 + 测试 + 文档；按依赖顺序逐步交付。
- 每个小 case 完成后必须立即执行对应验证，通过后再进入下一个 case。
- **前后端必须同步交付**：实现后端接口时，必须同步实现对应的前端界面与交互；禁止只实现后端而前端没有可用的操作入口。

### 通用能力解耦
- 任何跨模块/跨页面/跨项目复用的能力，必须封装为独立库或 workspace package，不得在业务代码中重复实现。
- 后端通用能力沉淀到 `Atlas.Core`、`Atlas.Shared.Contracts` 或独立 Infrastructure 包。
- 前端通用能力沉淀到 `shared-react-core`、`schema-protocol`、`app-shell-shared` 等 workspace 包。
- 封装时遵循"高内聚、低耦合"原则：对外暴露最小接口，内部实现完全隐藏；优先使用接口/抽象类而非具体实现依赖。

### 上下文不足时的处理
- 若当前上下文窗口不足以完整完成当前 case，必须立即停止，**不得草草收场或伪造完成**。
- 停止时必须明确说明：当前已完成部分、未完成部分、阻塞原因、建议下一步。
- 用户可根据停止说明重新发起对话，继续从断点处推进。

### 完整性审查
- 实现前：先核对现有代码、契约文档与计划，确认前后端实现边界。
- 实现中：后端接口 → 前端 API 客户端 → 前端页面/组件 → i18n 词条 → `.http` 测试文件 → 必要测试，缺一不可。
- 实现后：执行构建 + 测试 + i18n 校验，通过后方可宣称完成。

## 文档驱动开发

- 按文档驱动实施：先有产品架构清单，再针对每个小需求完整跟踪实现。
- `docs/plan-*.md` 为实施计划；如存在专题说明或迁移文档，优先结合 `docs/coze/`、`docs/coze-workflow-migration.md`、`docs/contracts.md` 一并阅读。
- 需求拆分为前端与后端实现计划，小步慢跑完成；每个任务需可闭环。
- 新增功能：先核对当前代码、契约文档与实施计划，再按最小闭环补齐实现、测试、`.http` 示例与文档。

## 开发约束

- **零警告：** 构建必须 0 错误 0 警告（由 `Directory.Build.props` 约束）。
- **修改前：** 必须先阅读目标文件，理解既有模式后再做最小化修改。
- **新增文件：** 必须将新文件加入对应项目文件（`.csproj`），并解决所有警告。
- **实现顺序：** 先实现底层代码，再实现引用层代码。
- **避免过度设计：** 仅实现所需功能，不添加未要求的能力。
- **解题原则：** 先从架构、边界、契约与复用层面处理，消除系统性问题；用"定义问题 → 提出假设 → 收集证据 → 验证结论"的思维解题，避免只盯单个报错钻牛角尖。
- **国际化：** 所有用户可见文案必须走 i18n，禁止硬编码；中英文词条必须同步，日期/数字/货币格式走区域配置。

### 前端界面语言与排查

- 语言持久化在浏览器 `localStorage` 键 **`atlas_locale`**，取值为 **`zh-CN`** 或 **`en-US`**（实现见 `src/frontend/apps/app-web/src/app/i18n.tsx`）。
- **中英混杂**：先确认 `atlas_locale` 与语言切换器一致；再确认是否为最新 `pnpm run build` 产物。
- **中英键对齐**：对比 `zh-CN.ts` / `en-US.ts` 是否同步更新，避免新增 key 漏翻。

### 包级 i18n 契约（M2-M6 收口规则，强制约束）

`pnpm run i18n:check` 现在不仅校验 `apps/app-web/src/app/messages.ts` 的中英对齐，还会扫描 `packages/*/src/**/*.{ts,tsx}` 内的 CJK 硬编码与包导出 Labels 的宿主接管覆盖（实现见 [src/frontend/scripts/i18n-audit.mjs](src/frontend/scripts/i18n-audit.mjs)）。

**两种合规模式（任选一种，单包内必须一致）**：

1. **Required Labels 模式**（推荐用于"组件库类"包，如 `@atlas/external-connectors-react`）
   - 包内每个用户可见组件导出 `XxxLabelsKey` 联合类型 + `XxxLabels = Record<XxxLabelsKey, string>` 类型 + `XXX_LABELS_KEYS` `as const` 数组 + `defaultXxxLabels: XxxLabels`（中性英文兜底）。
   - 组件 props 必须 `labels: XxxLabels`（**Required**，禁止 `Partial`），编译期强制宿主穷举注入。
   - 宿主 `apps/app-web` 在 `messages.ts` 加对应 zh/en keys，在页面里显式 `t("namespace_xxx")` 注入每个 label。

2. **包级 copy.ts 字典模式**（推荐用于"业务模块类"包，如 `@atlas/library-module-react`、`@atlas/module-studio-react`）
   - 包内统一维护 `src/copy.ts`（导出 `getStudioCopy(locale)` / `getLibraryCopy(locale)` 等），文件本身按 `i18n-baseline.json` 加白名单豁免 CJK 检查。
   - 每个组件接 `locale: SupportedLocale` prop，内部用 `getXxxCopy(locale)` 拿翻译，不再使用内联 `locale === "en-US" ? ... : ...` 三元。
   - 字典分子节点（按功能域分组），便于增量扩充。

**强制约束**：

- 包内任何用户可见 UI 文案禁止硬编码 CJK；新加 / 修改组件时同步更新对应字典或 Labels 类型。
- 测试 fixture / mock data / `*.test.tsx` / `*.spec.tsx` / `__tests__/**` 自动豁免；**`copy.ts` 字典文件本身**必须列入 `i18n-baseline.json` 的 `allowedCjkFiles`。
- 纯开发者诊断（`throw new Error(...)` / `console.warn(...)` / 调试日志）允许 CJK 但**应尽量保留英文**；如必须用 CJK，加入 `allowedCjkFiles` 并标注 reason。
- baseline 中的 `_pendingUserFacingFiles` 是 TODO 清单，新建 PR 时不允许在该清单中追加新文件，只允许移除（即只能更"干净"，不能更"脏"）。

**新增包必读**：开发新包时，先决定走模式 1 还是模式 2，参照 `external-connectors-react` 或 `library-module-react` 的实现照搬。

## 前后端约束

- 后端：禁止反射、`dynamic`、运行时编译或表达式树等弱类型特性；必须使用强类型 DTO、实体与接口，所有公共 API 输入输出显式类型声明与验证。
- 后端：禁止在循环内执行数据库操作；优先批量查询/更新/删除，通过字典或集合聚合减少往返次数。
- 前端：禁止 `any`、`unknown` 或 `eval`/动态注入；必须全量 TypeScript 类型标注，API 客户端与接口契约保持类型对齐。
- **前端 UI 框架（唯一强制）：** 所有前端 UI 组件必须使用 `@douyinfe/semi-ui`（Semi Design）；禁止引入任何其他 UI 组件库；自绘组件必须基于 Semi 组件进行封装或扩展，不得绕过 Semi 体系独立实现可交互 UI。
- 前端：搜索下拉框默认展示 20 条结果，必须提供搜索框并支持远程检索。
- 前后端：遇到缺陷、重复逻辑、边界不清时，先做系统性诊断，再局部修补。
- 契约：前后端共享数据契约集中于 `docs/contracts.md` 并保持与实现同步，修改契约时同步更新类型定义与验证。

## API 测试文件

- 每个新增或修改的 API 端点需在对应 Host 下创建或更新 `*.http` 文件。
- `.http` 文件需包含覆盖受影响端点的请求示例。

## 控制器规范（RESTful + 版本控制）

- 控制器遵循 RESTful 风格：资源名复数、路径表示资源层级、HTTP 动词表达操作语义。
- 禁止在路径中使用动词（如 `/create`、`/update`）。
- 所有 API 路由必须包含版本前缀（如 `api/v1`）；新增版本保持向后兼容或明确弃用策略。
- 弃用流程：新版本发布时标记旧版本 Deprecated，给出至少 6 个月弃用窗口；窗口结束后方可移除。

## 测试与验证

- **后端：** 使用 xUnit，测试项目位于 `tests/Atlas.WorkflowCore.Tests` 与 `tests/Atlas.SecurityPlatform.Tests`；接口验证配套 `.http` 文件。
- **前端：** 使用 Vitest 单元测试、Playwright E2E 测试，并通过 `pnpm run i18n:check` 做词条校验。
- 新增测试优先复用现有体系，命名模式：`*Tests.cs`、`*.spec.ts`。

## Dag 工作流引擎（Coze 复刻）补充约束

- DAG 工作流引擎（`DagWorkflow*` / REST `api/v2/workflows`）必须与 LogicFlow 表达式能力对齐，节点表达式统一通过 `NodeExecutionContext.EvaluateExpression()`。
- `app-web` 工作流页面优先复用 `@coze-workflow/playground-adapter`、`@coze-studio/workspace-adapter` 与 `src/frontend/packages/workflow/**`，禁止再引入 Atlas 自研桥接包分叉实现。
- DAG 运行时需保障：Batch 子图执行、Loop + Break/Continue、Selector 分支裁剪、Resume（基于 preCompletedNodeKeys）。
- 前端工作流编辑器维持"节点声明驱动 + 动态表单渲染"模式。
- 新增/修改节点能力时，必须同步更新：节点目录/模板 API、前端节点面板分组与属性表单、i18n 词条、单测/E2E/`.http` 示例、`docs/workflow-editor-validation-matrix.md`。

## 知识库专题（v5 §32-44）补充约束

- 知识库 API 唯一权威路由前缀为 `api/v1/knowledge-bases`：基础 CRUD 由 `KnowledgeBasesController` 提供，v5 §32-44 扩展（jobs / bindings / permissions / versions / retrieval-logs / provider-configs / table / image / 统一 retrieval）由 `KnowledgeBasesV5Controller` 同前缀挂载，禁止重复实现。
- 前端组件统一通过 `@atlas/library-module-react` 的 `LibraryKnowledgeApi` 抽象消费，`apps/app-web/src/services/api-knowledge.ts` 与 `mock/adapter.ts` 必须保持同型；切换走 `VITE_LIBRARY_MOCK` 环境开关。
- 知识库新增 SqlSugar 实体 / 仓储必须挂入 `Atlas.Infrastructure.Services.AtlasOrmSchemaCatalog`，否则平台启动时不会建表。
- ParsingStrategy / ChunkingProfile / RetrievalProfile / RetrievalCallerContext 是前后端共用契约：前端在 `library-module-react/src/types.ts`、后端在 `Atlas.Application/AiPlatform/Models/KnowledgeStrategyModels.cs` 与 `RetrievalLogModels.cs`，扩展字段必须双侧同步。
- `KnowledgeRetriever` / `KnowledgeIndexer` DAG 节点扩展时必须同步更新 `BuiltInWorkflowNodeDeclarations`（默认 config + form-meta + 端口 schema）和 `docs/workflow-editor-validation-matrix.md`；新增字段必须既保留旧字段以兼容历史画布，又把新字段写到 form-meta 让节点面板能渲染。
- **Hangfire 强约束（v5 §35 / 计划 G3）：** 所有知识库 parse / index 任务必须经过 `IKnowledgeParseJobService` / `IKnowledgeIndexJobService` 走 Hangfire `IBackgroundJobClient.Enqueue<TRunner>` 链路。禁止在新 KB 处理代码中直接调用 `IBackgroundWorkQueue.Enqueue` 入 KB 处理；非 KB 后台任务（清理 / 批处理 / 通知）继续可用 IBackgroundWorkQueue。Hangfire runner 必须带 `[AutomaticRetry(Attempts=3)]` 注解；失败时 runner 内部 `IncrementAttempts()` 后达 MaxAttempts 自动写 `DeadLetter`。
- 完整里程碑、契约对照、验收命令清单与回滚指引见 `docs/plan-knowledge-platform-v5.md`，与 `docs/contracts.md` 共同构成本专题的契约权威。

## 提交与变更

- **提交信息：** 采用 conventional commits（`feat:`、`fix:`、`docs:` 等）。
- **PR/变更：** 包含简要说明、关联需求；UI 变更需附截图。
- **架构变更：** 修改架构时需同步更新 `AGENTS.md` 与 `docs/contracts.md`。

## 安全与合规（等保2.0）

- 设计与实现须符合等保2.0 要求，安全控制为必选项。
- 禁止在仓库中存放密钥；使用环境变量或安全密钥存储。
- SqlSugar + SQLite：实施最小权限数据访问，敏感字段按清单要求加密存储。
- 完整清单见 `等保2.0要求清单.md`；已实现安全控制见 `CLAUDE.md` 的 Security and Compliance 章节。

### 写接口安全基线

- 当前仓库已废止 `Idempotency-Key` 防重放机制与 `X-CSRF-TOKEN` 校验机制。
- 新增或修改写接口时，不得将上述机制作为公共前置要求写回实现、测试或契约文档。
- 如需重新引入等效保护，必须先补充替代安全方案与契约说明，再整体落地。

## 表格视图（个人）支持

- 员工/角色/权限/菜单/部门/职位/项目/应用管理页面均已接入统一表格个人视图能力（见 `docs/contracts.md` "表格视图（个人）"章节）。
- 视图只绑定当前登录用户（后台以 `tenant_id + user_id` 识别，前端不可传递用户标识）。
- `TableViewConfig` 支持列配置、密度、分页等项，HTTP 测试见 `src/backend/Atlas.PlatformHost/Bosch.http/TableViews.http`。
- 默认配置由 `TableViewDefaultOptions`（`appsettings.json` 的 `TableViewDefaults` 节）定义，调整时同步更新后端配置与 `docs/contracts.md`。

## 登录与 UX 说明

- 涉及登录页或入口体验调整时，必须先检查 `src/frontend/apps/app-web` 的现有实现、i18n 文案、认证接口与相关计划文档，再决定改动范围。
- **app-web 已全量 Semi Design 化（M0-M7 完成）**：`src/frontend/apps/app-web/src/app/{pages,components,layouts}` 内所有页面与组件已统一使用 Semi 组件 + `apps/app-web/src/app/_shared/` 公共壳，**禁止再引入 `atlas-button` / `atlas-input` / `atlas-pill` / `atlas-tab` / `atlas-form-field` / `atlas-result-card` / `atlas-setup-panel` / `atlas-setup-card` / `atlas-loading-page` / `atlas-status-card` 等自绘 className**；新增 UI 必须走 `_shared/` 公共壳（PageShell / FormCard / SectionCard / StateBadge / StepsBar / ResultCard / InfoBanner）或直接用 Semi UI 组件。`app.css` 仅保留 `:root` 全局变量、`html/body/#app` reset、`body` 渐变背景、`.app-nav-glyph` 与 `.coze-*` 命名空间布局规则；新增 atlas-* 选择器视为破坏性变更，PR 不予合并。
- **lowcode 子应用 Semi 化（M9-M14 完成）**：`apps/lowcode-sdk-playground` / `apps/lowcode-mini-host` / `apps/lowcode-preview-web` / `apps/lowcode-studio-web` 全部 `.tsx`（除纯路由装配 `main.tsx` 与已豁免文件外）已 Semi 化；新增 lowcode 子应用必须在 `package.json` `dependencies` 加 `@douyinfe/semi-ui ^2.82.0` + `@douyinfe/semi-icons ^2.82.0`，并以 Semi 组件实现可交互 UI。完整豁免名单见 [docs/contracts.md](docs/contracts.md) "monorepo Semi Design 全量覆盖与豁免名单"章节。

## Cursor Cloud specific instructions

### 系统依赖

- **后端：** 需要 .NET 10 SDK（`dotnet-sdk-10.0`），Ubuntu 24.04 可通过 `sudo apt-get install -y dotnet-sdk-10.0` 安装。
- **前端：** 需要 Node.js 22，并使用 `pnpm` 管理 `src/frontend` workspace 依赖；如环境缺少 `pnpm`，通过 Corepack 自动启用。
- **Cloud 预装：** 仓库提供 `.cursor/environment.json`，启动时执行 `scripts/cursor-cloud-install.sh`，自动校验 Node 22、执行 `dotnet restore`，并在 `src/frontend` 下运行 `pnpm install`。

### 服务概览

| 服务 | 端口 | 启动命令 |
|---|---|---|
| AppHost | 5002 | `dotnet run --project src/backend/Atlas.AppHost` |
| AppWeb 开发服务器 | 5181 | `cd src/frontend && pnpm run dev:app-web` |

`app-web` 默认以 `platform` 语义模式启动，但本地开发代理统一直达 `AppHost`；如需切换前端运行语义，可使用 `cd src/frontend && pnpm run dev:app-web:direct`。

数据库为嵌入式 SQLite（`atlas.db`），无需外部数据库服务。Hangfire（`hangfire.db`）同样为嵌入式 SQLite 存储。首次启动时会自动创建数据库并初始化 BootstrapAdmin 账号。

### 后端启动

后端在 `Development` 模式下运行，配置来自 `appsettings.Development.json`。标准启动命令：

```bash
dotnet run --project src/backend/Atlas.AppHost
```

### 开发默认账号

- **租户 ID：** `00000000-0000-0000-0000-000000000001`
- **用户名：** `admin`
- **密码：** `P@ssw0rd!`（由 `appsettings.Development.json` 的 `Security.BootstrapAdmin.Password` 配置）

### 测试

- 后端工作流测试：`dotnet test tests/Atlas.WorkflowCore.Tests`
- 后端单元/领域测试：`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"`
- 后端集成测试：`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~Integration"`
- 前端单元测试：`cd src/frontend && pnpm run test:unit`
- 前端 E2E：`cd src/frontend && pnpm run test:e2e:app`
- 前端国际化校验：`cd src/frontend && pnpm run i18n:check`
- 前端构建与 Lint：`cd src/frontend && pnpm run build`、`cd src/frontend && pnpm run lint`

### 构建与 Lint 命令参考

标准命令以 `AGENTS.md`、`.cursor/environment.json` 与实际 `package.json` / `.csproj` 为准；`CLAUDE.md` 仅作补充背景参考。

## AI 助理执行约束

- 所有回复必须使用中文。
- 修改前必须先阅读相关文件，理解现有架构、分层、契约与既有实现模式。
- 先分析，再实施；先给出最小可行方案，再进行代码修改。
- 严禁擅自扩需求、重构无关模块、替换技术栈或引入未要求依赖。
- 分层边界：Controller/页面层只做编排；数据访问通过 Repository/Service；前端 `apps/*` 只做装配，共享能力沉淀到 `packages/*`。
- 优先最小化修改，保持 diff 可审查、可回滚、可验证。
- 每完成一个阶段必须验证：后端执行 `dotnet build` / `dotnet test`；前端执行 `pnpm run build` / `pnpm run test:unit` / `pnpm run i18n:check`。
- 不得声称"已完成""已修复""可用"，除非已完成对应验证。
- 新增或修改 API 时，必须同步更新 `.http` 文件、契约文档与必要测试。
- 如无法完整完成，必须明确说明阻塞点、已完成部分、风险与下一步建议，不得伪造结果。

## 长任务执行规则

- 长任务必须先拆分为多个里程碑，按"分析 → 实施 → 验证 → 进入下一里程碑"闭环推进。
- 开始编码前，必须先输出：任务理解、范围边界、里程碑拆分、涉及文件、验证方式。
- 每个里程碑：先做最小可行实现 → 立即执行构建/测试/i18n 校验 → 记录修改文件、改动、验证结果。
- 当前里程碑验证通过后，默认自动进入下一个里程碑，不因阶段性完成而中断。
- **上下文不足时**：若上下文窗口不足以完整完成当前 case，必须立即停止，明确说明已完成部分、未完成部分、阻塞原因，由用户重新发起对话继续推进；**禁止草草收场或伪造完成**。
- 只有在以下情况才停止并汇报：遇到明确阻塞无法推进、继续执行会违反架构/契约/安全约束、需求本身存在冲突。
- 最终必须输出：里程碑完成情况、修改文件清单、执行命令、验证结果、剩余风险与后续建议。
- **不得把长任务只完成一部分就当作整体完成**；除非所有里程碑完成并通过验证，否则不得宣称任务完成。
