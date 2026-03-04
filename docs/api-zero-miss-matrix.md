# 全平台接口零遗漏对照矩阵（阶段1基线）

> 基线来源：`src/backend/Atlas.WebApi/Controllers`、`src/backend/Atlas.WebApi/Bosch.http`、`docs/contracts.md`
>
> 目标：确保每个对外端点都有审查结论与测试覆盖。

## 1. 审查口径

- 路由对比维度：`HTTP Method + Path`（忽略 query 顺序与示例 ID 值）。
- 写接口定义：`POST/PUT/PATCH/DELETE`。
- 安全必检：写接口是否具备 `Idempotency-Key` 与 `X-CSRF-TOKEN` 正/负例。

## 2. 三方盘点结论

### 2.1 代码已实现、`.http` 已覆盖、但 `contracts` 未完整记录（示例高频）

- `GET /api/v1/health`
- `GET /api/v1/secure/ping`
- `GET /api/v1/auth/routers`
- `POST /api/v1/auth/register`
- 角色/权限/菜单多个写接口（`/api/v1/roles/*`、`/api/v1/permissions/*`、`/api/v1/menus/*`）
- 审批扩展端点（`/api/v1/approval/tasks/*`、`/api/v1/approval/department-leaders/*`、`/api/v1/approval/copy-records/*`）
- 工作流端点（`/api/v1/workflows/*`）

### 2.2 `contracts` 已记录、但 `.http` 缺失（P0/P1优先补齐）

- `GET /api/v1/visualization/processes/{id}`
- `GET /api/v1/table-views/{id}`
- `PUT /api/v1/dynamic-tables/{tableKey}`
- `POST /api/v1/dynamic-tables/{tableKey}/schema/alter`
- `GET /api/v1/dynamic-tables/{tableKey}/fields`
- `GET /api/v1/dynamic/meta/field-types`
- `GET /api/v1/dynamic-tables/{tableKey}/records/{id}`
- `POST /api/v1/dynamic-tables/{tableKey}/records/query`
- `POST /api/v1/dynamic-tables/{tableKey}/records/batch`
- `DELETE /api/v1/dynamic-tables/{tableKey}/records`
- `GET /api/v1/amis/dynamic-tables/designer`
- `GET /api/v1/amis/dynamic-tables/{tableKey}/crud`
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/create`
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/edit`
- `GET /api/v1/projects/{id}`
- `GET /api/v1/apps/current`
- `POST /api/v1/approval/flows/{id}/preview`

### 2.3 写接口安全用例缺口（示例高优先）

- `Auth.http`：`/auth/token`、`/auth/refresh`、`/auth/password`、`/auth/logout` 缺幂等/CSRF组合用例
- `Users.http`：用户创建/更新/分配/删除写接口缺完整幂等/CSRF组合用例
- `Departments.http`、`Positions.http`、`Projects.http`、`Apps.http`、`Visualization.http` 同类缺口
- `SystemConfigs.http`、`DictTypes.http` 存在仅幂等、不含 CSRF 的写请求段

## 3. 已识别占位实现（P0必须修复）

- `ApprovalAgentController`
  - `GET /api/v1/approval/agents` 直接返回空集合
  - `POST/DELETE` 核心仓储操作被注释
- `ApprovalTasksController`
  - `GET /api/v1/approval/tasks/pool` 返回固定空分页
  - `POST /api/v1/approval/tasks/batch-transfer`、`POST /api/v1/approval/tasks/{id}/viewed` 核心逻辑被注释

## 4. 阶段1输出状态

- `inventory`：完成（有基线矩阵、缺口清单、P0问题定位）
- 下一阶段：进入 `p0-build` 与 `p0-tests`，先打通构建与去占位实现，再补齐写接口安全测试矩阵。

## 5. 阶段2当前进度（本轮）

- 已完成
  - 后端构建已恢复为 `0 错误 / 0 警告`。
  - 占位实现已替换为真实逻辑：
    - `ApprovalAgentController`（查询/创建/删除）
    - `ApprovalTasksController`（任务池、批量转办、标记已阅）
  - 审批操作处理器注册补齐（Claim/Urge/Communicate/Jump/BatchTransfer 等）。
  - 关键写接口 `.http` 已补齐幂等与 CSRF 头（`Users/Departments/Positions/Projects/Apps/SystemConfigs/DictTypes/Visualization/ApprovalTasks`）。
  - 新增 `ApprovalAgent.http` 与 `ComplianceEvidence.http`，覆盖审批代理与等保证据导出链路。
  - OTel 关键属性打点已补齐（`tenant_id`、`trace_id`、`resource_id/task_id/instance_id`）。

- 仍需继续增强（下一轮建议优先）
  - 历史遗留 `.http`（低代码/工作流/仪表盘等）仍存在较多写接口未补齐 `Idempotency-Key + X-CSRF-TOKEN` 完整矩阵。
  - `docs/contracts.md` 与部分已实现接口仍有映射差异，需继续按模块补齐契约与用例。
