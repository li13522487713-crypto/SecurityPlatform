# 统一术语词汇表 v1（SEC-31）

> 版本：v1.0-draft  
> 产出日期：2026-03-16  
> 任务来源：Linear `SEC-31`  
> 输入基线：`docs/analysis/object-definition-conflicts.md`（SEC-29 证据盘点）

## 1. 目标与适用范围

- 本文用于收敛平台主对象命名，降低 `Application / Project / Workspace / Workflow / Runtime` 等多义词冲突。
- 本文是术语草案，服务于 `SEC-19` 冲突矩阵、后续 `docs/contracts.md` 命名统一、控制台信息架构与 API 命名对齐。
- 本文只定义对象语义与层级边界，不包含数据库表设计与 API 字段级变更。

## 2. 分层模型（v1）

- 平台层（Platform）：产品能力目录、平台控制台、全局治理与模板市场。
- 租户层（Tenant）：租户主实体、租户开通关系、租户级权限与配额。
- 应用层（Application）：被租户开通后可运行的业务应用实例。
- 工作台层（Workspace）：面向创作与配置的交互空间（Studio/IDE）。
- 运行层（Runtime）：任务执行、编排运行、调试与可观测。
- 发布层（Release）：版本发布、上线状态、可用性窗口。

## 3. 统一术语表（v1）

| 规范术语 | 英文名 | 所属层级 | 规范定义（v1） | 禁用/待淘汰别名 | 关联对象 |
|---|---|---|---|---|---|
| 租户 | Tenant | 租户 | 平台中的隔离主体，承载组织、用户、权限、配额与审计边界。 | 无 | TenantApplication、TenantDataSource |
| 应用目录项 | ApplicationCatalog | 平台 | 平台提供给租户可选开通的应用定义（产品目录），不直接代表租户已开通实例。 | Application（未加限定时） | TenantApplication、Workspace |
| 租户应用开通 | TenantApplication | 租户/应用 | 租户与应用目录项的订阅关系，承载开通状态、可用性与应用级配置入口。 | Tenant-App、AppSubscription | Tenant、ApplicationCatalog、TenantDataSource |
| 租户应用实例 | TenantAppInstance | 应用 | 租户开通后的实际运行载体，承载运行参数与资源绑定（如默认数据源、运行开关）。 | LowCodeApp（语义不完整时） | TenantApplication、RuntimeContext |
| 租户数据源 | TenantDataSource | 租户/应用 | 租户可管理的数据连接资源；可声明为租户共享或租户应用专属，并通过显式关系绑定到 TenantApplication。 | DataSource（未加层级限定时） | Tenant、TenantApplication、RuntimeContext |
| 工作空间 | Workspace | 工作台 | 面向创作与配置的工作域，承载页面导航、资产组织与 IDE 上下文，不等同于租户或应用目录。 | Space、Studio（泛称时） | ProjectAsset、Agent、WorkflowDefinition |
| 项目资产 | ProjectAsset | 工作台 | 工作空间内的业务创作资产容器，可包含应用页面、流程、知识与 Agent 配置。 | Project（未加限定时）、App（创作语境） | Workspace、WorkflowDefinition、KnowledgeBase |
| 智能体 | Agent | 工作台/运行 | 可独立草稿、调试、发布与运行的 AI 交互对象，可绑定知识库与工作流。 | Bot（若未定义） | KnowledgeBase、WorkflowDefinition、RuntimeContext |
| 工作流定义 | WorkflowDefinition | 工作台 | 可编辑、版本化、可发布的流程编排定义，不等同于运行中的执行实例。 | Workflow（未加限定时） | RuntimeExecution、Agent |
| 运行上下文 | RuntimeContext | 运行 | 执行前绑定的上下文（租户、租户应用、数据源、版本、权限快照）。 | Runtime（泛称时） | TenantAppInstance、TenantDataSource |
| 运行执行实例 | RuntimeExecution | 运行 | 一次具体的运行任务实例，包含状态流转、日志、指标与审计记录。 | Run、Execution（未受控别名） | RuntimeContext、WorkflowDefinition |
| 知识库 | KnowledgeBase | 工作台/运行 | 面向检索增强的结构化知识资源集合，包含文档处理、切片、索引与权限控制。 | Knowledge（未加限定时） | Agent、RuntimeExecution |
| 市场 | Marketplace | 平台/发布 | 模板、插件或资产的发现与分发场域，支持浏览、复制与引入工作空间。 | 探索广场（可保留展示名） | Workspace、ProjectAsset |

## 4. 同名词收敛规则

### 4.1 Application 收敛规则

- `Application` 在文档与接口中禁止裸用，必须显式指向：
  - `ApplicationCatalog`（平台目录定义）或
  - `TenantApplication`（租户开通关系）或
  - `TenantAppInstance`（租户应用运行实例）。
- 迁移顺序：先文档与图示，再 API 字段，再前端路由与菜单。

### 4.2 DataSource 收敛规则

- `DataSource` 禁止裸用，统一替换为 `TenantDataSource`。
- 当表达绑定关系时，统一使用 `TenantApplicationDataSourceBinding`（关系名，后续数据模型卡落实）。
- `DataSourceId` 若出现在应用创建流程，必须补充其归属层级（租户共享/租户应用专属）。

### 4.3 Project / Workspace 收敛规则

- 创作语境下使用 `ProjectAsset`，禁止与组织项目域隔离概念混用。
- `Workspace` 为规范术语；`Space` 保留为外部参考名，不作为平台主术语。

### 4.4 Workflow / Runtime 收敛规则

- `WorkflowDefinition`（定义态）与 `RuntimeExecution`（运行态）必须严格区分。
- `Runtime` 仅作为层名出现；对象命名必须使用 `RuntimeContext` 或 `RuntimeExecution`。

## 5. 术语映射与迁移清单（首批）

| 现有写法 | 目标写法 | 迁移优先级 | 说明 |
|---|---|---|---|
| Application | ApplicationCatalog / TenantApplication / TenantAppInstance | P0 | 必须按上下文拆分，禁止继续单词复用。 |
| Tenant-App | TenantApplication | P0 | 统一中英文写法，减少同义词扩散。 |
| DataSource | TenantDataSource | P0 | 先在文档统一，再推进接口命名。 |
| Project | ProjectAsset | P1 | 仅在创作语境替换；组织项目域另行命名。 |
| Space / 工作区 / 工作台 | Workspace | P0 | 统一主术语，展示层可保留别名。 |
| Workflow | WorkflowDefinition / RuntimeExecution | P0 | 强制定义态与运行态拆分。 |
| Runtime | RuntimeContext / RuntimeExecution | P0 | 消除“高频但未定义”状态。 |
| Knowledge | KnowledgeBase | P1 | 统一为资源对象命名。 |

## 6. 落地约束（对后续任务）

- 新增文档（`docs/plan-*.md`、`docs/prd-case-*.md`）必须采用本词汇表术语。
- 变更 `docs/contracts.md` 时，不得引入裸词 `Application` / `DataSource` / `Workflow` / `Runtime`。
- 前后端新增 API、DTO、页面路由命名时，需标注“对应词汇表术语”。
- 若需新增术语，必须同时补充：定义、层级、关联对象、禁用别名、迁移影响。

## 7. 未决问题（需在后续卡确认）

- `TenantAppInstance` 是否保留独立对象，还是并入 `TenantApplication` 的实例态字段。
- `ProjectAsset` 与“组织项目域隔离对象”是否需要拆为两个规范名。
- `Marketplace` 是否作为平台一级对象进入首版导航。
- `Agent` 在本平台是否作为一级对象，或仅作为应用子资源。

## 8. 与 SEC-29 的衔接说明

- 本文术语收敛范围直接覆盖 SEC-29 标注的高风险对象：`Application`、`DataSource`、`Project`、`Workspace`、`Workflow`、`Runtime`。
- 本文不替代 SEC-29 的证据表；若术语定义与后续产品决策冲突，应回溯 SEC-29 证据并更新本词汇表版本。
