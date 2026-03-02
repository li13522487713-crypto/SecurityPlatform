# 等保三级控制点证据映射（Atlas Security Platform）

## 目标

将“控制点要求”映射到“系统实现、配置位置、日志证据与导出路径”，用于测评取证与内部审计。

## 映射表

| 控制点 | 控制要求 | 平台实现 | 配置/代码位置 | 证据位置 | 导出方式 | 责任角色 |
|---|---|---|---|---|---|---|
| 8.1.3 身份鉴别 | 身份唯一、口令复杂度、失败锁定 | JWT + 密码策略 + 锁定 + MFA | `src/backend/Atlas.WebApi/Controllers/AuthController.cs`、`src/backend/Atlas.WebApi/Controllers/MfaController.cs`、`appsettings.json` `Security` | 登录日志、审计日志 | API 导出登录日志 + 审计查询导出 | 安全管理员 |
| 8.1.3 输入安全 | 防 XSS、参数校验 | XSS 中间件 + FluentValidation | `src/backend/Atlas.WebApi/Middlewares/XssProtectionMiddleware.cs`、各 Validator | 错误日志、审计日志 | 错误日志归档 + 审计导出 | 开发负责人 |
| 8.1.4 访问控制 | 最小权限、分权、数据权限 | RBAC + Policy + DataScope | `src/backend/Atlas.WebApi/Authorization/*`、`RolesController`、`DataScopeFilter` | 授权变更审计、访问拒绝日志 | 审计 API 导出 | 权限管理员 |
| 8.1.5 安全审计 | 关键行为留痕、可追溯 | 统一审计记录器 + 业务操作审计 | `Atlas.Application.Audit`、各 Controller `RecordAuditAsync` | 审计记录表 | 审计分页查询导出 | 审计员 |
| 8.1.6 运行安全 | 运行监测与告警 | Monitor + OpenTelemetry traces/metrics | `src/backend/Atlas.WebApi/Controllers/MonitorController.cs`、`Program.cs` OpenTelemetry 配置 | 监控日志、OTLP 数据 | 监控平台报表导出 | 运维管理员 |
| 8.1.8 备份恢复 | 定期备份、恢复可演练 | 数据库备份托管服务 | `Atlas.Infrastructure/Services/DatabaseBackupHostedService.cs`、`appsettings.json` `Database:Backup` | 备份文件目录、任务日志 | 文件快照 + 日志导出 | DBA/运维 |

## 周期与产出

- 周期：每月一次证据归档，每季度一次恢复演练。
- 归档物：审计导出包、配置快照、备份校验记录、监控报表。
- 结论模板：通过/不通过 + 整改项 + 责任人 + 期限。
