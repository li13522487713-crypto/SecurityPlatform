# Plan: SecurityPlatform 下一阶段产品化重构实施计划（12 Sprint）

> 本文档为产品化重构主追踪基线。  
> 原则：增量对齐既有规范与文档约束，采用全新实现代码路径，形成可售卖、可私有化、可迁移、可审计闭环。  
> 如与历史计划冲突，以 `docs/contracts.md` + 本文档最新版本为准。

---

## 一、摘要

- 目标：在保持既有规范与安全约束不变的前提下，全新重做“平台控制面 + 应用工作台 + 运行交付面 + 治理层”。
- 策略：采用“增量对齐现有基线（文档与约束）+ 全新实现代码路径”的方式，避免继续沿用已回退实现。
- 交付粒度：按里程碑拆到可执行任务、接口契约、验收用例，覆盖 P0/P1。
- 文档落点：
  - 更新 `docs/contracts.md`
  - 更新 `docs/plan-功能补齐总览.md`
  - 本文档 `docs/plan-产品化重构-12-sprint.md` 作为主追踪

---

## 二、关键实现变更（接口/类型/行为）

### 2.1 统一元模型

- `AppManifest`
- `AppRelease`
- `RuntimeRoute`
- `PackageArtifact`
- `LicenseGrant`
- `ToolAuthorizationPolicy`
- `FlowDefinition`（审批流与工作流统一挂载）

### 2.2 前端信息架构

- 平台入口：`/console`
- 应用入口：`/apps/:appId/*`
- 运行态入口：`/r/:appKey/:pageKey`
- 兼容入口：`/settings/*`（Deprecated，保留窗口期）

### 2.3 后端公共 API（`api/v1`）

- 平台面：
  - `/platform/overview`
  - `/platform/resources`
  - `/platform/releases`
- 应用面：
  - `/app-manifests`
  - `/app-manifests/{id}/workspace/*`
- 运行面：
  - `/runtime/apps/{appKey}/pages/{pageKey}`
  - `/runtime/tasks/*`
- 治理面：
  - `/packages/export|import`
  - `/licenses/offline-request|import|validate`
  - `/tools/authorization-policies|simulate|audit`

### 2.4 安全与一致性

- 所有写接口强制：`Idempotency-Key` + `X-CSRF-TOKEN`
- 明确并校验以下契约行为：
  - 幂等键冲突（同 key 不同 payload）
  - 跨租户访问拒绝
  - 敏感字段脱敏写入/返回

### 2.5 迁移与兼容

- 旧接口保留 6 个月弃用窗口，仅允许安全修复与关键缺陷修复。
- 新增 API 不复用旧 DTO，统一显式强类型 DTO + 强校验。

---

## 三、12 Sprint 实施编排

| Sprint | 核心目标 | 状态 |
|---|---|---|
| Sprint 1 | 重构基线冻结，统一元模型与领域边界定稿，接口命名与版本策略定稿，弃用清单发布 | [ ] |
| Sprint 2 | 后端骨架与数据库迁移（Manifest/Release/Policy/License/Package），仓储与服务层空实现及契约占位 | [ ] |
| Sprint 3 | 平台控制面 V1（平台首页、应用中心、资源中心）+ 对应聚合 API | [ ] |
| Sprint 4 | 发布中心与审计主链路（发布、回滚点、影响分析基础能力）+ 平台层 e2e | [ ] |
| Sprint 5 | 应用工作台 V1（概览、页面/表单、流程、数据、权限入口）+ 应用域 API | [x] |
| Sprint 6 | 统一设计器 V1（页面/表单/流程统一元数据存储），模板体系最小可用版本接入 | [ ] |
| Sprint 7 | 运行态 V1（`appKey/pageKey` 访问、任务/审批中心、发布态读取）+ 业务用户访问闭环 | [ ] |
| Sprint 8 | 流程闭环强化（审批流与工作流统一编排边界、状态回写、失败补偿、可观测性） | [ ] |
| Sprint 9 | 应用导入导出 V1（结构包/基础数据包/完整副本三模式、冲突检测与回滚） | [ ] |
| Sprint 10 | 离线 License 中心 V1（申请、签发、导入、校验、续期、审计），私有化场景验收 | [ ] |
| Sprint 11 | Tools Authorization Center V1（目录、策略矩阵、审批要求、限流配额、策略模拟、审计） | [ ] |
| Sprint 12 | 全链路硬化与发布门禁（Gate-R1/R2、性能与安全压测、弃用公告、上线文档与运维手册） | [ ] |

---

## 四、测试与验收计划

- 契约测试：
  - 所有新增/变更端点同步 `.http` 用例
  - 覆盖成功、鉴权失败、幂等冲突、跨租户拒绝
- 集成测试：
  - 覆盖平台 -> 应用 -> 运行态 -> 审批 -> 回写 -> 审计主链路
  - 覆盖导入导出/License/Tools 策略链路
- 前端 e2e：
  - 三层入口切换
  - 发布与回滚
  - 业务用户运行态访问
  - 任务处理与异常提示
- 安全测试：
  - CSRF
  - 幂等防重放
  - 敏感信息脱敏
  - 权限绕过与越权访问
- 验收指标：
  - 5 分钟模板建应用
  - 运行态可直接访问
  - 包可迁移
  - License 离线可校验
  - Tools 策略可审计可模拟

---

## 五、假设与默认决策

- 采用 12 Sprint（默认每 Sprint 2 周）节奏推进。
- P0 在 Sprint 1-8 完成，P1 在 Sprint 9-12 完成。
- “全新重做”定义为新模块/新接口/新路由重建，不直接恢复已回退代码。
- 沿用既有安全规范、文档流程、等保约束。
- 继续使用现有技术栈（.NET + Vue + SQLite + SqlSugar），不引入反射、动态类型或运行时代码生成。
- 若与历史文档冲突，以更新后的 `docs/contracts.md` + 本文档为唯一实施基线。

---

## 六、Gate 与 GUI 手动测试要求（必须执行）

### 6.1 Gate-R1（功能闭环门禁）

- 必须完成 GUI 手工全链路测试并留痕：
  - 平台控制面 -> 应用工作台 -> 运行交付面 -> 审批/任务 -> 审计
- 关键手工场景（最少）：
  1. 平台首页查看资源与发布信息
  2. 应用创建/编辑/发布
  3. 运行态按 `/r/:appKey/:pageKey` 访问并提交业务动作
  4. 任务领取/处理/回写状态
  5. 审计日志追踪发布与运行事件

### 6.2 Gate-R2（交付闭环门禁）

- 文档、契约、`.http`、集成测试与 e2e 结果全部齐套。
- 弃用公告与上线/运维手册齐备。

### 6.3 GUI 手测输出物

- 手测报告（测试人、时间、环境、版本）
- 场景清单与结果（通过/失败）
- 缺陷列表与回归结论
- 关键流程截图或录屏引用

### 6.4 执行记录（2026-03-08）

- 已完成 Gate-R1 GUI 手测执行与证据归档：
  - 报告：`docs/gate-r1-12sprint-gui手测报告-2026-03-08.md`
  - 截图：`docs/evidence/gate-r1-20260308/*.png`
  - API 补证：`docs/evidence/gate-r1-20260308/api-check-results.json`
- 当前结论：**Gate-R1 已通过**（License 激活 + 关键 API/GUI 回归均通过）。
- 历史阻塞已在执行阶段修复，详见手测报告“缺陷与阻塞清单（修复记录）”。

---

## 七、执行纪律

- 每个 Sprint 结束必须更新状态与阻塞项。
- 每个接口变更必须先改契约，再改实现，再补 `.http`。
- 实现过程严格遵守：强类型、分层依赖、幂等/CSRF、安全审计、禁止循环内数据库操作。
