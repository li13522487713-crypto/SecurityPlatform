# Plan: 产品化重构 12 Sprint 实施计划

> 本文档是 SecurityPlatform 下一阶段产品化重构的主执行基线。  
> 执行原则：文档先行、契约同步、前后端并行、测试闭环、等保合规。  
> 若与历史计划冲突，以本文档与 `docs/contracts.md` 最新版本为准。

---

## 一、目标与边界

### 1.1 总目标

- 在不放松既有安全规范的前提下，全新重做平台控制面、应用工作台、运行交付面、治理层。
- 建立“可配置、可运行、可迁移、可治理”的商业化闭环。
- P0 在 Sprint 1-8 完成，P1 在 Sprint 9-12 完成。

### 1.2 边界与约束

- 技术栈保持不变：`.NET 10 + ASP.NET Core + SqlSugar + SQLite + Vue3 + TS`。
- 所有写接口强制 `Idempotency-Key` + `X-CSRF-TOKEN`。
- 控制器禁止直接访问数据库；必须经 Service + Repository。
- 公共契约统一维护在 `docs/contracts.md`，每个 Sprint 完成时同步更新。
- 旧路由与旧接口进入 6 个月弃用窗口，仅做安全修复与关键缺陷修复。

---

## 二、目标架构（统一元模型）

### 2.1 四层结构

- 平台控制面：平台首页、应用中心、资源中心、发布中心。
- 应用设计面：应用工作台、页面/表单/流程设计、数据与权限配置。
- 运行交付面：按 `appKey/pageKey` 访问已发布应用、任务中心、审批中心。
- 治理层：导入导出、License、Tools 授权、审计、版本与回滚。

### 2.2 核心对象（新增）

- `AppManifest`：应用全量元数据根对象。
- `AppRelease`：发布快照与可回滚版本。
- `RuntimeRoute`：运行态路由与页面映射。
- `PackageArtifact`：应用导入导出包及清单。
- `LicenseGrant`：离线授权实体与授权范围。
- `ToolAuthorizationPolicy`：工具授权策略与配额规则。
- `FlowDefinition`：审批流与工作流统一挂载模型。

### 2.3 前端信息架构

- 平台入口：`/console`。
- 应用入口：`/apps/:appId/*`。
- 运行态入口：`/r/:appKey/:pageKey`。
- 兼容入口：`/settings/*`（Deprecated，保留窗口期）。

---

## 三、接口契约增量（v1）

> 所有新增写接口：`Idempotency-Key` + `X-CSRF-TOKEN` 必填。

### 3.1 平台面 API

- `GET /api/v1/platform/overview`
- `GET /api/v1/platform/resources`
- `GET /api/v1/platform/releases`

### 3.2 应用面 API

- `GET /api/v1/app-manifests`
- `POST /api/v1/app-manifests`
- `GET /api/v1/app-manifests/{id}`
- `PUT /api/v1/app-manifests/{id}`
- `GET /api/v1/app-manifests/{id}/workspace/{module}`

### 3.3 运行面 API

- `GET /api/v1/runtime/apps/{appKey}/pages/{pageKey}`
- `POST /api/v1/runtime/apps/{appKey}/pages/{pageKey}/actions`
- `GET /api/v1/runtime/tasks/inbox`
- `GET /api/v1/runtime/tasks/done`

### 3.4 治理面 API

- `POST /api/v1/packages/export`
- `POST /api/v1/packages/import`
- `POST /api/v1/licenses/offline-request`
- `POST /api/v1/licenses/import`
- `POST /api/v1/licenses/validate`
- `GET /api/v1/tools/authorization-policies`
- `PUT /api/v1/tools/authorization-policies/{id}`
- `POST /api/v1/tools/authorization-policies/simulate`
- `GET /api/v1/tools/authorization-audits`

---

## 四、12 Sprint 执行编排

| Sprint | 核心目标 | 后端交付 | 前端交付 | 契约/测试交付 | 状态 |
|---|---|---|---|---|---|
| 1 | 基线冻结与边界定稿 | 统一域模型草案、弃用清单 | 新 IA 导航原型 | 契约增量草案、测试矩阵 | [ ] |
| 2 | 后端骨架与迁移 | 核心实体/仓储/服务空实现、DB 迁移 | 主框架路由壳 | API 占位 `.http` | [ ] |
| 3 | 平台控制面 V1 | `platform/*` 聚合 API | 平台首页/应用中心/资源中心 | 平台 e2e 场景 1-3 | [ ] |
| 4 | 发布中心与审计主链路 | 发布、回滚点、审计事件 | 发布中心页面与时间线 | 发布回滚 `.http` + e2e | [ ] |
| 5 | 应用工作台 V1 | `app-manifests/*` 工作台接口 | 应用概览/数据/权限入口 | 应用域联调用例 | [ ] |
| 6 | 统一设计器 V1 | 设计元数据存储与读取 | 页面/表单/流程统一入口 | 设计态回归测试 | [ ] |
| 7 | 运行态 V1 | `runtime/*` 页面读取与任务接口 | `/r/:appKey/:pageKey` 运行壳 | 业务用户运行链路 e2e | [ ] |
| 8 | 流程闭环强化 | `FlowDefinition` 统一与回写补偿 | 任务/审批中心强化 | 主链路集成测试 | [ ] |
| 9 | 导入导出 V1 | 包导入导出、冲突检测、回滚 | 包管理中心 UI | 结构包/数据包/完整包测试 | [ ] |
| 10 | 离线 License V1 | 申请/导入/校验/续期 | License 中心 UI | 离线场景验收脚本 | [ ] |
| 11 | Tools 授权中心 V1 | 策略、限流、审计、模拟接口 | 策略矩阵与模拟页面 | 越权与策略测试 | [ ] |
| 12 | 发布门禁与硬化 | 性能、安全、兼容收口 | 缺陷收敛与引导文档 | Gate-R1/Gate-R2 签收 | [ ] |

---

## 五、验收门禁与指标

### 5.1 Gate-R1（功能闭环）

- 完成平台 -> 应用 -> 运行态 -> 审批/任务 -> 审计全链路手工验证。
- 支持通过模板在 5 分钟内创建可运行应用并完成一次业务提交。
- 发布与回滚具备可视化结果与审计追踪。

### 5.2 Gate-R2（交付闭环）

- 提交并固化本轮验收数据库与配置快照（按仓库规则执行）。
- 所有新增接口均有 `.http` 覆盖示例。
- 关键 e2e 冒烟与集成测试通过。

### 5.3 安全与合规

- 幂等冲突、跨租户访问、CSRF 失败、敏感字段脱敏均有验证用例。
- 审计日志覆盖发布、回滚、导入导出、License、Tools 授权关键动作。

---

## 六、文档同步任务

- `docs/contracts.md`：新增产品化重构 v1 契约分组与弃用策略。
- `docs/plan-功能补齐总览.md`：路线图基线切换为本计划，旧 Phase 清单转为能力池。
- `src/backend/Atlas.WebApi/Bosch.http/*.http`：按 Sprint 对新增 API 持续补充。

---

## 七、执行纪律

- 每个 Sprint 结束前，更新本表状态并记录阻塞项。
- 每个接口变更必须先改契约再改实现。
- 遇到“跨层直连数据库、弱类型、循环内 DB 操作”等违规实现必须立即整改。
