# Repository Guidelines (AGENTS.md)

本文件为 AI 助理提供仓库级开发指南。详细技术说明可参考 `CLAUDE.md`，但若与当前代码、`package.json`、`.csproj`、`.cursor/environment.json` 或实际目录结构冲突，以当前仓库实际状态为准。

**语言要求：** 所有助理回复必须使用中文。

## 项目概览

**Atlas Security Platform** — 符合等保2.0（GB/T 22239-2019）的安全支撑平台，当前已演进为“平台控制面 + 应用运行时 + React 应用壳前端 + 多 package 能力层”的架构，支持多租户、AI 工作流、知识库与严格安全控制。

- 后端：.NET 10 + ASP.NET Core + SqlSugar + SQLite，结合 Hangfire、YARP、OpenTelemetry、Semantic Kernel、WorkflowCore / WorkflowCore.DSL、MassTransit、Qdrant / MinIO 等运行时能力
- AppWeb：React 18 + TypeScript + Semi Design + Vite 8，支持 `platform` / `direct` 双运行模式
- 前端共享：pnpm monorepo，采用“`app-web` 单宿主 + `packages/*` 多包能力层”的组织方式
- 关键文档：`等保2.0要求清单.md`、`docs/contracts.md`、`docs/workflow-editor-validation-matrix.md`、`docs/plan-*.md`、`docs/coze/`

## 架构与目录

- 方案：`Atlas.SecurityPlatform.slnx`，代码位于 `src/backend/`、`src/frontend/`
- 后端分层：`Atlas.Core` → `Atlas.Shared.Contracts` → `Atlas.Domain*` → `Atlas.Application*` → `Atlas.Infrastructure*` → `Atlas.PlatformHost` / `Atlas.AppHost`
- 后端纵向模块：在基础层之上按能力拆分为 `Workflow`、`LogicFlow`、`BatchProcess`、`AgentTeam`、`Approval`、`Assets`、`Audit`、`Alert` 等模块
- 前端结构：`src/frontend/apps/*` 为宿主壳应用，`src/frontend/packages/*` 为共享核心、协议、编辑器、能力层与业务模块包
- 前端当前重点包：
  - 宿主与路由：`app-shell-shared`
  - 协议与共享：`schema-protocol`、`shared-react-core`
  - Shell 与业务模块：`coze-shell-react`、`library-module-react`、`module-admin-react`、`module-explore-react`、`module-studio-react`、`module-workflow-react`、`workflow-core-react`、`workflow-editor-react`
- 运行拓扑：`PlatformHost` 是平台控制面与 API 网关，`AppHost` 是应用运行时数据面；当前前端宿主只有 `app-web`
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
dotnet run --project src/backend/Atlas.PlatformHost   # 平台后端 http://localhost:5001
dotnet run --project src/backend/Atlas.AppHost        # 应用后端 http://localhost:5002
dotnet restore
```

### 前端（pnpm monorepo）
```bash
cd src/frontend
pnpm install                    # 安装所有 workspace 依赖
pnpm run dev:app-web            # AppWeb 开发服务器 http://localhost:5181
pnpm run dev:app-web:platform   # AppWeb 以平台代理模式启动
pnpm run dev:app-web:direct     # AppWeb 以直连模式启动
pnpm run build                  # 构建所有前端项目
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
- 使用宿主对应的 `.http` 文件：
  - `src/backend/Atlas.PlatformHost/Bosch.http/`
  - `src/backend/Atlas.AppHost/Bosch.http/`
- 每个新增或修改的接口需在对应 Host 下创建或更新 `.http` 文件

## 编码规范与约定

- **文档：** 标题层级连续（`#`、`##`、`###`），短句、 bullet 列表；文件名与现有模式一致（如 `等保2.0要求清单.md`）。
- **.NET：** 4 空格缩进，PascalCase 类型/公开成员，camelCase 局部变量/字段；File-scoped namespaces；启用 Nullable reference types。
- **AppWeb / React Packages（React/TSX）：** 2 空格缩进，优先沿用现有 kebab-case 页面/路由文件命名与 PascalCase 组件导出；hooks 使用 `useXxx`，context 使用 `XxxContext`；TypeScript 严格模式，禁止 `any`。
- **安全与设计：** 强调安全编码与 OOP；优先清晰、可测试的抽象；避免过度抽象与不必要的模式。
- **异步与仓储：** 所有 I/O 必须 async/await；控制器不得直接访问数据库，必须通过 Repository 与 Service。
- **注释规范：** 禁止无上下文 TODO（必须包含需求/工单号与处理条件）；注释应优先说明“为什么”与约束背景，而非重复代码“做了什么”。
- **前端分层：** 新增跨页面、跨壳、跨模块能力时，优先沉淀到 `src/frontend/packages/*`，`apps/*` 仅负责宿主装配、路由、页面编排与环境适配。

完整约定见 `CLAUDE.md` 的 Coding Standards 章节。

## 文档驱动开发

- **开发方式：** 按文档驱动实施，先有产品架构清单，再针对每个小需求完整跟踪实现。
- **需求文档：** `docs/plan-*.md` 为实施计划；如存在专题说明或迁移文档，优先结合 `docs/coze/`、`docs/coze-workflow-migration.md`、`docs/contracts.md` 一并阅读。
- **Plan 模式：** 需求需拆分为前端与后端实现计划，小步慢跑完成；每个任务需可闭环。
- **任务拆分：** 将需求梳理为很小的 case，每个 case 可独立完成并验证；实现过程中须满足等保要求。
- **完整性：** 按要求文档完成所有任务，确保前后端、契约、测试文件同步更新。
- **新增功能：** 先核对当前代码、契约文档与实施计划，再按最小闭环补齐实现、测试、`.http` 示例与文档。

## 开发约束

- **零警告：** 构建必须 0 错误 0 警告（由 `Directory.Build.props` 约束）。
- **修改前：** 必须先阅读目标文件，理解既有模式后再做最小化修改。
- **新增文件：** 必须将新文件加入对应项目文件（`.csproj`），并解决所有警告。
- **实现顺序：** 先实现底层代码，再实现引用层代码。
- **避免过度设计：** 仅实现所需功能，不添加未要求的能力。
- **解题原则：** 解决问题时优先从架构、边界、契约与复用层面处理，先消除系统性问题，再做局部修补；用博士/研究生式思维解题，先定义问题、提出假设、收集证据、验证结论，再决定实现方案，避免只盯单个报错点钻牛角尖。
- **国际化检查：** 新开发和更新现有功能时，必须检查并遵循国际化（i18n）实践；禁止硬编码面向用户的文案、日期/时间/数字/货币格式与区域相关内容，需统一走可本地化资源、配置或既有国际化机制，并同时关注中英文等多语言展示、回退文案与区域差异。

### 前端界面语言与排查

- 语言持久化在浏览器 `localStorage` 键 **`atlas_locale`**，取值为 **`zh-CN`** 或 **`en-US`**（实现见 `src/frontend/apps/app-web/src/app/i18n.tsx`）。
- **中英混杂**：先确认 `atlas_locale` 与语言切换器一致；再确认线上/本地使用的是否为最新 **`pnpm run build`** 产物（避免旧 bundle 缺少新版词条）。
- **中英键对齐**：在各应用目录下对比 `zh-CN.ts` / `en-US.ts` 是否同步更新，避免新增 key 漏翻。
- **当前前端形态**：现有前端以 React / TSX 为主；排查 i18n 时优先检查 `useAppI18n`、消息表和 `packages/*` 中的共享文案，而不是沿用历史 Vue 审计路径。

## 前后端约束

- 后端：禁止反射、动态类型/`dynamic`、运行时编译或表达式树生成等弱类型特性；必须使用强类型 DTO、实体、配置对象和接口，所有公共 API 输入输出都需显式类型声明与验证。
- 后端：后台接口操作数据库时不允许在循环内执行数据库操作；优先使用批量查询、批量更新、批量删除，并通过字典或集合聚合减少往返次数。
- 前端：禁止使用 `any`、`unknown` 或运行时 `eval`/动态注入脚本；必须使用 TypeScript 全量类型标注，组件 props/emit/状态均需强类型定义，API 客户端与接口契约保持类型对齐。
- 前端：搜索下拉框默认展示 20 条结果，必须提供搜索框并支持远程检索。
- 前端：跨模块共享协议、类型、宿主桥接与能力注册逻辑，优先沉淀到 `shared-react-core`、`schema-protocol`、`app-shell-shared` 等 workspace 包，避免在 `apps/*` 中重复定义。
- 前后端：遇到缺陷、重复逻辑、边界不清或频繁补丁时，优先检查架构分层、契约设计、复用边界和数据流；先做系统性诊断与验证，再做局部修补，而不是只对单点现象临时打补丁。
- 合同：前后端共享的数据契约需集中于 `docs/contracts.md` 并保持与实现同步，修改契约时同步更新类型定义与相关校验。

## API 测试文件

- 每个新增或修改的 API 端点需在对应 Host 下创建或更新 `*.http` 文件（`*` 为控制器名，如 `Bosch.http`）。
- `.http` 文件需包含覆盖受影响端点的请求示例。

## 控制器规范（RESTful + 版本控制）

- 控制器必须遵循 RESTful 风格：资源名用复数、路径表示资源层级，HTTP 动词表达操作语义（GET/POST/PUT/PATCH/DELETE）。
- 禁止在路径中使用动词（例如 `/create`、`/update`），改用标准动词与语义化路径。
- 统一 API 版本控制：所有 API 路由必须包含版本前缀（例如 `api/v1`）。新增版本时保持向后兼容或明确弃用策略。
- 版本并行策略：同一资源允许 `v1`/`v2` 并行存在，新增版本必须保持旧版本可用，除非明确进入弃用期。
- 弃用流程：发布新版本时同步标记旧版本为 Deprecated，并给出至少 6 个月的弃用窗口；窗口期内不再新增旧版本功能，但允许安全修复与关键缺陷修复。
- 终止策略：弃用窗口结束后方可移除旧版本路由，移除需在变更日志与发布说明中显式告知。

## 测试与验证

- **后端：** 使用 xUnit，测试项目位于 `tests/Atlas.WorkflowCore.Tests` 与 `tests/Atlas.SecurityPlatform.Tests`；接口验证仍需配套 REST Client `.http` 文件。
- **前端：** 使用 Vitest 进行单元测试、Playwright 进行 E2E 测试，并通过 `pnpm run i18n:check` 做词条完整性校验。
- **新增测试时：** 优先复用现有 xUnit / Vitest / Playwright 体系，记录命名模式（如 `*Tests.cs`、`*.spec.ts`）与运行命令。

## Workflow V2（Coze 复刻）补充约束

- WorkflowV2 引擎必须保持与 LogicFlow 表达式能力对齐，节点表达式统一通过 `NodeExecutionContext.EvaluateExpression()`。
- `workflow-editor-react` 是工作流画布与节点表单的单一前端实现，`app-web` 如需工作流编辑能力，优先复用该包，禁止复制分叉实现。
- DAG 运行时需保障以下能力长期可回归：
  - Batch 子图执行
  - Loop + Break/Continue
  - Selector 分支裁剪
  - Resume（基于 preCompletedNodeKeys）
- 前端工作流编辑器应维持“节点声明驱动 + 动态表单渲染”模式，避免将节点配置 UI 写死在单一组件中。
- 新增/修改节点能力时，必须同步更新：
  - 节点目录/模板 API
  - 前端节点面板分组与属性表单
  - i18n 中英文词条
  - 对应单测 / E2E / `.http` 示例
  - `docs/workflow-editor-validation-matrix.md`

## 提交与变更

- **提交信息：** 采用清晰约定（如 conventional commits：`feat:`、`fix:`、`docs:`）。
- **PR/变更：** 包含简要说明、关联需求、UI 变更需附截图。
- **架构变更：** 修改架构时需同步更新 `AGENTS.md` 与 `docs/contracts.md`。

## 安全与合规（等保2.0）

- 设计与实现须符合等保2.0 要求，安全控制为必选项；各功能需满足相关控制点并留有文档。
- 禁止在仓库中存放密钥；使用环境变量或安全密钥存储。
- SqlSugar + SQLite：实施最小权限数据访问，敏感字段按清单要求加密存储。
- 完整清单见 `等保2.0要求清单.md`；已实现安全控制见 `CLAUDE.md` 的 Security and Compliance 章节。

### 写接口安全基线（现行）

- 当前仓库已废止基于请求头的 `Idempotency-Key` 防重放机制。
- 当前仓库已废止基于 `X-CSRF-TOKEN` 的浏览器 Anti-Forgery 校验机制。
- 新增或修改写接口时，不得再把 `Idempotency-Key` / `X-CSRF-TOKEN` 作为公共前置要求写回实现、测试、`.http` 示例或契约文档。
- 如需重新引入等效保护，必须先补一份新的替代安全方案与契约说明，再整体落地，禁止局部回滚到旧机制。

## 表格视图（个人）支持

- 员工/角色/权限/菜单/部门/职位/项目/应用管理页面均已接入统一表格个人视图能力（见 `docs/contracts.md` “表格视图（个人）”章节）。
- 视图只绑定当前登录用户（后台以 `tenant_id + user_id` 识别，前端不可传递用户标识），对每个 `tableKey` 仅保存用户自己的视图与默认映射。
- `TableViewConfig` 支持列配置、密度、分页等项，相关 HTTP 测试存在于 `src/backend/Atlas.PlatformHost/Bosch.http/TableViews.http`。
- 默认配置由 `TableViewDefaultOptions`（`appsettings.json` 的 `TableViewDefaults` 节）定义，需要调整请同步更新后端配置与 `docs/contracts.md` 的描述。

## 登录与 UX 说明

- 当前仓库未固定 `docs/login-prd.md` 入口；涉及登录页或入口体验调整时，必须先检查 `src/frontend/apps/app-web` 的现有实现、对应 i18n 文案、认证接口与相关计划文档，再决定改动范围。

## Cursor Cloud specific instructions

### 系统依赖

- **后端：** 需要 .NET 10 SDK（`dotnet-sdk-10.0`），Ubuntu 24.04 可通过 `sudo apt-get install -y dotnet-sdk-10.0` 安装。
- **前端：** 需要 Node.js 22，并使用 `pnpm` 管理 `src/frontend` workspace 依赖；如环境缺少 `pnpm`，通过 Corepack 自动启用。
- **Cloud 预装：** 仓库提供 `.cursor/environment.json`，启动时执行 `scripts/cursor-cloud-install.sh`，自动校验 Node 22、执行 `dotnet restore`，并在 `src/frontend` 下运行 `pnpm install`。

### 服务概览

| 服务 | 端口 | 启动命令 |
|---|---|---|
| PlatformHost | 5001 | `dotnet run --project src/backend/Atlas.PlatformHost` |
| AppHost | 5002 | `dotnet run --project src/backend/Atlas.AppHost` |
| AppWeb 开发服务器 | 5181 | `cd src/frontend && pnpm run dev:app-web` |

`app-web` 默认以 `platform` 模式启动；如需直连 `AppHost`，使用 `cd src/frontend && pnpm run dev:app-web:direct`。

数据库为嵌入式 SQLite（`atlas.db`），无需外部数据库服务。Hangfire（`hangfire.db`）同样为嵌入式 SQLite 存储。首次启动时会自动创建数据库并初始化 BootstrapAdmin 账号。

### 后端启动

后端在 `Development` 模式下运行，配置来自 `appsettings.Development.json`。标准启动命令：

```bash
dotnet run --project src/backend/Atlas.PlatformHost
# 如需运行应用数据面服务，再启动：
dotnet run --project src/backend/Atlas.AppHost
```

### 开发默认账号

- **租户 ID：** `00000000-0000-0000-0000-000000000001`
- **用户名：** `admin`
- **密码：** `P@ssw0rd!`（由 `appsettings.Development.json` 的 `Security.BootstrapAdmin.Password` 配置）

### 测试

- 后端工作流测试：`dotnet test tests/Atlas.WorkflowCore.Tests`
- 后端单元/领域测试：`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"`
- 后端集成测试：`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~Integration"`（使用 `WebApplicationFactory`，需正确配置 Hangfire SQLite）
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
- 必须遵循现有分层与边界：
  - Controller / 页面层只负责编排，不直接落数据库、不直接写业务核心逻辑。
  - 数据访问必须通过 Repository / Service。
  - 前端宿主 `apps/*` 只做装配、路由、页面编排与环境适配，共享能力优先沉淀到 `packages/*`。
- 优先做最小化修改，保持 diff 可审查、可回滚、可验证。
- 遇到问题先做系统性诊断：先看架构、边界、契约、复用与数据流，再决定是否局部修补。
- 每完成一个阶段都必须验证：
  - 后端改动至少执行相关 `dotnet build` / `dotnet test`
  - 前端改动至少执行相关 `pnpm run build` / `pnpm run test:unit` / `pnpm run i18n:check`
- 不得声称“已完成”“已修复”“可用”，除非已实际完成对应验证。
- 新增或修改 API 时，必须同步更新对应 `.http` 文件、相关契约文档与必要测试。
- 修改前后端共享契约时，必须同步更新 `docs/contracts.md` 与类型定义。
- 新增用户可见文案必须走 i18n，禁止硬编码。
- 如无法完整完成，必须明确说明阻塞点、已完成部分、风险与下一步建议，不得伪造结果。

## 长任务执行规则

- 长任务必须先拆分为多个里程碑，按“分析 → 实施 → 验证 → 自动进入下一里程碑”的方式闭环推进。
- 开始编码前，必须先输出：
  - 任务理解
  - 范围边界
  - 里程碑拆分
  - 涉及文件
  - 验证方式
- 每个里程碑都必须遵循：
  - 先做最小可行实现
  - 完成后立即执行相关构建、测试、i18n 校验或接口验证
  - 记录修改文件、关键改动、验证结果
- 当前里程碑验证通过后，默认自动进入下一个里程碑继续执行，不因阶段性完成而中断。
- 只有在以下情况才停止并汇报：
  - 遇到明确阻塞，无法继续推进
  - 继续执行会违反现有架构、契约、安全或本文件约束
  - 需求本身存在冲突，继续实现会产生错误结果
- 如发生阻塞，必须明确说明：
  - 阻塞点
  - 已完成部分
  - 未完成部分
  - 风险
  - 建议下一步
- 不得把长任务只完成一部分就当作整体完成；除非所有里程碑完成并通过验证，否则不得宣称任务完成。
- 最终必须输出：
  - 里程碑完成情况
  - 修改文件清单
  - 执行过的命令
  - 验证结果
  - 剩余风险与后续建议
