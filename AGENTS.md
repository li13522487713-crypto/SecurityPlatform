# Repository Guidelines (AGENTS.md)

本文件为 AI 助理提供仓库级开发指南。详细技术说明见 `CLAUDE.md`。

**语言要求：** 所有助理回复必须使用中文。

## 项目概览

**Atlas Security Platform** — 符合等保2.0（GB/T 22239-2019）的安全支撑平台，采用 Clean Architecture，支持多租户与严格安全控制。

- 后端：.NET 10 + ASP.NET Core + SqlSugar + SQLite
- 前端：Vue 3 + Vite + Ant Design Vue + TypeScript
- 文档：`等保2.0要求清单.md`（等保要求）、`docs/contracts.md`（接口契约）、`docs/架构与产品能力总览.md`（架构与产品能力）

## 架构与目录

- 方案：`Atlas.SecurityPlatform.slnx`，代码位于 `src/backend/`、`src/frontend/`
- 分层：Core → Domain → Application → Infrastructure → WebApi
- 共享契约：`docs/contracts.md` 定义统一响应、分页与接口模型

完整结构与依赖说明见 `CLAUDE.md` 的 Architecture Overview、Project Structure 章节。

## 构建与开发命令

### 后端
```bash
dotnet build                                    # 必须 0 错误 0 警告
dotnet run --project src/backend/Atlas.WebApi   # API 运行于 http://localhost:5000
dotnet restore
```

### 前端
```bash
cd src/frontend/Atlas.WebApp
npm install
npm run dev      # 开发服务器 http://localhost:5173，代理 /api → localhost:5000
npm run build    # 生产构建（含 TypeScript 检查）
npm run lint
npm run format
```

### API 测试
- 使用 `src/backend/Atlas.WebApi/Bosch.http/` 下的 `.http` 文件
- 每个新增或修改的接口需创建/更新对应 `.http` 文件

## 编码规范与约定

- **文档：** 标题层级连续（`#`、`##`、`###`），短句、 bullet 列表；文件名与现有模式一致（如 `等保2.0要求清单.md`）。
- **.NET：** 4 空格缩进，PascalCase 类型/公开成员，camelCase 局部变量/字段；File-scoped namespaces；启用 Nullable reference types。
- **Vue/TS：** 2 空格缩进，组件文件 kebab-case（如 `login-page.vue`），组件名 PascalCase；TypeScript 严格模式，禁止 `any`。
- **安全与设计：** 强调安全编码与 OOP；优先清晰、可测试的抽象；避免过度抽象与不必要的模式。
- **异步与仓储：** 所有 I/O 必须 async/await；控制器不得直接访问数据库，必须通过 Repository 与 Service。

完整约定见 `CLAUDE.md` 的 Coding Standards 章节。

## 文档驱动开发

- **开发方式：** 按文档驱动实施，先有产品架构清单，再针对每个小需求完整跟踪实现。
- **需求文档：** `docs/plan-*.md` 为实施计划，`docs/prd-case-*.md` 为具体需求用例。
- **Plan 模式：** 需求需拆分为前端与后端实现计划，小步慢跑完成；每个任务需可闭环。
- **任务拆分：** 将需求梳理为很小的 case，每个 case 可独立完成并验证；实现过程中须满足等保要求。
- **完整性：** 按要求文档完成所有任务，确保前后端、契约、测试文件同步更新。
- **新增功能：** 完整步骤见 `CLAUDE.md` 的 Development Workflow 章节。

## 开发约束

- **零警告：** 构建必须 0 错误 0 警告（由 `Directory.Build.props` 约束）。
- **修改前：** 必须先阅读目标文件，理解既有模式后再做最小化修改。
- **新增文件：** 必须将新文件加入对应项目文件（`.csproj`），并解决所有警告。
- **实现顺序：** 先实现底层代码，再实现引用层代码。
- **避免过度设计：** 仅实现所需功能，不添加未要求的能力。

## 前后端约束

- 后端：禁止反射、动态类型/`dynamic`、运行时编译或表达式树生成等弱类型特性；必须使用强类型 DTO、实体、配置对象和接口，所有公共 API 输入输出都需显式类型声明与验证。
- 后端：后台接口操作数据库时不允许在循环内执行数据库操作；优先使用批量查询、批量更新、批量删除，并通过字典或集合聚合减少往返次数。
- 前端：禁止使用 `any`、`unknown` 或运行时 `eval`/动态注入脚本；必须使用 TypeScript 全量类型标注，组件 props/emit/状态均需强类型定义，API 客户端与接口契约保持类型对齐。
- 前端：搜索下拉框默认展示 20 条结果，必须提供搜索框并支持远程检索。
- 合同：前后端共享的数据契约需集中于 `docs/contracts.md` 并保持与实现同步，修改契约时同步更新类型定义与相关校验。

## API 测试文件

- 每个新增或修改的 API 端点需创建或更新对应的 `*.http` 文件（`*` 为控制器名，如 `Bosch.http`）。
- `.http` 文件需包含覆盖受影响端点的请求示例。

## 控制器规范（RESTful + 版本控制）

- 控制器必须遵循 RESTful 风格：资源名用复数、路径表示资源层级，HTTP 动词表达操作语义（GET/POST/PUT/PATCH/DELETE）。
- 禁止在路径中使用动词（例如 `/create`、`/update`），改用标准动词与语义化路径。
- 统一 API 版本控制：所有 API 路由必须包含版本前缀（例如 `api/v1`）。新增版本时保持向后兼容或明确弃用策略。
- 版本并行策略：同一资源允许 `v1`/`v2` 并行存在，新增版本必须保持旧版本可用，除非明确进入弃用期。
- 弃用流程：发布新版本时同步标记旧版本为 Deprecated，并给出至少 6 个月的弃用窗口；窗口期内不再新增旧版本功能，但允许安全修复与关键缺陷修复。
- 终止策略：弃用窗口结束后方可移除旧版本路由，移除需在变更日志与发布说明中显式告知。

## 测试与验证

- **当前：** 无单元测试框架，使用 REST Client `.http` 文件进行接口验证。
- **新增测试时：** 需记录框架（如 xUnit/NUnit 用于 .NET、Vitest 用于 Vue）、命名模式（如 `*Tests.cs`、`*.spec.ts`）及运行命令。

## 提交与变更

- **提交信息：** 采用清晰约定（如 conventional commits：`feat:`、`fix:`、`docs:`）。
- **PR/变更：** 包含简要说明、关联需求、UI 变更需附截图。
- **架构变更：** 修改架构时需同步更新 `AGENTS.md` 与 `docs/contracts.md`。

## 安全与合规（等保2.0）

- 设计与实现须符合等保2.0 要求，安全控制为必选项；各功能需满足相关控制点并留有文档。
- 禁止在仓库中存放密钥；使用环境变量或安全密钥存储。
- SqlSugar + SQLite：实施最小权限数据访问，敏感字段按清单要求加密存储。
- 完整清单见 `等保2.0要求清单.md`；已实现安全控制见 `CLAUDE.md` 的 Security and Compliance 章节。

### 幂等与防重放要求

- 关键写接口（创建/提交/开通/触发任务）必须要求客户端传 `Idempotency-Key`。
- 服务端以 `tenant_id + user_id + api_name + idempotency_key` 作为唯一键；首次成功后保存处理结果（状态 + 资源ID/响应摘要）。
- 重复请求应返回相同业务结果；同 key 不同 payload 必须拒绝并返回“幂等键冲突”。
- 幂等记录需按配置保留 N 小时/天后过期并清理。
- 受浏览器调用的写接口必须通过 Anti-Forgery 校验（Header: `X-CSRF-TOKEN`，由后端下发），前端需在写请求中携带。

## 表格视图（个人）支持

- 员工/角色/权限/菜单/部门/职位/项目/应用管理页面均已接入 Ant Design Vue `a-table` 的个人视图能力（见 `docs/contracts.md` “表格视图（个人）”章节）。
- 视图只绑定当前登录用户（后台以 `tenant_id + user_id` 识别，前端不可传递用户标识），对每个 `tableKey` 仅保存用户自己的视图与默认映射。
- `TableViewConfig` 支持列配置、密度、分页等项，所有写接口（POST/PUT/PATCH/DELETE 等）要求 `Idempotency-Key` + `X-CSRF-TOKEN`，相关 HTTP 测试存在于 `src/backend/Atlas.WebApi/Bosch.http/TableViews.http`。
- 默认配置由 `TableViewDefaultOptions`（`appsettings.json` 的 `TableViewDefaults` 节）定义，需要调整请同步更新后端配置与 `docs/contracts.md` 的描述。

## 登录页 UX 规范

- 登录页的详细结构与状态控制在 `docs/login-prd.md` 中记录，包含控件尺寸、校验规则、状态图、错误文案与多租户/组织切换行为，可直接给前端落地。
