# 登录日志 + 在线用户实施计划

> Phase 1 功能，等保2.0关联要求（8.1.2/8.1.3）：
> - 须记录每次登录成功/失败事件（含时间、IP、用户名、结果）
> - 登录日志须至少保留 6 个月
> - 须能查看并强制下线在线用户（会话管理）

---

## 一、功能说明

### 1.1 登录日志

记录每一次登录尝试（成功/失败），供审计人员查看。写入时机：
- 登录成功 → LoginStatus=true
- 登录失败（密码错误、账号锁定、MFA失败）→ LoginStatus=false + Message

保留策略：
- 配合现有 `AuditRetentionHostedService` 或单独增加清理策略
- 默认保留180天（6个月，满足等保最低要求）

### 1.2 在线用户（会话管理）

基于现有 `AuthSession` 表实现：
- 查询活跃会话列表（未被吊销且未过期）
- 管理员可强制下线任意会话
- 当前用户可下线自己的其他会话

---

## 二、数据模型

### LoginLog（已在 Domain 层创建）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 雪花ID |
| TenantIdValue | Guid | 租户隔离 |
| Username | string | 登录用户名 |
| IpAddress | string | 客户端IP |
| Browser | string? | 浏览器（从UA解析） |
| OperatingSystem | string? | 操作系统（从UA解析） |
| LoginStatus | bool | true=成功, false=失败 |
| Message | string? | 失败原因/备注 |
| LoginTime | DateTimeOffset | 登录时间 |

### OnlineUserDto（只读，来自 AuthSession）

| 字段 | 类型 | 说明 |
|------|------|------|
| SessionId | long | 会话ID |
| UserId | long | 用户ID |
| Username | string | 用户名 |
| IpAddress | string | 登录IP |
| Browser | string | 客户端类型 |
| LoginTime | DateTimeOffset | 登录时间 |
| LastSeenAt | DateTimeOffset | 最后活跃时间 |
| ExpiresAt | DateTimeOffset | 会话过期时间 |

---

## 三、API 契约

### 3.1 登录日志接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/v1/login-logs` | 分页查询登录日志（支持用户名、IP、状态、时间范围过滤）| `loginlog:view` |
| DELETE | `/api/v1/login-logs/{id}` | 删除单条日志（按需，建议由定时清理替代）| `loginlog:delete` |
| DELETE | `/api/v1/login-logs/batch` | 批量清理（按时间范围）| `loginlog:delete` |

### 3.2 在线用户接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/v1/sessions` | 查询在线用户（活跃会话分页）| `online:view` |
| DELETE | `/api/v1/sessions/{sessionId}` | 强制下线指定会话 | `online:force-logout` |

---

## 四、后端实现步骤

### 4.1 已有：Domain/LoginLog.cs

Domain 层实体已创建。

### 4.2 Application 层

- `Atlas.Application/System/Models/LoginLogModels.cs`（DTO + 查询请求）
- `Atlas.Application/System/Abstractions/ILoginLogWriteService.cs`（写入接口，供 JwtAuthTokenService 调用）
- `Atlas.Application/System/Abstractions/ILoginLogQueryService.cs`（查询接口）

### 4.3 Infrastructure 层

- `Atlas.Infrastructure/Repositories/LoginLogRepository.cs`
- `Atlas.Infrastructure/Services/LoginLogWriteService.cs`（写入登录日志，注入到 JwtAuthTokenService）
- `Atlas.Infrastructure/Services/LoginLogQueryService.cs`

### 4.4 WebApi 层

- `Atlas.WebApi/Controllers/LoginLogsController.cs`
- `Atlas.WebApi/Controllers/SessionsController.cs`（查询在线用户 + 强制下线）
- `Atlas.WebApi/Bosch.http/LoginLogs.http`

### 4.5 登录钩子

在 `JwtAuthTokenService.CreateTokenAsync` 中：
- 登录成功后写入 LoginLog（status=true）
- 登录失败时捕获异常并写入 LoginLog（status=false，message=原因）

---

## 五、前端实现步骤

### 5.1 Service 层

- `src/services/login-log.ts`：getLoginLogsPaged、deleteLoginLog
- `src/services/sessions.ts`：getOnlineUsers、forceLogout

### 5.2 页面

- `src/pages/system/LoginLogsPage.vue`：
  - 搜索条件：用户名、IP、状态（全部/成功/失败）、时间范围
  - 列表：用户名、IP、浏览器、OS、状态（标签颜色区分）、登录时间
- `src/pages/system/OnlineUsersPage.vue`：
  - 活跃会话列表：用户名、IP、客户端、登录时间、最后活跃、到期时间
  - 强制下线按钮（二次确认）

---

## 六、等保2.0 合规要点

- 登录日志写入必须异步+容错（不能因日志写入失败而阻断登录）
- 登录日志不可篡改（只有删除权限，无编辑权限）
- 保留期 ≥ 180 天（默认清理策略配置）
- 强制下线必须有操作审计记录

---

## 七、验收标准

- [ ] 登录成功后 LoginLog 表有新记录（status=true）
- [ ] 密码错误后 LoginLog 表有新记录（status=false，message=密码错误）
- [ ] GET /login-logs 按用户名/状态/时间范围过滤正常
- [ ] GET /sessions 返回活跃会话列表
- [ ] DELETE /sessions/{id} 强制下线后该 token 访问返回 401
- [ ] 前端登录日志页状态标签颜色正确（成功=绿，失败=红）
- [ ] 前端在线用户页强制下线有二次确认
