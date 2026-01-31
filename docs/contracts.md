# 接口契约说明

## 目标

- 统一前后端响应结构与分页模型。
- 明确多租户与认证相关的请求头。

## 通用请求头

- `Authorization: Bearer <accessToken>`：JWT 访问令牌。
- `X-Tenant-Id: <tenantId>`：租户标识（GUID）。
- `X-App-Id: <appId>`：应用标识（字符串，仅服务到服务或匿名场景使用；已登录请求从 JWT 的 `app_id` Claim 获取）。
- `X-Client-Type: WebH5 | Mobile | Backend`：客户端类型。
- `X-Client-Platform: Web | Android | iOS`：客户端平台。
- `X-Client-Channel: Browser | App`：客户端通道。
- `X-Client-Agent: Chrome | Edge | Safari | Firefox | Other`：客户端代理（浏览器或环境）。
- `X-Project-Id: <projectId>`：项目标识（仅当应用启用项目模式 `EnableProjectMode = true` 时必填）。
- `Idempotency-Key: <uuid>`：关键写接口必填（创建/提交/开通/触发任务），幂等键冲突返回 409。
- `X-CSRF-TOKEN: <token>`：已登录 Web 写请求必填，需先获取 Anti-Forgery Token。

## 幂等与 Anti-Forgery

### 幂等（Idempotency-Key）

- 服务端唯一键：`tenant_id + user_id + api_name + idempotency_key`。
- 首次请求成功后保存处理结果（状态 + 资源ID/响应摘要）；重复请求返回相同业务结果。
- 同一幂等键但 payload 不一致返回 `IDEMPOTENCY_CONFLICT`。
- 幂等记录保留 N 小时/天后过期（按配置清理）。

### Anti-Forgery Token

- 获取方式：`GET /api/v1/secure/antiforgery`（需登录）。
- 请求头：`X-CSRF-TOKEN`。
- 校验失败返回 `ANTIFORGERY_TOKEN_INVALID`。
## 通用响应模型

### ApiResponse

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-abc123...",
  "data": {}
}
```

字段说明：

- `success`：是否成功。
- `code`：错误码或成功码。
- `message`：错误信息或 OK。
- `traceId`：请求链路追踪 ID。
- `data`：业务数据，失败时为 `null`。

### 错误码

- `SUCCESS`：成功
- `VALIDATION_ERROR`：参数校验错误
- `UNAUTHORIZED`：未认证
- `FORBIDDEN`：无权限
- `NOT_FOUND`：资源不存在
- `SERVER_ERROR`：服务端错误
- `ACCOUNT_LOCKED`：账号锁定
- `PASSWORD_EXPIRED`：密码过期
- `TENANT_NOT_FOUND`：租户不存在
- `PROJECT_REQUIRED`：项目必填
- `PROJECT_NOT_FOUND`：项目不存在
- `PROJECT_DISABLED`：项目已停用
- `INVALID_CREDENTIALS`：账号或密码错误
- `TOKEN_EXPIRED`：令牌过期
- `IDEMPOTENCY_REQUIRED`：缺少幂等键
- `IDEMPOTENCY_CONFLICT`：幂等键冲突
- `IDEMPOTENCY_IN_PROGRESS`：幂等键处理中
- `ANTIFORGERY_TOKEN_INVALID`：CSRF 校验失败

## 分页模型

### PagedRequest

```json
{
  "pageIndex": 1,
  "pageSize": 10,
  "keyword": "search",
  "sortBy": "createdAt",
  "sortDesc": true
}
```

## 认证与授权契约

### 登录

`POST /api/v1/auth/token`

请求（需同时携带 `X-Tenant-Id` 请求头）：

```json
{
  "username": "admin",
  "password": "P@ssw0rd!"
}
```

响应（`ApiResponse` 包装）：

```json
{
  "accessToken": "jwt-access-token",
  "expiresAt": "2026-01-30T10:00:00Z",
  "refreshToken": "refresh-token",
  "refreshExpiresAt": "2026-01-30T22:00:00Z",
  "sessionId": 10010001
}

JWT Claims（新增）：

- `sid`：会话 ID
- `jti`：访问令牌唯一标识
- `app_id`：应用标识（用于租户 + 应用维度的 ID 生成）
- `client_type`：客户端类型（`WebH5`/`Mobile`/`Backend`）
- `client_platform`：客户端平台（`Web`/`Android`/`iOS`）
- `client_channel`：客户端通道（`Browser`/`App`）
- `client_agent`：客户端代理（`Chrome`/`Edge`/`Safari`/`Firefox`/`Other`）
```

### 刷新令牌（使用当前登录态）

`POST /api/v1/auth/refresh`

请求（需携带 `X-Tenant-Id`）：

```json
{
  "refreshToken": "refresh-token"
}
```

响应（`ApiResponse` 包装）：

```json
{
  "accessToken": "new-jwt-access-token",
  "expiresAt": "2026-01-30T11:00:00Z",
  "refreshToken": "new-refresh-token",
  "refreshExpiresAt": "2026-01-30T23:00:00Z",
  "sessionId": 10010001
}
```

### 当前用户

`GET /api/v1/auth/me`

响应（`ApiResponse` 包装）：

```json
{
  "id": "1001",
  "username": "admin",
  "displayName": "系统管理员",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "roles": ["Admin"],
  "permissions": ["workflow:design", "system:admin", "users:view", "roles:view"],
  "clientContext": {
    "clientType": "WebH5",
    "clientPlatform": "Web",
    "clientChannel": "Browser",
    "clientAgent": "Chrome"
  }
}
```

### 修改密码

`PUT /api/v1/auth/password`

请求（需携带 `Authorization` 与 `X-Tenant-Id`）：

```json
{
  "currentPassword": "OldP@ssw0rd!",
  "newPassword": "NewP@ssw0rd!2026",
  "confirmPassword": "NewP@ssw0rd!2026"
}
```

响应：通用 `ApiResponse`

### 注销

`POST /api/v1/auth/logout`

请求：无（需携带 `Authorization` 与 `X-Tenant-Id`）

响应：通用 `ApiResponse`

## 角色、权限与菜单契约

### 角色列表（分页）

`GET /api/v1/roles?pageIndex=1&pageSize=10&keyword=管理员&isSystem=true`

响应：

```json
{
  "items": [
    {
      "id": "1",
      "name": "管理员",
      "code": "Admin",
      "description": "系统内置角色",
      "isSystem": true
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

说明：

- `isSystem`：可选，`true`=系统内置，`false`=自定义。

### 权限列表（分页）

`GET /api/v1/permissions?pageIndex=1&pageSize=10&keyword=workflow&type=Api`

响应：

```json
{
  "items": [
    {
      "id": "1",
      "code": "workflow:design",
      "name": "工作流设计",
      "type": "Api",
      "description": "流程配置"
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

说明：

- `type`：可选，`Api` 或 `Menu`。

### 菜单列表（分页）

`GET /api/v1/menus?pageIndex=1&pageSize=10&keyword=system&isHidden=false`

响应：

```json
{
  "items": [
    {
      "id": "10",
      "name": "系统管理",
      "path": "/system",
      "parentId": null,
      "sortOrder": 0,
      "component": "Layout",
      "icon": "settings",
      "permissionCode": "system:admin",
      "isHidden": false
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

说明：

- `isHidden`：可选，`true`=仅隐藏，`false`=仅显示。

### 菜单全量

`GET /api/v1/menus/all`

响应：

```json
[
  {
    "id": "10",
    "name": "系统管理",
    "path": "/system",
    "parentId": null,
    "sortOrder": 0,
    "component": "Layout",
    "icon": "settings",
    "permissionCode": "system:admin",
    "isHidden": false
  }
]
```

### 权限码清单（默认）

- `system:admin`：系统管理员
- `workflow:design`：工作流设计器
- `users:view`：用户查看
- `users:create`：用户新增
- `users:update`：用户更新
- `users:assign-roles`：用户分配角色
- `users:assign-departments`：用户分配部门
- `roles:view`：角色查看
- `roles:create`：角色新增
- `roles:update`：角色更新
- `roles:assign-permissions`：角色分配权限
- `roles:assign-menus`：角色分配菜单
- `permissions:view`：权限查看
- `permissions:create`：权限新增
- `permissions:update`：权限更新
- `departments:view`：部门查看
- `departments:all`：部门全量
- `departments:create`：部门新增
- `departments:update`：部门更新
- `menus:view`：菜单查看
- `menus:all`：菜单全量
- `menus:create`：菜单新增
- `menus:update`：菜单更新
- `apps:view`：应用配置查看
- `apps:update`：应用配置更新
- `projects:view`：项目查看
- `projects:create`：项目新增
- `projects:update`：项目更新
- `projects:delete`：项目删除
- `projects:assign-users`：项目分配人员
- `projects:assign-departments`：项目分配部门
- `projects:assign-positions`：项目分配岗位
- `audit:view`：审计查看
- `assets:create`：资产新增
- `approval:flow:create`：审批流创建
- `approval:flow:update`：审批流更新
- `approval:flow:publish`：审批流发布
- `approval:flow:delete`：审批流删除
- `approval:flow:disable`：审批流停用
- `visualization:process:save`：可视化流程保存
- `visualization:process:update`：可视化流程更新
- `visualization:process:publish`：可视化流程发布

字段说明：

- `pageIndex`：页码，从 1 开始。
- `pageSize`：每页数量。
- `keyword`：关键字检索。
- `sortBy`：排序字段。
- `sortDesc`：是否降序。

### PagedResult

```json
{
  "items": [],
  "total": 100,
  "pageIndex": 1,
  "pageSize": 10
}
```

字段说明：

- `items`：当前页数据。
- `total`：总数量。
- `pageIndex`：页码。
- `pageSize`：每页数量。

## 示例：分页响应包装

## 审批流设计器契约

### 审批流定义（ApprovalFlowDefinition）

字段说明（响应）：

- `id`：流程定义 ID
- `name`：流程名称
- `definitionJson`：流程定义 JSON（设计器保存结构）
- `version`：版本号
- `status`：状态
- `publishedAt`：发布时间
- `publishedByUserId`：发布人
- `category`：流程分类
- `description`：流程说明
- `visibilityScopeJson`：可见范围配置 JSON
- `isQuickEntry`：是否快捷入口

### DefinitionJson 结构（新格式）

```json
{
  "meta": {
    "flowName": "采购申请审批",
    "description": "采购类流程",
    "category": "采购",
    "visibilityScope": {
      "scopeType": "Department",
      "departmentIds": [10, 11]
    },
    "isQuickEntry": false,
    "isLowCodeFlow": true
  },
  "lfForm": {
    "formJson": { "widgetList": [], "formConfig": {} },
    "formFields": [
      {
        "fieldId": "input_1",
        "fieldName": "金额",
        "fieldType": "input-number",
        "valueType": "Number",
        "options": []
      }
    ]
  },
  "nodes": {
    "rootNode": {
      "nodeId": "start_1",
      "nodeType": "start",
      "nodeName": "发起人",
      "childNode": {
        "nodeId": "approve_1",
        "nodeType": "approve",
        "nodeName": "部门负责人",
        "approverConfig": {
          "setType": 1,
          "signType": 1,
          "noHeaderAction": 0,
          "nodeApproveList": [
            { "targetId": "role-manager", "name": "部门经理" }
          ]
        },
        "childNode": {
          "nodeId": "end_1",
          "nodeType": "end",
          "nodeName": "结束"
        }
      }
    }
  }
}
```

### 节点模型说明

- `nodeType` 支持：`start`、`approve`、`copy`、`condition`、`dynamicCondition`、`parallelCondition`、`parallel`、`end`  
- `conditionNodes`：条件分支数组（每个分支含 `branchName`、`conditionRule`、`childNode`、`isDefault`）  
- `parallelNodes`：并行审批分支数组  
- `approverConfig`：审批人设置（setType/signType/noHeaderAction/人员列表）  
- `buttonPermissionConfig`：按钮权限（发起页/审批页/查看页）  
- `formPermissionConfig`：表单字段权限（R/E/H）  
- `noticeConfig`：通知设置（渠道与模板）

### 可见范围（VisibilityScope）

```json
{
  "scopeType": "All|Department|Role|User",
  "departmentIds": [1, 2],
  "roleCodes": ["Manager", "Admin"],
  "userIds": [100, 200]
}
```

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-abc123...",
  "data": {
    "items": [],
    "total": 0,
    "pageIndex": 1,
    "pageSize": 10
  }
}
```

## 审批实例与任务契约

### 审批实例

- `POST /api/v1/approval/instances`：发起流程实例
- `GET /api/v1/approval/instances/my`：我发起的流程实例
- `GET /api/v1/approval/instances/{id}`：流程实例详情
- `GET /api/v1/approval/instances/{id}/history`：流程实例历史
- `POST /api/v1/approval/instances/{id}/cancellation`：取消流程实例
- `POST /api/v1/approval/instances/{id}/operations`：实例操作（撤回/转办/加签等）
- `GET /api/v1/approval/instances/{id}/preview`：预览流程实例
- `GET /api/v1/approval/instances/{id}/print-view`：打印视图

### 审批任务

- `GET /api/v1/approval/tasks/my`：我的待办任务
- `GET /api/v1/approval/instances/{instanceId}/tasks`：实例内任务列表
- `POST /api/v1/approval/tasks/{taskId}/decision`：任务审批（`approved=true|false`）

## 可视化模块契约

### 流程概览

`GET /api/v1/visualization/overview`

响应数据：

```json
{
  "totalProcesses": 0,
  "runningInstances": 0,
  "blockedNodes": 0,
  "alertsToday": 0,
  "riskHints": []
}
```

### 流程列表与详情

`GET /api/v1/visualization/processes?pageIndex=1&pageSize=10`

```json
{
  "items": [
    {
      "id": "1",
      "name": "示例流程",
      "version": 1,
      "status": "Draft",
      "publishedAt": "2026-01-30T10:00:00Z"
    }
  ],
  "total": 0,
  "pageIndex": 1,
  "pageSize": 10
}
```

`GET /api/v1/visualization/processes/{id}`

```json
{
  "id": "1",
  "name": "示例流程",
  "version": 1,
  "status": "Draft",
  "publishedAt": "2026-01-30T10:00:00Z",
  "definitionJson": "{ \"nodes\": [], \"edges\": [] }"
}
```

### 保存草稿 / 发布

`POST /api/v1/visualization/processes`

```json
{
  "processId": "1",
  "version": 1,
  "status": "Draft"
}
```

`POST /api/v1/visualization/processes/{id}/publication`

```json
{
  "processId": "1",
  "version": 1,
  "status": "Published"
}
```

### 运行态实例

`GET /api/v1/visualization/instances?pageIndex=1&pageSize=10&processId=1&status=Running`

```json
{
  "items": [
    {
      "id": "1001",
      "flowName": "示例流程",
      "status": "Running",
      "currentNode": "审批节点",
      "startedAt": "2026-01-30T10:00:00Z",
      "durationMinutes": 35
    }
  ],
  "total": 0,
  "pageIndex": 1,
  "pageSize": 10
}
```

### 指标聚合

`GET /api/v1/visualization/metrics`

```json
{
  "totalProcesses": 0,
  "draftProcesses": 0,
  "runningInstances": 0,
  "completedInstances": 0,
  "pendingTasks": 0,
  "overdueTasks": 0,
  "assetsTotal": 0,
  "alertsToday": 0,
  "auditEventsToday": 0
}
```

### 审计查询

`GET /api/v1/visualization/audit?pageIndex=1&pageSize=10`

```json
{
  "items": [
    {
      "id": "1",
      "actor": "admin",
      "action": "可视化流程-发布",
      "result": "成功",
      "target": "流程ID: 1",
      "ipAddress": "127.0.0.1",
      "userAgent": "Mozilla/5.0",
      "clientType": "WebH5",
      "clientPlatform": "Web",
      "clientChannel": "Browser",
      "clientAgent": "Chrome",
      "occurredAt": "2026-01-30T10:00:00Z"
    }
  ],
  "total": 0,
  "pageIndex": 1,
  "pageSize": 10
}
```

## 表格视图（个人）

说明：

- 仅个人视图：与当前登录用户唯一绑定，不支持共享。
- `tableKey` 统一标识表格来源：
  - `system.users`：员工管理
  - `system.roles`：角色管理
  - `system.permissions`：权限管理
  - `system.menus`：菜单管理
  - `system.departments`：部门管理
  - `system.positions`：职位管理
  - `system.projects`：项目管理
  - `system.apps`：应用管理
- 系统默认配置来源于 `appsettings.json` 的 `TableViewDefaults` 配置节。

### 查询视图列表

`GET /api/v1/table-views?tableKey=system.users&pageIndex=1&pageSize=20&keyword=我的`

响应：`PagedResult<TableViewListItem>`

### 获取默认视图

`GET /api/v1/table-views/default?tableKey=system.users`

### 获取系统默认配置

`GET /api/v1/table-views/default-config?tableKey=system.users`

### 获取视图详情

`GET /api/v1/table-views/{id}`

### 创建视图（需幂等 + CSRF）

`POST /api/v1/table-views`

请求：

```json
{
  "tableKey": "system.users",
  "name": "我的视图",
  "config": {
    "columns": [
      { "key": "username", "visible": true, "order": 0 },
      { "key": "displayName", "visible": true, "order": 1 }
    ],
    "density": "default",
    "pagination": { "pageSize": 10 }
  }
}
```

### 更新视图（需幂等 + CSRF）

`PUT /api/v1/table-views/{id}`

### 更新视图配置（需幂等 + CSRF）

`PATCH /api/v1/table-views/{id}/config`

### 设为默认视图（需幂等 + CSRF）

`POST /api/v1/table-views/{id}/set-default`

### 复制视图（需幂等 + CSRF）

`POST /api/v1/table-views/{id}/duplicate`

请求：

```json
{
  "name": "我的视图（副本）"
}
```

### 删除视图（需幂等 + CSRF）

`DELETE /api/v1/table-views/{id}`

### TableViewConfig

```json
{
  "columns": [
    { "key": "username", "visible": true, "order": 0, "width": 120, "pinned": "left" },
    { "key": "displayName", "visible": true, "order": 1 }
  ],
  "density": "compact|default|comfortable",
  "pagination": { "pageSize": 10 },
  "sort": [{ "key": "createdAt", "order": "ascend", "priority": 1 }],
  "filters": [{ "key": "status", "operator": "eq", "value": "Active" }],
  "groupBy": { "key": "departmentId", "collapsedKeys": [] },
  "aggregations": [{ "key": "amount", "op": "sum" }],
  "queryPanel": { "open": true, "autoSearch": false, "savedFilterId": "filter-1" },
  "queryModel": {
    "logic": "AND",
    "conditions": [{ "field": "status", "operator": "eq", "value": "Active" }],
    "groups": []
  }
}
```

## 用户/部门/职位管理契约

### 用户

- `GET /api/v1/users`：分页查询用户
- `GET /api/v1/users/{id}`：用户详情
- `POST /api/v1/users`：新增用户
- `PUT /api/v1/users/{id}`：更新用户
- `DELETE /api/v1/users/{id}`：删除用户
- `PUT /api/v1/users/{id}/roles`：更新用户角色
- `PUT /api/v1/users/{id}/departments`：更新用户部门
- `PUT /api/v1/users/{id}/positions`：更新用户职位

### 部门

- `GET /api/v1/departments`：分页查询部门
- `GET /api/v1/departments/all`：获取全部部门
- `POST /api/v1/departments`：新增部门
- `PUT /api/v1/departments/{id}`：更新部门
- `DELETE /api/v1/departments/{id}`：删除部门

### 职位

- `GET /api/v1/positions`：分页查询职位
- `GET /api/v1/positions/{id}`：职位详情
- `GET /api/v1/positions/all`：获取全部职位
- `POST /api/v1/positions`：新增职位
- `PUT /api/v1/positions/{id}`：更新职位
- `DELETE /api/v1/positions/{id}`：删除职位

## 项目管理契约

### 项目

- `GET /api/v1/projects?appId=app-100`：分页查询项目（`appId` 为应用维度，必填）
- `GET /api/v1/projects/{id}`：项目详情
- `POST /api/v1/projects`：新增项目
- `PUT /api/v1/projects/{id}`：更新项目
- `DELETE /api/v1/projects/{id}`：删除项目
- `PUT /api/v1/projects/{id}/users`：项目分配人员
- `PUT /api/v1/projects/{id}/departments`：项目分配部门
- `PUT /api/v1/projects/{id}/positions`：项目分配职位
- `GET /api/v1/projects/my`：当前用户可切换项目列表

说明：

- 项目标识唯一性按 `appId + projectCode` 约束。

## 应用配置契约

### 应用配置

- `GET /api/v1/apps`：分页查询应用配置
- `GET /api/v1/apps/current`：当前应用配置
- `GET /api/v1/apps/{id}`：应用配置详情
- `PUT /api/v1/apps/{id}`：更新应用配置

### AppConfigResponse

```json
{
  "id": "app-100",
  "appCode": "security-platform",
  "name": "SecurityPlatform",
  "description": "默认应用配置",
  "apiVersion": "v1",
  "isActive": true,
  "isTenantSubscribable": true,
  "enableProjectMode": true,
  "allowTenantAdminManageDataSource": true,
  "auditLevel": "Required",
  "sortOrder": 0
}
```

### AppConfigUpdateRequest

```json
{
  "name": "SecurityPlatform",
  "isActive": true,
  "enableProjectMode": false,
  "apiVersion": "v1",
  "isTenantSubscribable": true,
  "allowTenantAdminManageDataSource": true,
  "auditLevel": "Required",
  "description": "默认应用配置",
  "sortOrder": 0
}
```

字段说明：

- `enableProjectMode`：是否启用项目模式（应用级开关，启用后业务数据必须带 `project_id`）。
- `isTenantSubscribable`：是否允许租户开通该应用。
- `allowTenantAdminManageDataSource`：是否允许租户管理员管理该应用的数据源。
- `auditLevel`：审计级别（`Required`/`Optional`/`None`）。

## 项目管理契约

项目为业务实体，启用项目模式后，所有业务数据必须以 `project_id` 作为数据范围隔离维度，项目编码在应用维度唯一（`appId + projectCode`）。

### 项目

- `GET /api/v1/projects?appId=app-100`：分页查询项目（`appId` 为应用维度，必填）
- `GET /api/v1/projects/{id}`：项目详情
- `POST /api/v1/projects`：新增项目
- `PUT /api/v1/projects/{id}`：更新项目
- `DELETE /api/v1/projects/{id}`：停用项目（逻辑删除）

### ProjectResponse

```json
{
  "id": "project-100",
  "appId": "app-100",
  "projectCode": "ops-core",
  "name": "运维核心项目",
  "description": "运维平台核心业务线",
  "isActive": true,
  "createdAt": "2026-01-31T10:00:00Z",
  "updatedAt": "2026-01-31T10:00:00Z"
}
```

### ProjectCreateRequest

```json
{
  "appId": "app-100",
  "projectCode": "ops-core",
  "name": "运维核心项目",
  "description": "运维平台核心业务线"
}
```

### ProjectUpdateRequest

```json
{
  "name": "运维核心项目",
  "description": "运维平台核心业务线",
  "isActive": true
}
```

字段说明：

- `appId`：所属应用标识（项目维度唯一性按 `appId + projectCode`）。

## Workflow Designer APIs (草案)
- POST `/api/v1/approval/flows` : 保存流程定义
  - body: { tenantId: string, definition: FlowDefinition }
  - resp: { id: string, version?: number }
- GET `/api/v1/approval/flows/{id}` : 加载流程定义
  - resp: { definition: FlowDefinition }
- PUT `/api/v1/approval/flows/{id}` : 更新流程定义
  - body 同保存
  - resp: { success: boolean }
- POST `/api/v1/approval/flows/{id}/publication` : 发布流程
  - resp: { success: boolean, version: number }
- POST `/api/v1/approval/flows/validation` : 前/后端联合校验
  - body: { tenantId: string, definition: FlowDefinition }
  - resp: { isValid: boolean, errors: string[], warnings?: string[] }
- POST `/api/v1/approval/flows/{id}/preview` : 预览（返回节点线性/树形展开视图数据）
  - resp: { definition: FlowDefinition, preview: any }

### FlowDefinition / FlowNode (见前端 types/workflow.ts)
- 节点类型：start/end/approve/condition/parallel/parallel-join/copy/task
- 审批人规则：fixedUser/role/departmentLeader/selfSelect/hrbp/formField/outsideApi
- 条件：ConditionGroup{ relation AND/OR, items: [{ field, op, value, group }] }
- 按钮：NodeButton { pageType, buttonType, name, remark }

### 约束与校验（后端应补充验证）
- 必须存在唯一 start 与 end 节点
- 并行节点必须有聚合节点 parallel-join
- condition 节点需有默认分支或全覆盖
- 审批节点必须配置 approverRule
- 节点 name/code 长度与特殊字符校验
- 发布前需要通过 validation

