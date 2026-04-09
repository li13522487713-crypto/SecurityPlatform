# PRD Case：Atlas v2 共享能力层 + 双壳同构 + AppBridge 控制面

## 文档信息

| 项 | 内容 |
|----|------|
| 版本 | v1.0（对应技术方案 v2 目标态的需求侧表述） |
| 状态 | 草案（待评审） |
| 关联技术方案 | `docs/analysis/unified-terminology-glossary-v1.md`、`docs/contracts.md`、分析文档 `sec-12-*` / `sec-13-*` |
| 合规基线 | 等保2.0 三级：身份鉴别、访问控制、安全审计、数据完整性 |

---

## 1. 背景与目标

### 1.1 背景

仓库已具备 **ApplicationCatalog → TenantApplication → TenantAppInstance → RuntimeContext → RuntimeExecution** 主业务链，以及资源中心、应用运行时监督、外部 API 连接器、Runtime 协议雏形等能力。当前主要矛盾已从「缺概念」转为：

- 平台壳（`platform-web`）与应用壳（`app-web`）能力重复建设、菜单与路由硬编码；
- 缺少显式 **共享能力层**，无法保证「同一能力、两处可直达操作」；
- 缺少 **平台—应用内部控制面（AppBridge）**，平台对应用的治理停留在零散接口与本地启停，难以形成暴露策略、命令、审计一致的闭环。

### 1.2 产品目标（一句话）

将 Atlas / SecurityPlatform 从「模块堆叠平台」演进为 **一套共享能力层 + 平台控制台与应用工作台双壳同构 + AppBridge 连接器控制面 + 统一运行/发布内核**，且 **以菜单可进入、可操作为能力落地的验收标准**。

### 1.3 业务目标

| 编号 | 目标 | 说明 |
|------|------|------|
| G1 | 共享层优先 | 协议、能力描述、UI 模块、服务投影先于两壳重复开发 |
| G2 | 导航投影为主线 | 菜单不由独立「菜单授权表」主导，由能力/页面/权限/发布/暴露等 **投影** 生成 |
| G3 | 双壳同构 | 同一能力模块同时支持 `hostMode=platform` 与 `hostMode=app`，平台侧具备总览、钻取、**直接编辑/调试/下命令**，而非仅跳转 |
| G4 | AppBridge 控制面 | 区分 **External Connector**（OpenAPI 等）与 **Internal AppBridge**；平台可感知在线应用、受控查看暴露数据、下发命令并追踪结果 |
| G5 | 合规内建 | 暴露查询、命令下发、跨应用操作全程可审计、可幂等、高风险可 dry-run/审批 |

### 1.4 范围

| In Scope | Out Scope（本 PRD 不单独定案，可另立专项） |
|----------|------------------------------------------|
| CapabilityManifest（能力注册）与 Navigation Projection（导航投影） | 第三方 IdP 单点登录全方案 |
| 共享前端包：`schema-protocol`、`capability-core`、`capability-ui`、`navigation-projection`、`appbridge-console` 等边界与 MVP 行为 | 画布引擎从 Vue Flow 到 X6 的完整迁移细节（见独立 PRD/计划） |
| AppBridge：注册、心跳、在线列表、暴露策略、命令模型、Local Managed / Federated 双模式 | 全量联邦部署网络拓扑与证书运营手册（可附录引用） |
| 平台页：在线应用、暴露面、数据浏览器（受控）、命令中心、组织/Agent/Workflow/Data 等 **共享页** 的平台挂载 | 向量库/消息队列品牌级选型变更（技术方案可定，本 PRD 只约束能力边界） |
| 与现有 `docs/contracts.md` v2 方向一致的 API 契约增量 | 推翻现有 DynamicTables / Runtime v2 已有主链 |

---

## 2. 术语与主链（与 glossary 对齐）

| 术语 | 用户可见含义 |
|------|----------------|
| ApplicationCatalog | 平台应用目录（可安装/开通的应用定义） |
| TenantApplication | 租户与目录应用的开通关系 |
| TenantAppInstance | 租户下可运行、可治理的应用实例 |
| RuntimeContext / RuntimeExecution | 运行上下文与执行记录（治理台） |
| Navigation Projection | 根据多源定义生成的 **导航树**（非授权主源） |
| AppBridge | 平台与租户应用实例之间的 **内部** 连接器控制面 |
| AppExposurePolicy | 应用向平台声明「可看什么数据、可执行什么命令」的策略 |
| AppCommand | 平台向应用下发的标准化命令（含幂等、审计） |

---

## 3. 用户角色与权限原则

### 3.1 角色（示例）

| 角色 | 典型职责 |
|------|----------|
| 平台运营 / 安全审计员 | 跨应用总览、在线与健康、审计与合规查看 |
| 平台管理员 | 目录、租户开通、实例、资源、命令下发（受权） |
| 应用构建者 | 单应用内设计、发布、调试 |
| 应用管理员 | 单应用组织、权限、连接器实例 |

### 3.2 权限原则（强制）

- **授权主源**：页面/资源权限（及现有 RBAC），**不是**独立 app-level 菜单表。
- **导航**：由 Navigation Projection 生成；允许 `NavigationOverride` 做排序、隐藏、分组，**不**作为授权源。
- **平台看应用数据**：必须同时满足 **平台权限 + AppExposurePolicy + 脱敏策略**。
- **平台下命令**：必须满足 **命令 ACL + 暴露声明 + Idempotency-Key + CSRF（浏览器）+ 审计**；高风险支持 reason / 审批 / dry-run（按命令类型配置）。

---

## 4. 功能需求

### 4.1 共享能力层（P0）

| ID | 需求描述 | 验收要点 |
|----|----------|----------|
| FR-SCL-01 | 提供 **能力注册**：每个能力有稳定 `capabilityKey`，并声明 `hostModes`（platform/app）、路由模板、所需权限、菜单分组建议 | 可从代码或配置加载；支持后续 DB 覆盖（与实现计划一致） |
| FR-SCL-02 | 提供 **CapabilityHostContext**：共享页面只依赖注入的上下文（租户、用户、`appInstanceId`/`appKey`、`permissions` 等），不硬编码壳差异 | 同一共享页在 platform-web 与 app-web 可挂载，行为随上下文变化 |
| FR-SCL-03 | 后端 **ICapabilityRegistry** / 等价能力：可枚举能力定义供投影与治理使用 | 有单元测试或契约测试覆盖关键字段 |

### 4.2 导航投影（P0）

| ID | 需求描述 | 验收要点 |
|----|----------|----------|
| FR-NAV-01 | **菜单由投影生成**：整合 CapabilityManifest、页面/路由清单、可用页面权限、Runtime 路由、连接器暴露项、（可选）导航覆盖 | `AppSidebar.vue` / `ConsoleLayout.vue` 不再作为主真相来源 |
| FR-NAV-02 | 提供 API：`GET /api/v2/navigation/platform`、`GET .../apps/{appInstanceId}/workspace`、`GET .../runtime`（路径与 `docs/contracts.md` 对齐或可评审变更） | 返回分组菜单树；权限裁剪后 **无越权入口** |
| FR-NAV-03 | 发布物包含 **NavigationProjectionSnapshot**（目标态）：发布切换后运行态菜单与定义一致 | 与 Release 流程绑定，里程碑可分阶段 |

### 4.3 双壳同构 — 共享 UI（P0→P1）

| ID | 需求描述 | 验收要点 |
|----|----------|----------|
| FR-DUAL-01 | 下列能力域在 PRD 上要求 **共享模块 + 双挂载**：Agent、Workflow、Knowledge、Dynamic Data、Organization、Connectors（外连）、Runtime、Release | 每域至少列明平台入口与应用入口；平台入口支持列表/钻取/进入同一编辑体验（非仅外链） |
| FR-DUAL-02 | 平台专属页可存在（如在线应用舰队、全局命令中心），但 **不得** 作为回避共享层的长期方案 | 架构评审可跟踪 |

### 4.4 AppBridge — 内部连接器（P0→P1）

| ID | 需求描述 | 验收要点 |
|----|----------|----------|
| FR-AB-01 | **与 External API Connector 分域建模**：文档、API 前缀、权限码、审计事件分离 | 新用户可从文档区分两类连接器 |
| FR-AB-02 | **在线应用**：平台可查询实例在线/健康/版本/心跳等 **统一投影**（本地注册表 + 联邦心跳注册表） | 舰队列表与详情页或等价 API |
| FR-AB-03 | **AppExposurePolicy**：应用声明暴露的数据集、命令、脱敏与导出策略 | 平台「暴露面」可查看与（授权下）修改 |
| FR-AB-04 | **AppCommand**：创建、查询状态、结果；幂等键冲突策略与 `docs/contracts.md` / AGENTS 幂等规则一致 | 本地模式走内部派发；联邦模式走 pull/ack/result |
| FR-AB-05 | **平台数据查看**：仅允许通过 **IExposedDataQueryService**（或等价）访问暴露数据集 | 默认禁止「裸连应用库」作为产品能力宣传 |

### 4.5 命令中心（P1）

| ID | 需求描述 | 验收要点 |
|----|----------|----------|
| FR-CMD-01 | 支持命令分类：组织、权限、运行、发布、配置、数据、知识等（最小集可分期） | 每类至少 1 条示例命令可走通 |
| FR-CMD-02 | 高风险命令：`dry-run`、diff 预览、reason、审批（可配置） | 组织类 `replace-structure` 等需满足风险策略 |
| FR-CMD-03 | 审计事件：`app.command.dispatch`、`app.command.ack`、`app.exposure.query` 等可检索 | 满足等保审计追溯 |

### 4.6 与现有模块的关系（复用声明）

| 模块 | 需求 |
|------|------|
| ApplicationCatalogsV2 / TenantApplicationsV2 / TenantAppInstancesV2 | 继续作为主链，本 PRD 不要求改名 |
| ResourceCenterV2、ApiConnectorsController | 资源中心与 **外部** 连接器延续并增强 |
| AppRuntimeManagement、AppRuntimeSupervisorHostedService | 作为 **Local Managed** AppBridge 基础 |
| RuntimeContextsV2 / RuntimeExecutionsV2 | 运行治理与共享 Runtime UI 对接 |

---

## 5. 非功能需求

| ID | 类别 | 要求 |
|----|------|------|
| NFR-01 | 安全 | mTLS/短期令牌（联邦）、租户隔离、命令与暴露查询全链路审计 |
| NFR-02 | 性能 | 在线列表与导航投影可缓存；数据浏览分页、限频、异步导出 |
| NFR-03 | 可靠性 | 命令持久化优先；超时与重试策略可配置 |
| NFR-04 | 可维护性 | 共享层包独立版本与文档；Breaking change 走契约与迁移说明 |

---

## 6. 用户故事（摘选）

| 编号 | 故事 | 验收 |
|------|------|------|
| US-01 | 作为平台管理员，我希望在 **不进入应用壳** 的情况下看到所有应用在线状态与健康，以便快速处置故障 | 舰队视图 + 过滤；数据与现有监督能力一致或更好 |
| US-02 | 作为平台管理员，我希望在授权下 **查看应用暴露的组织数据**（脱敏后），以便治理 | 仅暴露字段；操作记审计 |
| US-03 | 作为平台管理员，我希望 **下发组织同步类命令** 并看到执行结果与 trace | 幂等、失败可诊断 |
| US-04 | 作为应用构建者，我希望 **同一套 Agent 编辑体验** 在应用内与平台「钻取进入」时一致 | 共享页 + host 上下文 |
| US-05 | 作为安全审计员，我希望检索 **谁对哪个应用下发了什么命令、查了哪些暴露数据** | 审计字典与报表或导出 |

---

## 7. 里程碑建议（与实施计划对齐）

| 阶段 | 目标 | 可交付验收 |
|------|------|------------|
| Phase 1 | 共享层骨架 + CapabilityRegistry + HostContext | 共享包可被两壳引用；无业务分叉 demo 页 |
| Phase 2 | Navigation Projection API + 替换硬编码菜单（可特性开关） | 平台/应用菜单来自 API |
| Phase 3 | 双壳共享页 MVP（1～2 个能力域） | 平台与应用同页异壳 |
| Phase 4 | AppBridge Local Managed：在线列表、暴露策略、命令闭环 | 本地实例全覆盖 |
| Phase 5 | Federated：注册/心跳/拉命令/回结果 | 契约测试 + 最小远程演示 |
| Phase 6 | ReleaseBundle 增强导航/暴露快照 | 发布与运行菜单一致 |

---

## 8. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 共享层抽取滞后导致双维护 | Phase 1 阻塞后续大范围页面开发 |
| 导航与权限混淆 | 文档与评审强制「授权源 ≠ 导航源」 |
| 平台直控导致越权 | Exposure + Command ACL + 分级审批 |
| 联邦与本地行为不一致 | 统一 DTO 与双 adapter 同源测试 |

**依赖**：`docs/contracts.md` 同步；新增/变更 API 需 `.http` 与前端类型对齐；国际化词条同步 `zh-CN` / `en-US`。

---

## 9. 附录：平台 / 应用信息架构（验收参考）

### 9.1 平台侧一级菜单（目标态）

概览中心、应用中心、能力中心、资源中心、运行中心、指令中心、安全治理（细化二级菜单见技术方案 v2，实施时以 Navigation Projection 为准）。

### 9.2 应用侧一级菜单（目标态）

仪表盘、Builder、AI Studio、Workflow、Data、Organization、Connectors、Runtime、Release、Settings（同上，以投影为准）。

---

## 10. 文档变更记录

| 版本 | 日期 | 说明 |
|------|------|------|
| v1.0 | 2026-04-09 | 初稿：从 v2 技术收敛结论抽取需求与验收 |
