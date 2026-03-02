# 通知公告

> 文档版本：v1.0 | 等保2.0 覆盖：安全审计、访问控制、数据保密性

---

## 一、功能描述

**通知公告**模块允许管理员向系统用户推送公告与消息，用户可在顶栏铃铛图标处查看未读通知数量，点击后进入通知列表页面查看并管理消息。

### 核心功能

| 功能 | 说明 |
|------|------|
| 公告发布 | 管理员创建、编辑、删除公告，支持标题、内容、类型、有效期 |
| 公告接收 | 公告按租户范围推送给指定用户或所有用户 |
| 未读计数 | 用户登录后可实时获取未读通知数量 |
| 标记已读 | 用户可单条或全部标记已读 |
| 通知列表 | 分页展示通知，支持按状态（未读/已读）过滤 |
| 顶栏铃铛 | 全局导航栏显示未读数量徽标，点击跳转通知页 |

### 等保2.0 合规要求

- 公告操作（创建、编辑、删除）均记录操作日志（`RecordAuditAsync`）
- 公告内容经过 XSS 净化后存储（注意白名单配置）
- 用户只能查看属于自己租户的通知，强制租户隔离

---

## 二、产品架构清单（实现追踪）

### Phase 3A — 后端实现

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| A1 | Domain | `Atlas.Domain/System/Entities/Notification.cs` | 公告实体（标题、内容、类型、优先级、发布时间、过期时间） | ☐ |
| A2 | Domain | `Atlas.Domain/System/Entities/UserNotification.cs` | 用户-公告关联实体（UserId、NotificationId、IsRead、ReadTime） | ☐ |
| A3 | Application | `System/Models/NotificationModels.cs` | DTO 和请求模型 | ☐ |
| A4 | Application | `System/Abstractions/INotificationService.cs` | 查询+命令接口 | ☐ |
| A5 | Application | `System/Validators/NotificationValidators.cs` | FluentValidation 验证 | ☐ |
| A6 | Infrastructure | `Repositories/NotificationRepository.cs` | 公告 CRUD 仓储 | ☐ |
| A7 | Infrastructure | `Repositories/UserNotificationRepository.cs` | 用户通知状态仓储 | ☐ |
| A8 | Infrastructure | `Services/NotificationService.cs` | 业务逻辑（发布、推送、标记已读） | ☐ |
| A9 | Infrastructure | `Services/DatabaseInitializerHostedService.cs` | InitTables 添加两个实体 | ☐ |
| A10 | Infrastructure | `DependencyInjection/CoreServiceRegistration.cs` | 注册服务和仓储 | ☐ |
| A11 | WebApi | `Controllers/NotificationsController.cs` | CRUD + 未读数量 + 标记已读端点 | ☐ |
| A12 | WebApi | `Authorization/PermissionPolicies.cs` | 添加通知权限常量 | ☐ |

### Phase 3B — 前端实现

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| B1 | Service | `services/notification.ts` | 通知 API 服务 | ☐ |
| B2 | Component | `components/layout/NotificationBell.vue` | 顶栏铃铛组件（未读数徽标 + 下拉快捷列表） | ☐ |
| B3 | Layout | `layouts/MainLayout.vue` 或顶栏组件 | 集成 NotificationBell | ☐ |
| B4 | Page | `pages/system/NotificationsPage.vue` | 用户通知列表页（分页、已读/未读过滤、标记已读） | ☐ |
| B5 | Page | `pages/system/NotificationManagePage.vue` | 管理员公告管理页（CRUD） | ☐ |
| B6 | Router | `router/index.ts` | 添加通知页面路由 | ☐ |

---

## 三、数据模型

### Notification（公告实体）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| TenantIdValue | string | 租户 ID |
| Title | string(200) | 公告标题 |
| Content | string(4000) | 公告内容（富文本） |
| NoticeType | string(20) | 类型：`Announcement`/`System`/`Reminder` |
| Priority | int | 优先级：0普通/1重要/2紧急 |
| PublisherId | long | 发布人 UserId |
| PublisherName | string(100) | 发布人姓名 |
| PublishedAt | DateTimeOffset | 发布时间 |
| ExpiresAt | DateTimeOffset? | 过期时间（null=永不过期） |
| IsActive | bool | 是否启用 |
| CreatedAt | DateTimeOffset | 创建时间 |

### UserNotification（用户通知关联）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| TenantIdValue | string | 租户 ID |
| UserId | long | 用户 ID |
| NotificationId | long | 公告 ID |
| IsRead | bool | 是否已读 |
| ReadAt | DateTimeOffset? | 阅读时间 |

---

## 四、API 端点

### 管理员公告管理

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/notifications/manage` | `notification:manage:view` | 分页查询公告列表 |
| POST | `/api/notifications/manage` | `notification:manage:create` | 创建公告 |
| PUT | `/api/notifications/manage/{id}` | `notification:manage:edit` | 更新公告 |
| DELETE | `/api/notifications/manage/{id}` | `notification:manage:delete` | 删除公告（记录审计） |

### 用户通知

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/notifications` | 已登录 | 当前用户通知列表（分页） |
| GET | `/api/notifications/unread-count` | 已登录 | 未读通知数量 |
| PUT | `/api/notifications/{id}/read` | 已登录 | 标记单条已读 |
| PUT | `/api/notifications/read-all` | 已登录 | 全部标记已读 |

---

## 五、验收标准

### 后端

- [ ] 管理员创建公告后，所有同租户用户的 `UserNotification` 记录自动生成
- [ ] `GET /api/notifications/unread-count` 正确返回当前用户的未读数
- [ ] 标记已读后再次查询未读数减少
- [ ] 已过期公告（ExpiresAt < now）不出现在用户列表中
- [ ] 删除公告时同时删除关联 `UserNotification` 记录
- [ ] 公告管理操作均有审计日志

### 前端

- [ ] 顶栏铃铛显示未读徽标（数量 > 0 时显示数字，最大显示 99+）
- [ ] 铃铛下拉展示最近 5 条未读，底部有"查看全部"链接
- [ ] 通知列表页支持未读/已读 Tab 切换
- [ ] 点击通知条目自动标记已读并刷新徽标数
- [ ] 管理员公告管理页支持完整 CRUD 并有内容预览
