# 对象定义冲突盘点（SEC-29）

> 版本：v0.1  
> 产出日期：2026-03-16  
> 任务来源：Linear `SEC-29`  
> 目的：仅做证据盘点，不做术语裁决；为 `SEC-19` 冲突矩阵与 `SEC-31` 术语词汇表提供输入。

## 1. 文档来源表

| 文档名 | 章节/位置 | 原始对象名 | 当前定义摘要 | 相关层级 |
|---|---|---|---|---|
| `docs/架构与产品能力总览.md` | `2.3`、`2.4`、`4.7`，L76-L81、L94-L97、L238-L241 | Workflow / Projects / Apps / TenantDataSources / Tenant-App | 将 `Workflow`、`Projects`、`Apps`、`TenantDataSources` 视为平台领域对象；其中 `Apps` 被描述为"租户-应用开通"，`TenantDataSources` 被描述为"租户-应用级 SQL Server 连接"；多租户扩展支持 `Tenant-App` 订阅、项目模式、多数据源。 | 平台 / 租户 / 应用 / 运行 |
| `docs/多租户多应用.md` | "范围定义（对象与关系）""关系模型""租户-应用开通""项目管理"，L17-L43、L93-L103、L175-L208、L262-L268 | Tenant / Application / TenantApplication / Project / DataSource | 明确区分 `Application` 与 `TenantApplication (Tenant-App)`；`Tenant-App` 是租户开通应用后的订阅关系；`Project` 是应用内业务实体，不是数据源维度；`DataSource` 绑定到 `Tenant-App`。 | 平台 / 租户 / 应用 / 发布 |
| `docs/plan-平台控制台与应用数据源.md` | "目标""核心约束""信息架构""数据模型""应用创建向导"，L20-L24、L34-L52、L64-L95、L106-L167、L185-L206 | Platform Console / App Workspace / LowCodeApp / DataSource / AppEntityAlias | 定义了"平台控制台"和"应用工作台"两层界面；`LowCodeApp.DataSourceId` 在应用创建时绑定且不可变；`TenantDataSource` 有平台级与应用专属两类；应用还承载共享策略与实体别名。 | 平台 / 应用 / 工作台 / 发布 |
| `docs/coze-studio-feature-atlas.md` | "空间与工作区""Agent Bot 开发""项目 IDE""工作流""探索广场"，L29-L35、L43-L52、L56-L67、L71-L87、L150-L158 | Space / Agent / Project / App / Workflow / Marketplace | `Space` 是用户进入开发与资源库的工作空间；`Agent` 是独立草稿/发布对象；`Project / App` 通过 `project-ide` 管理；`Workflow` 是可运行、可调试、可发布对象；`Marketplace` 是探索广场。 | 工作台 / 应用 / 运行 / 发布 |
| `docs/coze-studio-feature-inventory.md` | `2.2`、`2.3`、`2.5`、`2.7`、`2.14`，L58-L68、L72-L92、L121-L145、L173-L199、L303-L316 | Space / Agent / Workflow / Knowledge / Marketplace | 进一步把 `Space` 定义为开发列表与资源库入口；`Agent`、`Workflow`、`Knowledge`、`Marketplace` 都是一级业务对象，且各自拥有独立路由、能力与 API。 | 工作台 / 应用 / 运行 / 发布 |
| `docs/coze-studio-tech-stack-analysis.md` | 前端包结构，L99-L103 | Space / Workspace / Agent IDE | 从技术栈与包结构角度，把 `foundation`、`studio`、`agent-ide` 分成不同层；其中 `studio` 明确带有 `Workspace` 概念。 | 工作台 / 应用 |
| `docs/coze-studio-project-cognitive-map.md` | 目录结构、前端路由，L100-L107、L224-L234 | foundation/space / studio/workspace / AgentIDE / ProjectIDE / WorkflowPage | 前端结构把 `Space`、`studio/workspace`、`AgentIDE`、`ProjectIDE`、`WorkflowPage` 拆成不同包与入口，说明"空间""工作台""IDE""工作流编辑器"不是同一层对象。 | 工作台 / 应用 / 运行 |

## 2. 冲突证据表

| 对象名 | 冲突类型 | 冲突说明 | 证据来源 A | 证据来源 B |
|---|---|---|---|---|
| 应用 / App / Application | 同名异义 / 层级冲突 | 在 `架构与产品能力总览` 中，`Apps` 接近"租户-应用开通"能力；在 `多租户多应用` 中，`Application` 是平台提供的业务应用，而 `Tenant-App` 才是开通关系；在 `plan-平台控制台与应用数据源` 中，应用又变成带 `DataSourceId`、共享策略、实体别名的 `LowCodeApp`；在 Coze 文档中，`App/Project` 还是 Space 下的创作资产与 IDE 入口。当前"应用"同时承担了产品目录、租户订阅、运行载体、创作项目四种语义。 | `docs/架构与产品能力总览.md:L80-L81,L239-L241` | `docs/多租户多应用.md:L19-L25,L33-L39`；`docs/plan-平台控制台与应用数据源.md:L34-L52,L106-L167`；`docs/coze-studio-feature-atlas.md:L56-L67` |
| Tenant-App / TenantApplication | 边界缺失 | `多租户多应用` 把 `Tenant-App` 定义为核心订阅对象，并承载状态、数据源、启停；但 `plan-平台控制台与应用数据源` 的关系图与数据模型中，主关系变成 `Tenant -> LowCodeApp -> TenantDataSource`，没有同级保留独立的订阅对象，导致"租户开通记录"和"应用自身定义"被混写。 | `docs/多租户多应用.md:L21-L25,L93-L103` | `docs/plan-平台控制台与应用数据源.md:L161-L167,L185-L191` |
| 数据源 / DataSource | 同名异义 / 层级冲突 | 一组文档把数据源定义为 `Tenant-App` 级连接，随租户开通关系解析；另一组文档把数据源定义为应用创建时绑定的 `DataSourceId`，且允许平台级默认数据源与应用专属数据源并存。对象名相同，但所有权、生命周期和解析入口不同。 | `docs/架构与产品能力总览.md:L81,L241`；`docs/多租户多应用.md:L25,L37,L117-L175,L266-L268` | `docs/plan-平台控制台与应用数据源.md:L34-L35,L45,L106-L120,L162-L165,L185-L206` |
| 项目 / Project | 同名异义 | 在本平台文档中，`Project` 是应用内权限与数据范围隔离实体，不作为数据源维度；在 Coze 文档中，`Project / App` 是可创建、复制、发布并通过 `project-ide` 编辑的一级创作资产。若后续引入 Coze 术语而不区分，将把"项目域隔离"与"项目 IDE 资产"混为一体。 | `docs/架构与产品能力总览.md:L79,L240`；`docs/多租户多应用.md:L23,L39-L41,L206-L208` | `docs/coze-studio-feature-atlas.md:L56-L67`；`docs/coze-studio-project-cognitive-map.md:L227-L228` |
| 平台控制台 / 工作台 / Space / Workspace | 异名同义 / 层级冲突 | `plan-平台控制台与应用数据源` 定义了"平台控制台"和"应用工作台"两层入口；Coze 文档中，`Space` 本身就是开发与资源库的工作空间，而 `studio/workspace` 又是其中的工作台包。当前"控制台""工作台""Space""Workspace"缺少统一层级映射。 | `docs/plan-平台控制台与应用数据源.md:L20-L24,L64-L95` | `docs/coze-studio-feature-atlas.md:L29-L35`；`docs/coze-studio-feature-inventory.md:L58-L68`；`docs/coze-studio-tech-stack-analysis.md:L99-L103` |
| Workflow / 工作流 | 同名异义 | 本平台文档把 `Workflow` 作为通用工作流领域，强调定义、实例、事件；Coze 文档中的 `Workflow` 是 AI 工作流对象，包含画布、节点调试、测试运行、发布、ChatFlow 与 Open API 运行。名称一致，但产品语义、运行时能力和边界深度显著不同。 | `docs/架构与产品能力总览.md:L76,L201-L217` | `docs/coze-studio-feature-atlas.md:L71-L87`；`docs/coze-studio-feature-inventory.md:L121-L145` |
| Runtime / 运行态 | 边界缺失 | 当前平台文档大量出现"运行""运行时操作""运行监测"等词，但没有单独定义 `Runtime` 是业务对象、执行环境还是控制面；Coze 文档则把工作流执行、节点调试、ChatFlow 运行、Eino 运行时明确落到具体能力。`Runtime` 目前是高频词但非收敛术语。 | `docs/架构与产品能力总览.md:L155,L204-L207` | `docs/coze-studio-feature-atlas.md:L77-L85,L87`；`docs/coze-studio-project-cognitive-map.md:L103-L105,L232-L233` |
| Agent | 边界缺失 | Coze 文档把 `Agent` 定义为独立草稿、发布、在线运行对象，并可绑定知识库、数据库、工作流；当前平台主文档没有对应一级对象定义。若后续继续以 Coze Studio 为参照，`Agent` 需要明确是"应用子资源"还是"一级产品对象"。 | `docs/coze-studio-feature-atlas.md:L39-L52` | `docs/coze-studio-feature-inventory.md:L72-L92` |
| Knowledge / 知识库 | 边界缺失 | Coze 文档中 `Knowledge` 是独立对象，包含知识库、文档、切片、索引与 Open API；当前平台主文档没有与之同级的主对象定义。后续若接入 AI 能力，不先收敛术语，容易把知识库误并入文件、数据源或低代码资源。 | `docs/coze-studio-feature-atlas.md:L108-L119` | `docs/coze-studio-feature-inventory.md:L173-L199` |
| Marketplace / 市场 | 边界缺失 | Coze 文档把 `Marketplace` 定义为"探索广场"，承载产品浏览、搜索、收藏与复制到工作区；当前平台主文档没有对应主对象，后续若做模板/插件市场，需明确其与"应用列表""插件列表""资源库"的边界。 | `docs/coze-studio-feature-atlas.md:L150-L158` | `docs/coze-studio-feature-inventory.md:L303-L316` |

## 3. 收敛候选对象清单

| 对象名 | 为什么必须进入统一词汇表 | 关联对象 | 风险等级 |
|---|---|---|---|
| Tenant | 多租户隔离、权限校验、数据过滤都以它为根对象；后续所有对象层级都要挂靠到 Tenant 或明确不挂靠。 | Tenant-App、DataSource、User、Project | 高 |
| Application | 当前同时表示产品能力单元、低代码应用、创作项目、运行入口；不拆清会直接污染路由、API、导航与权限模型。 | Tenant-App、Project、Workspace、Agent | 高 |
| Tenant-App | 这是"开通/订阅/状态/可用性"最自然的承载体，但在部分文档中已被弱化或隐去，必须收敛。 | Tenant、Application、DataSource | 高 |
| DataSource | 涉及所有权、生命周期、密钥存储、运行时解析，且当前文档对归属层级不一致。 | Tenant-App、Application、Runtime | 高 |
| Project | 既可能是项目域隔离实体，也可能是 IDE 中的创作资产，需要明确是否拆成两个词。 | Application、Workflow、Knowledge、Agent | 高 |
| Space | Coze 参照体系里的外层工作空间对象；如果后续借鉴其 IA，需要先决定是否引入该词。 | Workspace、Project、Agent、Library | 中 |
| Workspace / 工作台 / 控制台 | 当前至少存在"平台控制台""应用工作台""studio/workspace""Space 下工作区"四种表达，必须统一映射。 | Console、Space、Application | 高 |
| Workflow | 本平台工作流与 Coze AI 工作流不是同一概念，至少需要命名限定词。 | Runtime、Agent、Project | 高 |
| Runtime | 是执行环境、运行实例、还是控制面对象尚未收敛；不统一会影响 API、监控、审计与错误模型。 | Workflow、Agent、DataSource | 高 |
| Agent | 若后续平台引入 AI Studio，这会成为一级对象；不提前定边界，容易被并入 App 或 Workflow。 | Knowledge、Workflow、Marketplace | 中 |
| KnowledgeBase | 与文件、数据源、文档解析、向量索引关系紧密，未来是高耦合术语。 | Agent、Document、Workflow | 中 |
| Marketplace | 关系到模板、插件、资源复制与分发边界，后续会牵动导航与权限模型。 | Plugin、Template、Workspace | 中 |

## 4. 一页结论摘要

- 当前最严重的问题不是"缺少对象"，而是**同一个词在不同文档里承担了不同层级**。其中最严重的是 `Application`、`DataSource`、`Project`、`Workspace`、`Workflow`。
- `多租户多应用.md` 与 `plan-平台控制台与应用数据源.md` 是当前最直接的冲突对：前者以 `Tenant-App` 为开通与数据源承载体，后者以 `LowCodeApp + DataSourceId` 为中心，导致订阅关系、应用定义、运行载体三者没有稳定分层。
- Coze 相关分析文档已经给出一套更细的对象网：`Space -> Project/App -> Agent / Workflow / Knowledge / Marketplace`。如果后续路线继续吸收 Coze Studio 能力而不先建立统一词汇表，现有"应用/项目/工作台"将继续扩张成多义词。
- `Runtime` 是本轮盘点中最明显的"高频但未定义"对象。它已经出现在运行时操作、执行、监控等多个语境里，但还没有统一对象边界。
- 若不先做术语收敛，后续会直接影响这些任务：`SEC-19` 冲突矩阵、`SEC-31` 词汇表起草、`docs/contracts.md` 契约命名、控制台路由与导航命名、数据源解析链路、权限模型与审计模型。

## 5. 建议的下一步

- 由 `SEC-31` 基于本表先起草一版"对象词汇表 v1"，至少覆盖 `Tenant / Application / Tenant-App / DataSource / Project / Workspace / Workflow / Runtime`。
- 在 `SEC-19` 父卡中补一张"层级矩阵"，把每个对象映射到"平台 / 租户 / 应用 / 工作台 / 运行 / 发布"六层。
- 对 `Application` 与 `Project` 优先做术语裁决，否则后续所有 Coze 对齐讨论都会持续漂移。
