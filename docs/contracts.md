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

认证校验补充：

- 访问令牌在每次请求都会校验用户状态（`IsActive=true`）。
- 若用户被禁用，已签发 access token 在下一次请求即失效（返回 401）。
- 同时校验会话 `sid` 是否存在、是否已撤销、是否过期。

### 获取个人资料

`GET /api/v1/auth/profile`

响应（`ApiResponse` 包装）：

```json
{
  "displayName": "系统管理员",
  "email": "admin@atlas.local",
  "phoneNumber": "13800000000"
}
```

### 更新个人资料

`PUT /api/v1/auth/profile`

请求（需携带 `Authorization` 与 `X-Tenant-Id`）：

```json
{
  "displayName": "系统管理员",
  "email": "admin@atlas.local",
  "phoneNumber": "13800000000"
}
```

响应：通用 `ApiResponse`

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

## 租户数据源契约

### 管理接口

- `GET /api/v1/tenant-datasources`：查询数据源列表
- `POST /api/v1/tenant-datasources`：新增数据源
- `PUT /api/v1/tenant-datasources/{id}`：更新数据源
- `DELETE /api/v1/tenant-datasources/{id}`：删除数据源
- `POST /api/v1/tenant-datasources/test`：测试数据源连接

### 数据源类型

- `SQLite`
- `SqlServer`
- `MySql`
- `PostgreSql`

### 测试连接

`POST /api/v1/tenant-datasources/test`

请求示例：

```json
{
  "connectionString": "Host=127.0.0.1;Port=5432;Database=atlas;Username=postgres;Password=postgres",
  "dbType": "PostgreSql"
}
```

响应示例：

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-...",
  "data": {
    "success": true,
    "errorMessage": null
  }
}
```

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
- 删除约束：角色已被用户绑定时不允许删除，返回 `VALIDATION_ERROR`，需先解绑用户与角色关系。
- 角色权限生效规则：后端每次鉴权都会实时按当前 `用户-角色-权限` 关系解析，不依赖令牌中的角色快照，角色调整后无需重新登录。

### 角色详情

`GET /api/v1/roles/{id}`

响应：

```json
{
  "id": "1",
  "name": "管理员",
  "code": "Admin",
  "description": "系统内置角色",
  "isSystem": true,
  "dataScope": 1,
  "permissionIds": [101, 102],
  "menuIds": [201, 202]
}
```

说明：

- `dataScope` 数据范围枚举：
  - `1`：全部数据（当前租户）
  - `2`：自定义部门
  - `3`：本部门
  - `4`：本部门及下级
  - `5`：仅本人
  - `6`：项目维度

### 设置角色数据范围

`PUT /api/v1/roles/{id}/data-scope`

请求体：

```json
{
  "dataScope": 4
}
```

响应：通用 `ApiResponse`

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

- `type`：可选，支持 `Api`、`Menu`、`Application`、`Page`、`Action`。

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
- `assets:view`：资产查看
- `assets:create`：资产新增
- `alert:view`：告警查看
- `approval:flow:view`：审批流查看
- `approval:flow:create`：审批流创建
- `approval:flow:update`：审批流更新
- `approval:flow:publish`：审批流发布
- `approval:flow:delete`：审批流删除
- `approval:flow:disable`：审批流停用
- `visualization:view`：可视化查看
- `visualization:process:save`：可视化流程保存
- `visualization:process:update`：可视化流程更新
- `visualization:process:publish`：可视化流程发布
- `notification:view`：通知查看
- `notification:create`：通知创建
- `notification:update`：通知更新
- `notification:delete`：通知删除
- `file:upload`：文件上传
- `file:download`：文件下载
- `file:delete`：文件删除

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
- `GET /api/v1/approval/instances/{id}/history/export`：导出流程历史（CSV）
- `POST /api/v1/approval/instances/{id}/cancellation`：取消流程实例
- `POST /api/v1/approval/instances/{id}/operations`：实例操作（撤回/转办/加签等）
- `GET /api/v1/approval/instances/{id}/preview`：预览流程实例
- `GET /api/v1/approval/instances/{id}/print-view`：打印视图

`GET /api/v1/approval/instances/{id}` 返回 `ApprovalInstanceDetailDto`，其中 `status` 使用数值枚举：

- `-3`：已作废（Destroy）
- `-2`：已挂起（Suspended）
- `-1`：草稿（Draft）
- `0`：运行中（Running）
- `1`：已完成（Completed）
- `2`：已驳回（Rejected）
- `3`：已取消（Canceled）
- `4`：超时结束（TimedOut）
- `5`：强制终止（Terminated）
- `6`：自动通过（AutoApproved）
- `7`：自动拒绝（AutoRejected）
- `8`：AI 处理中（AiProcessing）
- `9`：AI 转人工（AiManualReview）

`GET /api/v1/approval/instances/{id}/history` 返回 `PagedResult<ApprovalHistoryEventDto>`，其中 `eventType` 为字符串枚举（如 `InstanceStarted`、`TaskApproved`、`TaskRejected`、`InstanceCompleted`、`InstanceSuspended` 等）。

### 审批任务

- `GET /api/v1/approval/tasks/my`：我的待办任务
- `GET /api/v1/approval/instances/{instanceId}/tasks`：实例内任务列表
- `POST /api/v1/approval/tasks/{taskId}/decision`：任务审批（`approved=true|false`）

## 运维监控与合规取证契约

- `GET /api/v1/monitor/server-info`：服务监控快照（CPU/内存/磁盘/运行时）
- `GET /api/v1/monitor/compliance/evidence-package`：导出等保证据包（zip）
  - 鉴权：`system:admin`
  - 响应：`application/zip` 文件流（非 `ApiResponse` 包装）
  - 默认证据项：合规映射文档、核心配置快照、取证 `.http` 样例、健康与任务取证脚本

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

## AMIS 低代码页面契约

  - **Schema 入口**：每个管理页面（员工/角色/权限/菜单/部门/职位/项目/应用）统一通过 `GET /api/v1/amis/pages/{key}` 拉取 Baidu AMIS 模式 JSON。
    - 请求必须带 `Authorization`、`X-Tenant-Id`（GET 请求无需 `Idempotency-Key`），写接口由前端 fetcher 自动附带 `Idempotency-Key` 与 `X-CSRF-TOKEN`。
    - 响应遵循通用 `ApiResponse<AmisPageDefinition>`，`data` 包含 `{ key, title, description, tableKey, schema }`，其中 `schema` 为完整的 AMIS JSON，即 `type: "page"` 的配置体。
    - 前端按模块加载 schema；schema 变更必须同步更新 `docs/contracts.md`。
- **tableKey 一览与模块映射**：
  - `system.users`：员工管理
  - `system.roles`：角色列表
  - `system.permissions`：权限列表
  - `system.menus`：菜单树
  - `system.departments`：部门
  - `system.positions`：职位
  - `system.projects`：项目
  - `system.apps`：应用配置
  这些 `tableKey` 同时用于表格视图存储与后端 `TableView` API。
  - **Schema 要求**：
  1. 所有列表型组件须默认分页 `pageSize=20`，并支持关键字（`keyword`）远程搜索，搜索字段归前端自由组合，但必须把 `keyword` 参数传给后端接口。
  2. 所有 `select`/`autocomplete` 组件必须使用远程接口（如 `/roles`, `/departments/all`）并通过 `pageSize=20` 限制，预设 `adaptor` 将 `ApiResponse` 转为 AMIS 可识别的 `{ status: 0, msg, data }`。
  3. 通过 `headerToolbar`/`toolbar` 按钮的 `dialog`/`ajax` 操作调用后端的写接口（`POST/PUT/DELETE`），在 schema 中可复用 `saveApi`/`deleteApi`，写操作由前端 fetcher 自动附带 `Idempotency-Key` + `X-CSRF-TOKEN`，后端统一返回 `ApiResponse`。
    4. 所有 schema 均包含 `tableKey` 字段，用于与个人视图（`TableView`）联动，具体联动策略以前端实现为准。
    5. schema 文件存放于后端 `src/backend/Atlas.WebApi/AmisSchemas/{key}.json`，由后端直接读取并返回。

- **示例响应**：

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-abc123...",
  "data": {
    "key": "system.users",
    "title": "员工管理",
    "description": "基于 AMIS 的员工列表与表单",
    "tableKey": "system.users",
    "schema": {
      "type": "page",
      "title": "员工管理",
      "body": [ ... ]
    }
  }
}
```

前端 `AmisRenderer` 组件会把 `schema` 直接传给 `render` 并依赖 `env.fetcher` 将请求透传到后端 `ApiResponse`，还会用 `notify`/`alert` 显示提示。

## 低代码应用与页面契约

### 低代码应用（LowCodeApp）

- `GET /api/v1/lowcode-apps?pageIndex=1&pageSize=10&keyword=&category=`：分页查询应用
- `GET /api/v1/lowcode-apps/{id}`：应用详情（含页面列表）
- `GET /api/v1/lowcode-apps/by-key/{appKey}`：按应用标识查询详情
- `POST /api/v1/lowcode-apps`：创建应用（需幂等 + CSRF）
- `PUT /api/v1/lowcode-apps/{id}`：更新应用（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/{id}/datasource`：查询应用绑定数据源（脱敏）
- `POST /api/v1/lowcode-apps/{id}/datasource/test`：测试应用绑定数据源连接（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/{id}/sharing-policy`：查询应用共享策略
- `PUT /api/v1/lowcode-apps/{id}/sharing-policy`：更新应用共享策略（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/{id}/entity-aliases`：查询应用实体别名
- `PUT /api/v1/lowcode-apps/{id}/entity-aliases`：更新应用实体别名（需幂等 + CSRF）
- `POST /api/v1/lowcode-apps/{id}/publish`：发布应用（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/{id}/versions?pageIndex=1&pageSize=10`：分页查询应用版本历史（仅系统管理员）
- `POST /api/v1/lowcode-apps/{id}/versions/{versionId}/rollback`：按应用版本回滚（需幂等 + CSRF，仅系统管理员）
- `POST /api/v1/lowcode-apps/{id}/disable`：停用应用（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/{id}/export`：导出应用 JSON 包
- `POST /api/v1/lowcode-apps/import`：导入应用 JSON 包（需幂等 + CSRF，支持 `Rename/Overwrite/Skip` 冲突策略）
- `GET /api/v1/lowcode-apps/{appId}/environments`：查询应用环境配置
- `GET /api/v1/lowcode-apps/environments/{id}`：环境配置详情
- `POST /api/v1/lowcode-apps/{appId}/environments`：创建应用环境配置（需幂等 + CSRF）
- `PUT /api/v1/lowcode-apps/environments/{id}`：更新应用环境配置（需幂等 + CSRF）
- `DELETE /api/v1/lowcode-apps/environments/{id}`：删除应用环境配置（需幂等 + CSRF）
- `DELETE /api/v1/lowcode-apps/{id}`：删除应用（需幂等 + CSRF）

授权策略：

- 读接口（GET）要求 `apps:view`
- 写接口（POST/PUT/PATCH/DELETE）要求 `apps:update`
- 应用版本查询/回滚接口要求 `system:admin`

### LowCodeAppCreateRequest（新增字段）

```json
{
  "appKey": "crm_app",
  "name": "CRM 应用",
  "description": "客户关系管理",
  "category": "CRM",
  "icon": "team",
  "dataSourceId": "20010001",
  "useSharedUsers": true,
  "useSharedRoles": true,
  "useSharedDepartments": true
}
```

字段说明：

- `dataSourceId`：应用绑定数据源 ID，创建后不可修改；为空表示使用平台默认数据源。
- `useSharedUsers/useSharedRoles/useSharedDepartments`：基础数据共享策略开关，`true` 继承平台，`false` 应用独立。

### AppDataSourceView

```json
{
  "dataSourceId": "20010001",
  "name": "CRM-MySQL",
  "dbType": "MySql",
  "lastTestSuccess": true,
  "lastTestedAt": "2026-03-07T10:00:00Z"
}
```

### AppSharingPolicyDto

```json
{
  "useSharedUsers": true,
  "useSharedRoles": true,
  "useSharedDepartments": true
}
```

### AppEntityAliasDto

```json
{
  "entityType": "user",
  "singularAlias": "员工",
  "pluralAlias": "员工列表"
}
```

### LowCodeAppVersionListItem

```json
{
  "id": "3001",
  "appId": "2001",
  "version": 5,
  "actionType": "Rollback",
  "sourceVersionId": "2998",
  "note": "Rollback to version 3",
  "createdAt": "2026-03-04T10:00:00Z",
  "createdBy": 10001
}
```

### 低代码页面（LowCodePage）

- `POST /api/v1/lowcode-apps/{appId}/pages`：创建页面（需幂等 + CSRF）
- `PUT /api/v1/lowcode-apps/pages/{pageId}`：更新页面元数据（需幂等 + CSRF）
- `PATCH /api/v1/lowcode-apps/pages/{pageId}/schema`：仅更新页面 Schema（需幂等 + CSRF）
- `POST /api/v1/lowcode-apps/pages/{pageId}/publish`：发布页面（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/pages/{pageId}/versions`：页面版本历史
- `GET /api/v1/lowcode-apps/pages/{pageId}/runtime?mode=draft|published&environmentCode=dev`：运行态 Schema（草稿/已发布，支持环境变量替换）
- `POST /api/v1/lowcode-apps/pages/{pageId}/rollback/{versionId}`：按历史版本回滚并生成新发布版本（需幂等 + CSRF）
- `DELETE /api/v1/lowcode-apps/pages/{pageId}`：删除页面（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/pages/{pageId}`：页面详情（含完整 `schemaJson`）
- `GET /api/v1/lowcode-apps/{appId}/pages/tree`：页面树（按 `parentPageId` + `sortOrder`）

授权策略：

- 页面读接口要求 `apps:view`
- 页面写接口要求 `apps:update`

### LowCodePageTreeNode

```json
{
  "id": "1001",
  "appId": "2001",
  "pageKey": "customer-list",
  "name": "客户列表",
  "pageType": "List",
  "routePath": "/customers",
  "description": "客户管理列表页面",
  "icon": "unordered-list",
  "sortOrder": 1,
  "parentPageId": null,
  "version": 3,
  "isPublished": true,
  "createdAt": "2026-03-03T09:00:00Z",
  "permissionCode": "customers:view",
  "dataTableKey": "crm_customers",
  "children": []
}
```

### LowCodePageDetail

```json
{
  "id": "1001",
  "appId": "2001",
  "pageKey": "customer-list",
  "name": "客户列表",
  "pageType": "List",
  "schemaJson": "{ \"type\": \"page\", \"body\": [] }",
  "routePath": "/customers",
  "description": "客户管理列表页面",
  "icon": "unordered-list",
  "sortOrder": 1,
  "parentPageId": null,
  "version": 3,
  "isPublished": true,
  "createdAt": "2026-03-03T09:00:00Z",
  "updatedAt": "2026-03-03T10:00:00Z",
  "createdBy": 10001,
  "updatedBy": 10001,
  "permissionCode": "customers:view",
  "dataTableKey": "crm_customers"
}
```

## 动态表与低代码 CRUD 契约（草案）

### 命名与校验规则

- `tableKey`：`^[A-Za-z][A-Za-z0-9_]{1,63}$`，禁止保留字与系统表名。
- `fieldName`：`^[A-Za-z][A-Za-z0-9_]{0,63}$`，禁止保留字与系统字段。
- 不允许使用 `drop/alter/insert/update/delete` 等危险关键字作为名称。

### 字段类型枚举

- `Int`、`Long`、`Decimal`、`String`、`Text`、`Bool`、`DateTime`、`Date`
- `Decimal` 需指定 `precision`/`scale`
- `String` 需指定 `length`
- 自增仅允许 `Int/Long` 且必须主键

### 动态表接口

- `GET /api/v1/dynamic-tables`：分页查询动态表
- `GET /api/v1/dynamic-tables/{tableKey}`：动态表详情
- `POST /api/v1/dynamic-tables`：新建动态表（需幂等 + CSRF）
- `PUT /api/v1/dynamic-tables/{tableKey}`：更新表元数据（需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/schema/alter`：变更字段（需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/schema/alter/preview`：预览变更 SQL（只读，不落库）
- `GET /api/v1/dynamic-tables/{tableKey}/migrations`：分页查询结构迁移记录
- `DELETE /api/v1/dynamic-tables/{tableKey}`：删除动态表（需幂等 + CSRF）
- `GET /api/v1/dynamic-tables/{tableKey}/relations`：查询轻量关系
- `PUT /api/v1/dynamic-tables/{tableKey}/relations`：覆盖更新轻量关系（需幂等 + CSRF）
- `GET /api/v1/dynamic-tables/{tableKey}/field-permissions`：查询字段级权限规则
- `PUT /api/v1/dynamic-tables/{tableKey}/field-permissions`：覆盖更新字段级权限规则（需幂等 + CSRF）

### 动态迁移记录接口（骨架）

- `GET /api/v1/dynamic-migrations?pageIndex=1&pageSize=10&tableKey=orders`：分页查询迁移记录
- `GET /api/v1/dynamic-migrations/{id}`：迁移记录详情
- `POST /api/v1/dynamic-migrations`：创建迁移草稿记录（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/detect/{tableKey}`：检测结构变更并生成预览脚本（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/{id}/execute`：执行迁移（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/{id}/precheck`：迁移预检查（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/{id}/retry`：重试迁移（需幂等 + CSRF）

```json
{
  "tableKey": "orders",
  "version": 1,
  "upScript": "ALTER TABLE ...",
  "downScript": "ALTER TABLE ...",
  "isDestructive": false
}
```

`detect` 响应：

```json
{
  "tableKey": "orders",
  "upScript": "ALTER TABLE ...",
  "downScript": "-- no-op",
  "isDestructive": false,
  "warnings": []
}
```

`execute/retry` 响应：

```json
{
  "id": "1001",
  "tableKey": "orders",
  "version": 1,
  "status": "Succeeded",
  "executedAt": "2026-03-03T12:00:00Z",
  "errorMessage": null
}
```

`precheck` 响应：

```json
{
  "id": "1001",
  "tableKey": "orders",
  "version": 1,
  "requiresConfirmation": true,
  "canExecute": true,
  "checks": [
    "迁移记录存在",
    "当前状态：Draft",
    "检测到破坏性变更，执行前需要确认"
  ]
}
```

### dbType 枚举

- `Sqlite`、`SqlServer`、`MySql`、`PostgreSql`

### 字段元数据接口

- `GET /api/v1/dynamic-tables/{tableKey}/fields`：字段列表
- `GET /api/v1/dynamic/meta/field-types?dbType=Sqlite`：字段类型枚举（用于前端联动）

### 记录 CRUD 接口

- `GET /api/v1/dynamic-tables/{tableKey}/records`：分页查询记录（关键词 + 排序）
- `GET /api/v1/dynamic-tables/{tableKey}/records/{id}`：单条记录
- `POST /api/v1/dynamic-tables/{tableKey}/records`：新增记录（需幂等 + CSRF）
- `PUT /api/v1/dynamic-tables/{tableKey}/records/{id}`：更新记录（需幂等 + CSRF）
- `DELETE /api/v1/dynamic-tables/{tableKey}/records/{id}`：删除记录（需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/records/query`：复杂筛选（支持 `eq/ne/gt/gte/lt/lte/like/in/between`，需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/records/export`：按筛选条件导出 CSV（需幂等 + CSRF，单次最多 10,000 条，分批查询避免 OOM）
- `POST /api/v1/dynamic-tables/{tableKey}/records/batch`：批量新增（需幂等 + CSRF）
- `DELETE /api/v1/dynamic-tables/{tableKey}/records`：批量删除（需幂等 + CSRF）

说明：

- 当目标表配置了字段级权限规则时，查询/详情/导出将按当前用户角色自动裁剪可见字段；
- 写入（create/update）会校验可编辑字段，越权字段写入将返回 `FORBIDDEN`。
- 当当前角色数据权限为“仅本人”时，动态记录查询/详情/导出将自动注入 owner 过滤（基于 `ownerId/createdBy/creatorId` 字段约定）。

### AMIS Schema 接口

- `GET /api/v1/amis/dynamic-tables/designer`：表结构设计器 Schema
- `GET /api/v1/amis/dynamic-tables/{tableKey}/crud`：动态 CRUD Schema（按字段实时生成列定义、排序与分页）
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/create`：新建表单 Schema
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/edit?id=1001`：编辑表单 Schema
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/detail?id=1001`：详情只读 Schema

### DynamicTableSummary

```json
{
  "id": "1001",
  "tableKey": "orders",
  "displayName": "订单",
  "description": "订单主表",
  "dbType": "Sqlite",
  "status": "Active",
  "createdAt": "2026-01-31T10:00:00Z",
  "createdBy": "10001"
}
```

### TableCreateRequest

```json
{
  "tableKey": "orders",
  "displayName": "订单",
  "description": "订单主表",
  "dbType": "Sqlite",
  "fields": [
    {
      "name": "id",
      "displayName": "主键",
      "fieldType": "Long",
      "isPrimaryKey": true,
      "isAutoIncrement": true,
      "allowNull": false
    },
    {
      "name": "orderNo",
      "displayName": "订单号",
      "fieldType": "String",
      "length": 50,
      "isUnique": true,
      "allowNull": false
    },
    {
      "name": "amount",
      "displayName": "金额",
      "fieldType": "Decimal",
      "precision": 18,
      "scale": 2,
      "allowNull": false
    }
  ],
  "indexes": [
    {
      "name": "idx_orders_no",
      "isUnique": true,
      "fields": ["orderNo"]
    }
  ]
}
```

### TableAlterRequest

> 当前阶段（M9 增量）支持 `addFields` 与 `updateFields`（仅允许更新字段显示名 `displayName` 与排序 `sortOrder`）；`removeFields` 仍返回 `VALIDATION_ERROR`。

```json
{
  "addFields": [
    { "name": "remark", "displayName": "备注", "fieldType": "String", "length": 200, "allowNull": true }
  ],
  "updateFields": [
    { "name": "amount", "displayName": "金额", "precision": 18, "scale": 4 }
  ],
  "removeFields": ["legacyField"]
}
```

### SchemaMigrationListItem

```json
{
  "id": "193500000000000001",
  "tableId": "193400000000000001",
  "tableKey": "orders",
  "operationType": "ADD_FIELDS",
  "status": "Succeeded",
  "appliedSql": "ALTER TABLE ...",
  "rollbackSql": "当前版本不支持自动回滚，请通过备份恢复。",
  "createdBy": 10001,
  "createdAt": "2026-03-03T10:00:00Z"
}
```

### AlterPreviewResponse

```json
{
  "tableKey": "orders",
  "operationType": "ADD_FIELDS",
  "sqlScripts": [
    "ALTER TABLE \"orders\" ADD COLUMN \"remark\" TEXT;",
    "CREATE UNIQUE INDEX \"uk_orders_remark\" ON \"orders\" (\"remark\");"
  ],
  "rollbackHint": "当前版本不支持自动回滚，请通过备份恢复。"
}
```

### FieldDefinition

```json
{
  "name": "orderNo",
  "displayName": "订单号",
  "fieldType": "String",
  "length": 50,
  "precision": 18,
  "scale": 2,
  "allowNull": false,
  "isPrimaryKey": false,
  "isAutoIncrement": false,
  "isUnique": true,
  "defaultValue": null,
  "validation": {
    "regex": "^[A-Za-z0-9_-]+$",
    "minLength": 1,
    "maxLength": 50
  }
}
```

### FieldValueDto

```json
{
  "field": "amount",
  "valueType": "Decimal",
  "decimalValue": 199.99
}
```

约束：仅允许填写一个具体值字段（`stringValue/intValue/longValue/decimalValue/boolValue/dateTimeValue/dateValue`）。

### DynamicRecordUpsertRequest

```json
{
  "values": [
    { "field": "orderNo", "valueType": "String", "stringValue": "SO-10001" },
    { "field": "amount", "valueType": "Decimal", "decimalValue": 199.99 },
    { "field": "createdAt", "valueType": "DateTime", "dateTimeValue": "2026-01-31T10:00:00Z" }
  ]
}
```

### DynamicRecordDto

```json
{
  "id": "1001",
  "values": [
    { "field": "orderNo", "valueType": "String", "stringValue": "SO-10001" },
    { "field": "amount", "valueType": "Decimal", "decimalValue": 199.99 }
  ]
}
```

### DynamicRecordQueryRequest

```json
{
  "pageIndex": 1,
  "pageSize": 20,
  "keyword": "SO-10001",
  "sortBy": "createdAt",
  "sortDesc": true,
  "filters": [
    { "field": "amount", "operator": "gte", "value": 100 },
    { "field": "status", "operator": "eq", "value": "Paid" }
  ]
}
```

### FilterOperator

- `eq`、`ne`、`gt`、`gte`、`lt`、`lte`、`like`、`in`、`between`

### DynamicColumnDef（AMIS 列配置）

```json
{
  "name": "orderNo",
  "label": "订单号",
  "type": "text",
  "sortable": true,
  "quickEdit": false,
  "searchable": true
}
```

### 动态列表响应（支持列定义）

当 `includeColumns=true` 时，响应数据附带 `columns` 供 AMIS 渲染。

```json
{
  "items": [
    {
      "id": "1001",
      "values": [
        { "field": "orderNo", "valueType": "String", "stringValue": "SO-10001" }
      ]
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 20,
  "columns": [
    { "name": "orderNo", "label": "订单号", "type": "text", "sortable": true }
  ]
}
```

### AMIS 模板引用

- `docs/amis-templates/dynamic-table-list.json`：动态表列表
- `docs/amis-templates/dynamic-table-designer.json`：表结构设计器
- `docs/amis-templates/dynamic-table-crud.json`：动态 CRUD 页面

## 用户/部门/职位管理契约

## 插件管理契约（AssemblyLoadContext）

- `GET /api/v1/plugins`：查询插件清单
- `POST /api/v1/plugins/reload`：重载插件目录并刷新清单

### PluginDescriptor

```json
{
  "code": "demo.plugin",
  "name": "Demo Plugin",
  "version": "1.0.0",
  "assemblyName": "Atlas.Plugin.Demo",
  "filePath": "/workspace/src/backend/Atlas.WebApi/plugins/Atlas.Plugin.Demo.dll",
  "state": "Loaded|Failed|NoEntryPoint",
  "loadedAt": "2026-03-03T12:00:00Z",
  "errorMessage": null
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

- `GET /api/v1/projects?PageIndex=1&PageSize=10&Keyword=核心`：分页查询项目
- `GET /api/v1/projects/{id}`：项目详情
- `POST /api/v1/projects`：新增项目
- `PUT /api/v1/projects/{id}`：更新项目
- `DELETE /api/v1/projects/{id}`：停用项目（逻辑删除）
- `PUT /api/v1/projects/{id}/users`：项目分配人员
- `PUT /api/v1/projects/{id}/departments`：项目分配部门
- `PUT /api/v1/projects/{id}/positions`：项目分配职位
- `GET /api/v1/projects/my`：当前用户可切换项目列表
- `GET /api/v1/projects/my/paged?PageIndex=1&PageSize=20&Keyword=核心`：当前用户可切换项目分页列表（用于下拉远程检索，默认返回 20 条）

说明：

- 项目编码 `code` 建议在租户范围内保持唯一。

### ProjectListItem

```json
{
  "id": "1001",
  "code": "ops-core",
  "name": "运维核心项目",
  "isActive": true,
  "description": "运维平台核心业务线",
  "sortOrder": 0
}
```

### ProjectCreateRequest

```json
{
  "code": "ops-core",
  "name": "运维核心项目",
  "isActive": true,
  "description": "运维平台核心业务线",
  "sortOrder": 0
}
```

### ProjectUpdateRequest

```json
{
  "name": "运维核心项目",
  "isActive": true,
  "description": "运维平台核心业务线",
  "sortOrder": 0
}
```

## 应用配置契约

### 应用配置

- `GET /api/v1/apps`：分页查询应用配置
- `GET /api/v1/apps/current`：当前应用配置
- `GET /api/v1/apps/{id}`：应用配置详情
- `PUT /api/v1/apps/{id}`：更新应用配置

### AppConfigResponse

```json
{
  "id": "1",
  "appId": "security-platform",
  "name": "SecurityPlatform",
  "isActive": true,
  "enableProjectScope": true,
  "description": "默认应用配置",
  "sortOrder": 0
}
```

### AppConfigUpdateRequest

```json
{
  "name": "SecurityPlatform",
  "isActive": true,
  "enableProjectScope": false,
  "description": "默认应用配置",
  "sortOrder": 0
}
```

字段说明：

- `enableProjectScope`：是否启用项目模式（应用级开关，启用后业务数据必须带 `project_id`）。

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

## 审批流 V1V2 增量契约（2026-03）

### 流程定义管理新增接口

- `POST /api/v1/approval/flows/{id}/copy`：复制现有流程为新草稿。
  - 请求：`{ "name"?: string }`
  - 响应：`ApprovalFlowDefinitionResponse`
- `GET /api/v1/approval/flows/{id}/export`：导出流程定义 JSON。
  - 响应：`ApprovalFlowExportResponse`
- `POST /api/v1/approval/flows/import`：导入流程定义 JSON 为新草稿。
  - 请求：`ApprovalFlowImportRequest`
  - 响应：`ApprovalFlowDefinitionResponse`
- `GET /api/v1/approval/flows/{id}/versions/{targetVersion}/compare`：按流程名 + 版本号对比定义差异。
  - 响应：`ApprovalFlowCompareResponse`

### 流程校验返回增强

- `POST /api/v1/approval/flows/validation` 响应新增 `details` 字段（兼容 `errors`/`warnings`）。

```json
{
  "isValid": false,
  "errors": ["并行节点必须配置汇聚后的后续节点"],
  "warnings": ["并行块缺少 groupId 标识"],
  "details": [
    {
      "code": "PARALLEL_JOIN_REQUIRED",
      "message": "并行节点必须配置汇聚后的后续节点",
      "severity": "error",
      "nodeId": "parallel_xxx",
      "edgeId": null
    }
  ]
}
```

### 运行时实例管理新增接口

- `GET /api/v1/approval/instances/admin`：管理端实例分页查询（支持过滤）。
  - Query 参数：
    - `pageIndex`, `pageSize`
    - `definitionId?`
    - `initiatorUserId?`
    - `businessKey?`
    - `startedFrom?`（ISO8601）
    - `startedTo?`（ISO8601）
    - `status?`（`Running|Completed|Rejected|Canceled`）
  - 响应：`PagedResult<ApprovalInstanceListItem>`

### 前端运行态新增页面路由（动态菜单）

- `/process/start`：发起审批
- `/process/inbox`：我的待办
- `/process/done`：我的已办
- `/process/my-requests`：我发起的
- `/process/cc`：我的抄送
- `/process/manage/flows`：流程定义管理
- `/process/manage/instances`：流程实例管理
- `/process/designer/:id`、`/process/tasks/:id`、`/process/instances/:id`：隐藏详情路由

## 低代码设计器版本管理契约（2026-03）

### 表单版本管理

#### FormDefinitionVersionListItem

```json
{
  "id": 123456789,
  "formDefinitionId": 987654321,
  "snapshotVersion": 3,
  "name": "客户信息登记表",
  "description": "第三版",
  "category": "CRM",
  "icon": "file-form",
  "dataTableKey": "customer_info",
  "createdBy": 111222333,
  "createdAt": "2026-03-01T10:00:00Z"
}
```

#### FormDefinitionVersionDetail

在 `FormDefinitionVersionListItem` 基础上增加：

```json
{
  "schemaJson": "{ ... }"
}
```

#### 接口列表

- `GET /api/v1/form-definitions/{id}/versions`：查询表单版本历史列表。
  - 响应：`FormDefinitionVersionListItem[]`
- `GET /api/v1/form-definitions/{id}/versions/{versionId}`：查询指定版本详情（含完整 schemaJson）。
  - 响应：`FormDefinitionVersionDetail`
- `POST /api/v1/form-definitions/{id}/rollback/{versionId}`：将表单定义回滚至指定历史版本，并创建新快照。
  - 请求：无 Body（路由参数）
  - 需要：`Idempotency-Key` + `X-CSRF-TOKEN`
  - 触发审计：`LowCode.FormDefinition.RolledBack`

### 审批流版本管理

#### ApprovalFlowVersionListItem

```json
{
  "id": 123456789,
  "definitionId": 987654321,
  "snapshotVersion": 2,
  "name": "采购审批流",
  "description": "V2 优化",
  "category": "采购",
  "createdBy": 111222333,
  "createdAt": "2026-03-02T09:00:00Z"
}
```

#### ApprovalFlowVersionDetail

在 `ApprovalFlowVersionListItem` 基础上增加：

```json
{
  "definitionJson": "{ ... }",
  "visibilityScopeJson": "{ ... }"
}
```

#### 接口列表

- `GET /api/v1/approval/flows/{id}/versions`：查询审批流版本历史列表。
  - 响应：`ApprovalFlowVersionListItem[]`
- `GET /api/v1/approval/flows/{id}/versions/{versionId}/detail`：查询指定版本详情（含完整 definitionJson）。
  - 响应：`ApprovalFlowVersionDetail`
- `POST /api/v1/approval/flows/{id}/rollback/{versionId}`：将审批流定义回滚至指定历史版本，并创建新快照。
  - 请求：无 Body（路由参数）
  - 需要：`Idempotency-Key` + `X-CSRF-TOKEN`
  - 触发审计：`Approval.FlowDefinition.RolledBack`

### 设计态审计埋点

发布和回滚操作均写入 `AuditRecord`，`action` 字段取值规范：

| 场景 | `action` |
|---|---|
| 表单定义发布 | `LowCode.FormDefinition.Published` |
| 表单定义回滚 | `LowCode.FormDefinition.RolledBack` |
| 审批流定义发布 | `Approval.FlowDefinition.Published` |
| 审批流定义回滚 | `Approval.FlowDefinition.RolledBack` |
| 低代码页面发布 | `LowCode.Page.Published` |
| 低代码页面回滚 | `LowCode.Page.RolledBack` |

`target` 格式：`{EntityType}:{id}` 或 `{EntityType}:{id}:Version:{versionId}`（回滚时附带版本 ID）。
