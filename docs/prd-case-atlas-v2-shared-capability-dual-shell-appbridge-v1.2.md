# PRD Case：Atlas v2 共享能力层 + 双壳同构 + AppBridge 控制面

## 文档信息

| 项 | 内容 |
|----|------|
| 版本 | v1.1（在 v1.0 基础上续写与补全） |
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
| v1.1 | 2026-04-09 | 续写：补充业务流程、页面级需求、状态模型、验收指标、发布策略与待定事项 |

---

## 11. 关键业务流程

### 11.1 导航投影生成流程

#### 目标
将能力定义、页面清单、权限与运行时入口统一投影为用户可见菜单，替换当前多套硬编码菜单来源。

#### 流程
1. 用户进入平台壳或应用壳。
2. 前端根据当前上下文请求对应导航投影接口。
3. 后端读取 Capability Manifest、页面清单、角色权限、运行态路由与导航覆盖配置。
4. 投影服务进行合并、排序、裁剪和分组。
5. 返回 Navigation Projection 给前端布局组件。
6. 前端只负责渲染，不再拼装权限逻辑。

#### 验收重点
- 不同用户、不同角色、不同应用实例返回不同菜单树。
- 菜单项权限不满足时不返回。
- 前端 Layout 中不允许继续维护长期硬编码菜单真相。

### 11.2 平台查看应用暴露数据流程

#### 目标
确保平台查看应用数据是“受控暴露”，不是直接穿透应用内部存储。

#### 流程
1. 平台管理员进入目标应用的“暴露面”页。
2. 平台读取该应用的 `AppExposurePolicy`。
3. 用户选择数据集并设置查询条件。
4. 系统校验平台权限、目标数据集是否暴露、字段是否允许返回、是否需要脱敏。
5. 查询通过 `IExposedDataQueryService` 或等价服务执行。
6. 返回分页结果并记录审计事件。

#### 验收重点
- 仅能查询暴露字段。
- 返回结果符合脱敏策略。
- 每次查询具备审计记录和追踪标识。

### 11.3 平台下发命令流程

#### 目标
统一平台对应用的控制方式，替代散落的临时接口调用。

#### 流程
1. 平台管理员在“命令中心”或业务页面发起命令。
2. 前端提交命令类型、目标应用、幂等键、原因、payload、是否 dry-run。
3. 后端进行权限校验、风险等级判断、幂等冲突判断。
4. 命令持久化为 `AppCommand`。
5. 根据实例模式分流：
   - Local Managed：走内部适配器直接执行。
   - Federated：进入待拉取命令队列。
6. 应用侧确认命令、执行命令并回写结果。
7. 平台查看状态流转、执行摘要与审计链路。

#### 验收重点
- 相同幂等键下命令不可重复生效。
- 命令状态清晰可追踪。
- 高风险命令支持 dry-run 和差异预览。

### 11.4 联邦应用注册与心跳流程

#### 目标
使远程或独立部署的应用实例也能接入平台统一治理。

#### 流程
1. 联邦应用启动后向平台注册。
2. 平台生成或确认桥接身份。
3. 应用定时上报心跳、版本、健康、支持命令列表。
4. 平台更新在线状态投影。
5. 平台需要下发命令时，应用通过 pull 机制拉取待执行命令。
6. 命令执行完成后回写 ack/result。

#### 验收重点
- 联邦实例与本地托管实例在平台舰队页统一展示。
- 心跳中断后状态自动变化为 Degraded/Offline。
- 命令状态链路一致。

---

## 12. 页面级需求清单

### 12.1 平台页：在线应用中心

#### 页面目标
集中查看所有应用实例在线状态、健康、版本、活跃会话与运行中任务。

#### 核心功能
- 实例列表
- 健康状态筛选
- 版本筛选
- 本地/联邦模式筛选
- 应用详情抽屉或详情页
- 进入命令中心
- 进入暴露面页
- 进入平台侧共享能力页

#### 页面字段建议
| 字段 | 说明 |
|------|------|
| AppInstanceId | 应用实例标识 |
| AppKey | 应用标识 |
| Name | 应用名称 |
| BridgeMode | Local Managed / Federated |
| RuntimeStatus | Running / Degraded / Offline |
| HealthStatus | 健康状态 |
| ReleaseVersion | 当前版本 |
| LastHeartbeatAt | 最近心跳时间 |
| ActiveSessions | 活跃会话数 |
| RunningExecutions | 运行中执行数 |

#### 验收标准
- 本地托管应用与联邦应用可以统一展示。
- 状态变化在合理时间窗口内反映到页面。

### 12.2 平台页：应用暴露面管理

#### 页面目标
查看并管理应用对平台暴露的状态、数据集与命令能力。

#### 核心功能
- 查看暴露数据集列表
- 查看暴露命令列表
- 查看字段脱敏策略
- 查看导出能力开关
- 在授权范围内修改暴露策略

#### 验收标准
- 暴露数据集、暴露命令、掩码策略可视化呈现。
- 修改策略后可追踪修改人和生效时间。

### 12.3 平台页：数据浏览器

#### 页面目标
在平台侧安全、受控地浏览应用暴露数据。

#### 核心功能
- 数据集选择
- 条件筛选
- 分页查询
- 字段列裁剪
- 脱敏展示
- 导出申请或异步导出

#### 验收标准
- 默认分页，不允许一次性全量拉取。
- 无权限字段不可出现在查询结果中。
- 导出行为有明确审计。

### 12.4 平台页：命令中心

#### 页面目标
统一发起、追踪、回看平台对应用的命令。

#### 核心功能
- 选择目标应用
- 选择命令类型
- dry-run
- diff 预览
- reason 填写
- 审批状态查看
- 状态跟踪
- 结果摘要
- 错误详情与 trace 链路

#### 验收标准
- 至少支持组织、运行、发布三类命令最小闭环。
- 失败命令可以查看失败摘要与错误信息。

### 12.5 平台页：共享能力挂载页

#### 页面目标
以平台视角直接进入 Agent、Workflow、Organization、Data 等共享页面。

#### 核心功能
- 跨应用筛选
- 以当前应用上下文钻取
- 直接编辑或调试
- 返回平台列表而不丢失上下文

#### 验收标准
- 打开的页面与应用内是同一共享模块。
- 仅 host 上下文和导航壳不同。

### 12.6 应用页：应用工作台共享能力入口

#### 页面目标
保证应用构建者在应用内看到的是与平台同源的能力模块。

#### 核心功能
- Agent 管理与调试
- Workflow 设计与执行
- Data 表与视图管理
- Organization 工作台
- Runtime 调试
- Release 发布

#### 验收标准
- 同一模块在平台与应用壳下交互体验保持一致。
- 应用壳只裁剪为单应用上下文，不再复制建设新页面。

---

## 13. 领域对象与状态模型

### 13.1 Capability Manifest

#### 目标
作为能力注册、导航投影、双壳挂载的统一入口。

#### 必需字段
| 字段 | 说明 |
|------|------|
| capabilityKey | 能力唯一标识 |
| title | 能力名称 |
| category | 能力分类 |
| hostModes | 支持平台/应用挂载 |
| platformRoute | 平台路由模板 |
| appRoute | 应用路由模板 |
| requiredPermissions | 访问所需权限 |
| navigation | 菜单分组与排序建议 |
| supportsExposure | 是否可向平台暴露 |
| supportedCommands | 支持的命令类型 |

### 13.2 Navigation Projection

#### 目标
以投影形式承接菜单树，而不是作为独立授权源。

#### 必需字段
| 字段 | 说明 |
|------|------|
| hostMode | platform / app / runtime |
| scope | 租户、应用实例、版本等上下文 |
| groups | 分组导航树 |
| items | 导航项 |
| permissionCode | 对应页面或资源权限码 |
| sourceRefs | 投影来源引用 |

### 13.3 AppExposurePolicy

#### 目标
约束平台可看什么、可操作什么。

#### 必需字段
| 字段 | 说明 |
|------|------|
| appInstanceId | 目标应用实例 |
| datasets | 暴露数据集定义 |
| commands | 暴露命令定义 |
| maskPolicies | 脱敏策略 |
| allowExport | 是否允许导出 |
| auditLevel | 审计等级 |

### 13.4 AppCommand 状态机

#### 状态定义
| 状态 | 说明 |
|------|------|
| Pending | 已创建待分发 |
| Dispatched | 已分发 |
| Acked | 应用已确认接收 |
| Running | 执行中 |
| Succeeded | 执行成功 |
| Failed | 执行失败 |
| Cancelled | 已取消 |
| TimedOut | 超时未完成 |

#### 状态流转约束
- `Pending` 之后必须进入 `Dispatched` 或 `Cancelled`。
- 联邦模式下，`Acked` 与 `Running` 允许分离。
- 幂等键冲突时不可创建新有效命令。

### 13.5 OnlineAppProjection

#### 目标
统一平台舰队视图读取模型。

#### 必需字段
| 字段 | 说明 |
|------|------|
| appInstanceId | 应用实例 ID |
| appKey | 应用标识 |
| bridgeMode | 本地托管 / 联邦 |
| runtimeStatus | 运行状态 |
| healthStatus | 健康状态 |
| releaseVersion | 当前版本 |
| lastSeenAt | 最后可见时间 |
| supportedCommands | 支持命令 |
| exposuresSummary | 暴露面摘要 |

---

## 14. 产品指标与验收标准

### 14.1 业务指标

| 指标 | 目标 |
|------|------|
| 平台侧可直达共享能力域数量 | 首期至少 2 个，目标 6 个以上 |
| 平台可见在线应用覆盖率 | 本地托管 100%，联邦试点应用 100% |
| 平台发命令闭环成功率 | 首期大于 95% |
| 平台查询暴露数据审计覆盖率 | 100% |

### 14.2 功能验收标准

| 项 | 验收标准 |
|----|----------|
| 共享层 | 至少一个共享页面可在 platform/app 两壳复用 |
| 导航投影 | Layout 不再承担主菜单真相逻辑 |
| 在线应用 | 平台可查看在线、健康、版本、活跃度 |
| 暴露面 | 平台可查看应用暴露数据集和命令能力 |
| 命令中心 | 至少 3 类命令类型形成闭环 |
| 运行/发布 | 发布切换后菜单与运行入口一致 |

### 14.3 安全验收标准

| 项 | 验收标准 |
|----|----------|
| 跨应用查询 | 无权限用户无法查询暴露数据 |
| 脱敏 | 敏感字段按策略脱敏 |
| 命令权限 | 无权限用户不可发命令 |
| 幂等 | 相同幂等键不会重复生效 |
| 审计 | 查询和命令行为均有审计记录 |

### 14.4 稳定性验收标准

| 项 | 验收标准 |
|----|----------|
| 心跳异常 | 指定窗口后能正确标记 Degraded/Offline |
| 命令失败 | 平台可见失败状态与错误摘要 |
| 导航缓存 | 权限变化后可触发失效刷新 |
| 联邦适配 | 联邦与本地两种模式返回一致结构结果 |

---

## 15. 发布与版本策略

### 15.1 发布原则
- 共享层能力变更必须有版本记录。
- 影响导航结构的变更需要同步发布导航快照。
- 影响暴露能力的变更需要同步发布暴露目录快照。

### 15.2 兼容原则
- 旧菜单逻辑允许短期兼容，但不作为长期主线。
- Runtime 旧入口允许过渡，但必须可代理到新投影模型。
- 旧平台页允许暂时保留，但必须制定迁移下线计划。

### 15.3 灰度策略
- 可按租户、应用实例、能力域、hostMode 做灰度。
- 命令中心高风险命令默认先开给受控白名单。
- 联邦模式先在试点应用验证，再扩大范围。

---

## 16. 研发协作与交付要求

### 16.1 前端交付要求
- 所有共享能力页必须沉淀到共享目录，不允许平台和应用各自拷贝开发。
- Layout 中不得新增新的硬编码主菜单真相。
- 所有新页面必须声明对应 capabilityKey 或路由归属。

### 16.2 后端交付要求
- 导航、暴露、命令、在线应用投影必须提供稳定 API。
- 命令状态、审计事件、错误摘要必须统一。
- Local Managed 与 Federated 必须共享 DTO 与状态模型。

### 16.3 测试交付要求
- 共享页双壳复用需有 E2E 覆盖。
- 命令幂等、暴露越权、导航裁剪需有集成测试。
- 联邦模式需有契约测试。

### 16.4 文档交付要求
- `.http` 请求样例与接口文档同步。
- 中英文国际化词条同步。
- 技术方案、PRD、实施计划版本号同步更新。

---

## 17. 待确认事项（Open Questions）

| 编号 | 问题 | 当前建议 |
|------|------|----------|
| OQ-01 | Capability Manifest 最终采用纯代码注册还是代码优先 + DB 覆盖 | 建议代码优先 + DB 覆盖 |
| OQ-02 | 导航覆盖允许到什么粒度 | 建议仅排序/隐藏/分组，不介入授权 |
| OQ-03 | 高风险命令审批是否首期内建 | 建议首期至少预留审批接口与状态位 |
| OQ-04 | 联邦模式命令采用 pull 还是 push | 建议首期采用 pull 主线 |
| OQ-05 | 平台数据导出是否首期开放 | 建议首期仅对白名单数据集开放 |
| OQ-06 | 平台侧共享页是否允许直接编辑 | 建议按能力域逐步开放，先从查看+低风险编辑开始 |

---

## 18. 最终结论

本 PRD 的核心结论不是“再补几个页面”，而是明确 Atlas / SecurityPlatform v2 的产品主线：

1. **先抽共享能力层**，统一能力描述、上下文与页面模块；
2. **再统一导航投影**，让平台、应用、运行态入口归一；
3. **再建设双壳同构能力**，保证同一能力可在平台与应用中直达；
4. **最后用 AppBridge 建立正式控制面**，让平台真正具备观察应用、查看暴露数据、下发命令、追踪结果的能力。

换句话说，v2 的产品目标不是“平台比以前多一些菜单”，而是：

**平台和应用开始围绕同一能力模块运行，平台第一次真正拥有对应用的受控治理能力。**


---

# 第二部分：产品—技术对齐补充（纳入架构评审版）

> 本部分用于把“统一编排内核 + 统一发布工件 + 统一运行时协议”的技术主线，正式纳入本 PRD 的评审范围。定位不是替代技术方案，而是把会影响产品边界、实施顺序、页面规划、验收标准、研发拆分的技术事实显式写入 PRD，避免产品文档与技术方案脱节。

## 19. 执行摘要（产品—技术一体版）

### 19.1 一句话目标

在现有 Atlas / SecurityPlatform 基础上，增量演进为一个：

**以共享能力层为中枢、以双壳同构为产品外观、以内外连接器分层为集成方式、以统一编排内核 + 统一发布工件 + 统一运行时协议为底座的企业级 AI Agent / Workflow / LowCode 平台。**

### 19.2 三条总原则

1. **不是重做平台，而是统一平台。**
   当前仓库的 AI、Workflow、LowCode、DynamicTables、Runtime、KB、Plugin 都已有基础，重点不是从零重建，而是收敛协议、执行、发布和治理。

2. **不是只做页面，而是先做运行与发布真相源。**
   所有设计态定义最终都应编译为可运行、可审计、可回滚的发布工件，运行态不直接读取可变草稿。

3. **不是平台单看应用，而是平台正式治理应用。**
   平台必须能跨应用看运行、看暴露、发命令、看结果，应用侧必须通过 AppBridge 受控纳入平台治理。

### 19.3 本 PRD 补充后的唯一主结论

除“共享能力层 + 双壳同构 + AppBridge 控制面”外，v2 还必须明确纳入以下三条底座约束：

- **统一编排内核（Orchestration Kernel）**
- **统一发布工件（ReleaseBundle）**
- **统一运行时协议（RuntimeManifest / RuntimeAction / RuntimeBinding）**

这三项不属于“纯技术实现细节”，而是会直接影响产品边界、菜单入口、发布流程、调试体验、治理能力与研发拆分的一级能力。

---

## 20. 现状与差距分析（补充版）

## 20.1 当前仓库的总体判断

当前系统状态可定义为：

**强基座、弱统一。**

也就是：

- 不是缺核心能力，而是能力已经较多；
- 不是没有运行时，而是运行时协议与入口有多条线；
- 不是没有编排能力，而是 Workflow / Team / RuntimeAction / AgentFlow 尚未统一；
- 不是没有治理，而是治理入口、发布边界、调试模型、观测模型还没有统一。

## 20.2 现有能力盘点（纳入 PRD 的产品判断）

| 能力域 | 当前判断 | 产品层结论 |
|---|---|---|
| AI Agent | 已有实体、会话、模型配置、知识绑定、插件执行、SSE 对话 | 可复用，重点补版本、发布、调试面板、trace 一致性 |
| Multi-Agent | 已存在多套模型与运行形态 | 必须收敛，不允许长期三套并存 |
| Workflow / DAG | 已有 draft/version/execution/debug/SSE/rollback 主线 | 是统一编排内核的最佳起点 |
| DynamicTables | 元数据、DDL、查询、权限、导入导出都较完整 | 坚决复用，不推倒重写 |
| LowCode / Runtime | 后端版本/环境已具备，前端 runtime 仍双轨 | 必须收敛到正式 RuntimeManifest 主线 |
| CEL 表达式 | 已形成后端引擎与前端预览链路 | 应升级为平台统一表达式语法 |
| Knowledge / RAG | 文档解析、分块、embedding、向量检索已打通 | 重点补生命周期、重排、多模态、治理态 |
| Plugin / Tool | OpenAPI 导入与执行已有 | 应升级为正式 Tool 平台 |
| Runtime 治理 | RuntimeContexts / RuntimeExecutions 已成形 | 继续增强，不另造运维台 |
| 观测与消息 | OTel、Outbox/Inbox、消息抽象已存在 | 升级基础设施，不推翻抽象 |

## 20.3 不建议推倒重写的部分

本 PRD 明确以下模块属于**“强资产，原则上不推倒”**：

- DynamicTables 全链路
- WorkflowV2 的持久化模型与运行 API
- Knowledge / RAG 服务链
- LowCodeApp / Page / Version / Environment 基础
- CEL 表达式引擎
- RuntimeContexts / RuntimeExecutions 控制台
- Outbox / Inbox / SqliteMessageQueue / Saga 抽象

## 20.4 必须重构的部分

| 模块 | 原因 | PRD 要求 |
|---|---|---|
| 多 Agent 三套模型 | 语义重叠、API 分裂、运行态分裂 | 必须收敛为单一主模型 |
| Page Runtime 双轨 | 新旧 runtime 入口并存 | 必须明确新主线、旧兼容窗口 |
| 画布技术路线分裂 | Vue Flow / X6 / LogicFlow 多线 | 必须统一底座 |
| 执行内核碎片化 | Workflow / Team / RuntimeAction 各自运行 | 必须统一编译产物与执行内核 |
| 发布工件缺失 | 运行态仍可能读 live config | 必须强制 release bundle 化 |
| 插件 / Secret 边界 | 工具执行与密钥治理未平台化 | 必须纳入治理与发布体系 |

## 20.5 与目标态的关键差距

这次补充后，v2 的差距不再只定义为“共享层和 AppBridge 缺失”，而是同时包括：

1. **执行内核未统一**
2. **发布工件未统一**
3. **运行时协议未统一**
4. **设计器画布未统一**
5. **平台治理面未统一**

---

## 21. 目标架构总览（补充版）

## 21.1 目标态四层结构

### 第一层：共享能力层
负责能力注册、导航投影、共享 UI、共享服务、权限与审计桥接。

### 第二层：双壳层
- Platform Console：面向平台治理、总览、直控、审计
- App Workspace：面向应用构建、配置、发布、调试

### 第三层：运行与发布层
负责：
- RuntimeManifest
- RuntimeAction
- RuntimeBinding
- ReleaseBundle
- RuntimeExecution
- RuntimeContext

### 第四层：统一编排内核
负责：
- Workflow
- AgentFlow
- Team / Multi-Agent
- RuntimeAction Orchestration
- 节点调度、重试、补偿、断点恢复、回放、trace

## 21.2 目标态核心对象

| 对象 | 作用 |
|---|---|
| CapabilityManifest | 共享能力注册 |
| NavigationProjection | 菜单与入口投影 |
| RuntimeManifest | 页面/运行态工件 |
| RuntimeAction | 声明式动作协议 |
| RuntimeBinding | 绑定协议 |
| OrchestrationPlan | 统一编排执行计划 |
| ReleaseBundle | 统一发布工件 |
| ToolManifest | 统一工具协议 |
| KnowledgeSnapshot | 知识库发布快照 |
| AppExposurePolicy | 应用向平台暴露策略 |
| AppCommand | 平台向应用命令 |

## 21.3 核心架构原则补充

| 原则 | PRD 层定义 |
|---|---|
| 设计态 / 发布态 / 运行态分离 | 设计可变、发布不可变、运行只读发布 |
| 统一编译后运行 | 各 DSL 保留，但统一编译为 OrchestrationPlan |
| 统一运行时协议 | 低代码页、原生页、动作、绑定、运行上下文都走同一协议 |
| 平台治理与应用运行分离 | 平台看治理，应用看业务，但共享同一能力层 |
| 插件化扩展 | 节点、工具、画布、渲染器、表达式函数均可扩展 |

---

## 22. 核心技术选型对产品边界的影响

> 本节不是做纯技术选型说明，而是明确哪些技术选型会影响产品规划、实施顺序与长期边界。

## 22.1 统一画布底座：X6

### 产品结论
- Workflow Designer
- Team / Multi-Agent Designer
- ERD / Dynamic Table Designer

最终都应收敛到一套统一画布底座，避免设计器交互和节点协议永久分裂。

### PRD 要求
- 不允许长期维持 Vue Flow、X6、LogicFlow 三线共存。
- 允许兼容期双轨，但目标态必须唯一。
- 统一画布后，属性面板、快捷键、撤销重做、复制粘贴、节点协议、历史差异、调试入口必须共用。

## 22.2 低代码 DSL 主线：AMIS + Runtime 协议

### 产品结论
AMIS 继续保留，但它只作为**页面 DSL 与渲染层的一部分**，不能直接承担整个平台运行时。

### PRD 要求
- AMIS 页面与原生 Vue 页面必须都落到 RuntimeManifest。
- 设计态和运行态必须解耦。
- 前端动作不能长期依赖硬编码注入，必须过渡到 RuntimeAction。

## 22.3 统一编排内核：基于 WorkflowV2 演进

### 产品结论
WorkflowV2 是现阶段最成熟的执行主线，应演进为统一编排内核，而不是继续让 Workflow、Team、AgentFlow、RuntimeAction 多内核并行。

### PRD 要求
- 保留各业务 DSL；
- 统一编译产物；
- 统一执行状态机、trace、调试、回放与恢复能力。

## 22.4 基础设施结论

| 能力 | 主选 | 对产品的影响 |
|---|---|---|
| 向量检索 | Qdrant | 知识库正式环境能力边界清晰，可支持 hybrid / filtering |
| 主消息总线 | RabbitMQ | 异步执行、索引构建、命令中心、通知中心可统一 |
| 主流式通道 | SSE | Chat / Workflow / Trace 产品交互更简单稳定 |
| 协同与双向通信 | SignalR | 只用于多人协同、presence、强双向场景 |
| 数据访问 | SqlSugar + Dapper | 继续复用现仓，不做 ORM 大迁移 |
| 本地编排/观测 | Aspire + OTel | 本地研发、联调、观测一致性更强 |

---

## 23. 前端完整方案补充要求

## 23.1 前端分层补充

PRD 除“共享能力层 + 双壳 UI”外，再新增以下前端层要求：

- `runtime-core`：运行时核心协议层
- `schema-protocol`：前后端共享协议层
- `canvas-x6`：统一画布包
- `orchestration-ui`：统一编排设计器壳
- `query-builder`：统一查询 AST 前端构建器
- `amis-adapter`：AMIS 与 Runtime 协议适配层

## 23.2 设计器产品形态补充

| 设计器 | 目标形态 |
|---|---|
| Agent Designer | 表单 + 资源绑定 + Playground + Trace |
| Workflow Designer | DAG + 属性面板 + 调试时间线 |
| Team Designer | 角色图 + handoff + context policy |
| ERD Designer | 关系图 + 迁移影响预览 |
| Query Builder | AST 图形化编辑 |
| Runtime Debug | Timeline + Var Snapshot + Replay |

## 23.3 表达式与绑定主线

### 表达式
CEL 子集应成为平台唯一表达式主线。

### 绑定
Binding 只描述：
- 来源
- 目标
- 转换
- 作用域

### 动作
动作由 RuntimeAction 描述，不再散落为页面硬编码逻辑。

## 23.4 表格与动态数据体验补充

PRD 新增要求：

- QueryGrid 必须成为跨 DynamicTable、LowCode、Runtime 的统一数据展示组件；
- Query Builder 必须与后端现有 QueryGroup / QueryRule AST 对齐；
- 字段级权限必须统一映射到列裁剪、只读与脱敏行为；
- 保存视图、列状态、批量操作必须平台级复用。

---

## 24. 后端完整方案补充要求

## 24.1 后端新增中枢域

在原有 Platform / AiPlatform / LowCode / DynamicTables 基础上，PRD 补充要求新增两类中枢域：

### A. Orchestration
负责：
- compiler
- scheduler
- checkpoint
- compensation
- execution state
- trace bridge

### B. Publication
负责：
- ReleaseBundle
- Activation
- Rollback
- Snapshot 管理
- 运行态读取发布工件

## 24.2 领域边界补充

| 领域 | 产品层要求 |
|---|---|
| Agent | 必须可发布、可调试、可回放 |
| Team | 必须统一模型，不允许永久三套 |
| Workflow | 必须支持版本、差异、回滚、恢复 |
| Runtime | 必须持有 execution 与 audit 模型 |
| LowCode | 必须产出运行工件 |
| DynamicTable | 必须支持 schema draft -> publish snapshot |
| Plugin | 必须发布为 ToolRelease |
| Knowledge | 必须有 index/version/snapshot |
| Publication | 必须成为统一发布中枢 |

## 24.3 执行内核补充要求

PRD 新增执行内核能力要求：

- 节点执行器注册机制
- 调度器
- 重试与超时
- 幂等
- 补偿
- 断点恢复
- 长任务异步化
- 节点级 trace

这些能力未来不仅服务 Workflow，也服务 AgentFlow、Team、RuntimeAction。

---

## 25. DynamicTable / Database / Memory 补充要求

## 25.1 动态表主线不变，但需要产品级补充

### 保持主线
- 元数据驱动
- 物理表主线
- SchemaDraft / PublishSnapshot
- FieldPermission
- CRUD / 导入导出 / 影响分析

### 增补要求
- 混合存储：物理列 + JSON 扩展列
- Query AST 全平台统一
- QueryBuilder 产品化
- Memory Facade 统一 Agent / Workflow / LowCode 访问
- 数据访问门面统一暴露给运行时与编排内核

## 25.2 Memory 统一要求

PRD 新增 `MemoryNamespace / MemoryEntry` 概念，面向：

- Session Memory
- Conversation Memory
- Execution Memory
- Long-term Memory
- Table-backed Memory

产品上必须避免每个能力域各自定义 memory 语义。

---

## 26. Workflow / Agent / Multi-Agent 统一编排补充要求

## 26.1 统一原则

不是把 Workflow、Agent、Team 的设计器做成一个页面，
而是：

- 领域 DSL 仍然存在；
- 编译产物统一；
- 执行内核统一；
- trace / debug / replay / resume 统一。

## 26.2 统一生命周期要求

节点生命周期必须统一为可治理状态机，至少包括：

- Ready
- Running
- WaitingExternal
- Retrying
- Succeeded
- Failed
- Cancelled
- Compensated
- Skipped

## 26.3 统一调试要求

PRD 新增统一调试体验要求：

- Test Snapshot 试运行
- 单节点 isolated run
- 执行时间线
- 变量快照
- 版本差异
- 执行回放
- 多 Agent 消息流
- Handoff timeline

---

## 27. Knowledge / RAG 补充要求

## 27.1 产品边界补充

知识库不只是一组文档上传能力，而应形成完整生命周期：

- 上传
- 隔离
- 解析
- 分块
- 向量化
- 索引
- 审核
- 发布
- 检索
- 重建
- 归档

## 27.2 正式环境要求

- 正式环境以 Qdrant 为主向量库；
- 本地/轻量环境可保留 SQLite 向量降级；
- 检索链路应支持 hybrid、filter、rerank、freshness 控制；
- KB 必须具备审核态与发布态，而不只是“导入后立即可用”。

## 27.3 与产品页面的关系

需要新增或增强：

- KB 状态页
- Chunk 检视页
- 检索调试页
- 索引重建页
- 发布/归档状态页

---

## 28. 插件 / 工具系统补充要求

## 28.1 产品主线升级

插件系统应从“可调试 HTTP 封装”升级为“正式 Tool 平台”。

## 28.2 必须纳入 PRD 的工具能力

- Tool Manifest
- SecretRef
- ToolRelease
- Quota
- Network Policy
- Audit
- Sandbox 执行边界
- 市场/审批/发布能力

## 28.3 页面与治理要求

| 页面 | 要求 |
|---|---|
| Tool 列表 | 支持版本、状态、来源、权限 |
| Tool 编辑 | 支持输入输出 schema、鉴权、网络策略 |
| Tool 调试 | 支持 masked 请求调试 |
| Tool 发布 | 生成 ToolRelease |
| Tool 审计 | 支持调用日志、失败统计、耗时统计 |

---

## 29. 运行时、发布、版本与环境补充要求

## 29.1 三态正式写入 PRD

| 状态 | 含义 |
|---|---|
| Draft | 可编辑定义 |
| Test Snapshot | 可运行、不可变、面向调试 |
| Published | 正式不可变工件 |

## 29.2 发布工件必须统一

ReleaseBundle 作为产品一级对象，至少包含：

- RuntimeManifestSet
- OrchestrationPlanSet
- ToolRelease 引用
- KnowledgeSnapshot 引用
- ResourceBindingSnapshot
- NavigationProjectionSnapshot
- ExposureCatalogSnapshot
- 签名信息

## 29.3 运行态边界强制要求

运行态不得直接读取：
- Draft 配置
- Live mutable config
- 未发布知识定义
- 未发布插件定义

运行态必须读取：
- 发布工件
- 测试快照（仅调试场景）

## 29.4 环境与回滚要求

- 环境激活是发布指针切换，而不是配置覆盖；
- 回滚是切回旧 release；
- 灰度发布按租户 / 应用 / 用户 / 渠道可扩展。

---

## 30. 安全、性能、工程化、DevOps、迁移与研发拆分补充要求

## 30.1 安全与合规补充

PRD 进一步要求以下能力必须作为平台基线：

- SecretRef，不存明文 secret
- Sandbox Worker，不在主宿主执行不受信代码
- egress allowlist
- Prompt / Tool / RAG 风险策略
- 数据访问与命令行为全链路审计

## 30.2 性能与可靠性补充

重点关注三类热点：

1. 大画布
2. 大表格与 QueryGrid
3. Workflow / Agent / RAG 的流式运行

对应要求：
- 节点懒渲染
- 服务端分页与列投影
- release / tool / model / KB 缓存
- checkpoint + resume
- MQ 持久化与异步 worker

## 30.3 工程化补充

必须增加以下测试类型：

- 协议回归测试
- 执行回归测试
- 对话回归测试
- 沙箱安全测试
- 联邦 AppBridge 契约测试

## 30.4 DevOps 与部署补充

### 产品侧明确部署策略
- 模块化单体优先
- 少量强隔离 worker 独立
- PlatformHost / AppHost 保持主业务宿主
- Aspire 用于本地/集成编排
- K8s / 容器用于正式环境

### 必须独立的 worker
- Ingestion Worker
- Execution Worker
- Sandbox Worker

## 30.5 迁移顺序正式纳入 PRD

为避免产品要求与实施顺序冲突，本 PRD 确认以下迁移主序：

1. 协议封板
2. 执行内核统一
3. Runtime 收敛
4. 画布统一
5. Multi-Agent 收敛
6. DynamicTable / QueryGrid / Memory 完整化
7. Governance / Release / Observability 完整化

## 30.6 研发拆分补充

研发应围绕五条主线组织，而不是按页面零碎拆活：

- 协议与共享层
- 执行内核
- Runtime
- Canvas / Studio
- 治理与发布

---

## 31. 综合后的最终结论

在保留第一部分 PRD 主线的基础上，本次补充后的**最终唯一主推荐**为：

**Atlas / SecurityPlatform v2 必须同时完成五件事：**

1. **抽共享能力层**：能力、菜单、上下文、共享 UI、共享服务统一；
2. **做双壳同构**：平台与应用都承载同一能力模块；
3. **建 AppBridge 控制面**：平台真正治理应用实例；
4. **建统一运行与发布主线**：RuntimeManifest + ReleaseBundle 成为运行真相源；
5. **建统一编排内核**：Workflow / Agent / Team / RuntimeAction 全部统一到同一执行底座。

更直白地说：

Atlas v2 不能只停留在“把菜单做出来、把平台页补出来、把连接器做出来”。
它必须进一步完成：

- 从多模型并存走向统一编排；
- 从运行时读配置走向运行时读发布工件；
- 从页面驱动开发走向协议驱动与工件驱动开发；
- 从应用孤岛走向平台可治理的应用网络。

因此，这份 PRD 在 v1.1 的基础上，已经从“产品功能需求文档”升级为：

**可直接支撑架构评审、产品立项、研发拆分、阶段排期与实施验收的 PRD v1.2。**

---

## 32. 建议后续输出物

基于本版 v1.2，后续建议直接衍生四份配套文档：

1. **PRD v2.0 排期版**：把能力拆成版本与迭代任务；
2. **页面清单与原型说明**：平台页、应用页、共享页逐页拆解；
3. **API / DTO / 权限码矩阵**：接口、对象、权限、审计一体清单；
4. **实施路线图**：按阶段列出模块、负责人、依赖与验收口径。
