# 服务监控

> 文档版本：v1.0 | 等保2.0 覆盖：安全审计、访问控制

---

## 一、功能描述

提供服务器运行状态实时监控，管理员可查看 CPU、内存、磁盘、运行时等核心指标，用于运维运营保障。

### 核心功能

| 功能 | 说明 |
|------|------|
| CPU 信息 | 逻辑核心数、当前占用率（进程级） |
| 内存信息 | 总内存、已用内存、可用内存、使用率（% ） |
| 磁盘信息 | 各分区总大小、已用、可用 |
| 运行时信息 | .NET 版本、OS 信息、应用启动时间、运行时长 |
| 进程信息 | 进程 ID、线程数、GC 内存 |
| 定时刷新 | 前端每 30 秒自动刷新一次 |

---

## 二、产品架构清单（实现追踪）

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| A1 | Application | `Application/Monitor/Models/ServerInfoDto.cs` | 服务器信息 DTO | ☐ |
| A2 | Application | `Application/Monitor/Abstractions/IServerInfoQueryService.cs` | 查询接口 | ☐ |
| A3 | Infrastructure | `Services/ServerInfoQueryService.cs` | 使用 `System.Diagnostics` 和 `Environment` 实现 | ☐ |
| A4 | Infrastructure | `CoreServiceRegistration.cs` | 注册服务 | ☐ |
| A5 | WebApi | `Controllers/MonitorController.cs` | GET /server-info 端点 | ☐ |
| B1 | Frontend | `pages/monitor/ServerInfoPage.vue` | 卡片布局展示各项指标，自动刷新 | ☐ |

---

## 三、数据模型

### ServerInfoDto

```csharp
public sealed record ServerInfoDto(
    CpuInfoDto Cpu,
    MemoryInfoDto Memory,
    IReadOnlyList<DiskInfoDto> Disks,
    RuntimeInfoDto Runtime);

public sealed record CpuInfoDto(int LogicalCores, double ProcessCpuUsagePercent);

public sealed record MemoryInfoDto(
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes,
    double UsagePercent);

public sealed record DiskInfoDto(
    string Name,
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes);

public sealed record RuntimeInfoDto(
    string DotNetVersion,
    string OsDescription,
    string MachineName,
    int ProcessId,
    int ThreadCount,
    long GcMemoryBytes,
    DateTimeOffset StartedAt,
    TimeSpan Uptime);
```

---

## 四、API 端点

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/monitor/server-info` | `monitor:view` | 获取服务器当前状态快照 |

---

## 五、验收标准

- [ ] GET `/api/monitor/server-info` 返回 CPU、内存、磁盘、运行时信息
- [ ] 未授权用户访问返回 403
- [ ] 前端页面卡片布局展示各项指标
- [ ] 页面每 30 秒自动刷新并更新数据
- [ ] 内存/磁盘使用率通过进度条可视化展示
