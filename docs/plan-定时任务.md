# 定时任务

> 文档版本：v1.0 | 等保2.0 覆盖：安全审计、访问控制

---

## 一、功能描述

提供基于 Hangfire 的持久化定时任务管理功能，支持管理员在界面上查看、触发和管理后台任务。

### 核心功能

| 功能 | 说明 |
|------|------|
| 任务列表 | 分页展示所有注册的 Cron 任务（名称、表达式、状态、上次运行时间） |
| 立即触发 | 手动触发一次任务执行 |
| 暂停/恢复 | 停用/启用定时任务 |
| 执行历史 | 查看任务的最近执行记录（状态、耗时、错误信息） |

---

## 二、产品架构清单（实现追踪）

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| A1 | Domain | `Atlas.Domain/System/Entities/ScheduledJob.cs` | 定时任务配置实体 | ☐ |
| A2 | Application | `System/Models/ScheduledJobModels.cs` | DTO 和请求模型 | ☐ |
| A3 | Application | `System/Abstractions/IScheduledJobService.cs` | 查询接口 | ☐ |
| A4 | Infrastructure | `Services/HangfireScheduledJobService.cs` | 使用 Hangfire API 实现 | ☐ |
| A5 | Infrastructure | `CoreServiceRegistration.cs` | 注册 Hangfire + 服务 | ☐ |
| A6 | WebApi | `Controllers/ScheduledJobsController.cs` | 查询/触发端点 | ☐ |
| A7 | WebApi | `Program.cs` | 配置 Hangfire Dashboard | ☐ |
| B1 | Frontend | `pages/monitor/ScheduledJobsPage.vue` | 任务列表 + 操作按钮 | ☐ |

---

## 三、数据模型

### ScheduledJobDto

```typescript
interface ScheduledJobDto {
  id: string;
  name: string;
  cronExpression: string;
  jobType: string;
  isEnabled: boolean;
  lastRunAt?: string;
  lastRunStatus?: string;
  nextRunAt?: string;
}
```

---

## 四、API 端点

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/scheduled-jobs` | `job:view` | 分页查询定时任务 |
| POST | `/api/scheduled-jobs/{id}/trigger` | `job:trigger` | 立即触发任务 |
| PUT | `/api/scheduled-jobs/{id}/enable` | `job:update` | 启用任务 |
| PUT | `/api/scheduled-jobs/{id}/disable` | `job:update` | 禁用任务 |

---

## 五、验收标准

- [ ] 后端 Hangfire 正常工作，任务按配置 Cron 执行
- [ ] `GET /api/scheduled-jobs` 返回已注册任务列表
- [ ] `POST /api/scheduled-jobs/{id}/trigger` 立即触发任务并返回成功
- [ ] 前端列表页展示任务信息，包含"立即执行"和"启用/禁用"按钮
- [ ] 触发任务操作有审计日志
