# Plan: 平台控制台与应用数据源

> 本文档定义平台控制台布局、应用工作台、应用级数据源、数据共享策略及实体别名的完整需求与设计约束。  
> 更新规则：架构变更或功能完成时同步更新本文档。

---

## 一、背景与目标

### 1.1 背景

当前前端采用单层管理后台布局，登录后直接进入带侧边栏的传统后台，存在以下问题：

- 缺少「平台控制台」概念，应用与平台管理混在一起
- 应用与数据源的关系不清晰
- 基础数据（用户/角色/部门）的跨应用共享与隔离无法配置

### 1.2 目标

- 登录后进入**平台控制台**（纯顶部导航），可新建应用、配置数据源、执行数据迁移
- 点击应用后进入**应用工作台**（应用专属侧边栏）
- 每个应用可绑定**独立数据源**（支持 SqlSugar 多驱动）
- 基础数据支持**应用级共享策略**（继承平台 / 应用独立）
- 支持**实体别名**，不同应用可对同一实体使用不同显示名称

---

## 二、核心约束（必读）

### 2.1 数据源不可变

| 约束 | 说明 |
|------|------|
| **创建后不可更改** | 应用创建时绑定的数据源（`DataSourceId`）一旦确定，**永不允许修改** |
| **原因** | 数据源绑定物理存储位置，应用内所有动态表、表单数据、审批记录已写入该数据源；变更会导致历史数据孤立、schema 缺失、关联失效 |
| **实现** | `LowCodeApp.DataSourceId` 仅构造函数可设，无 `UpdateDataSource` 方法；`PUT /api/v1/apps/{id}` 的 DTO 不含 `DataSourceId` |

### 2.2 可修改的基础数据

| 可修改项 | 说明 |
|----------|------|
| 应用元信息 | Name、Description、Icon、Category |
| 数据共享策略 | UseSharedUsers、UseSharedRoles、UseSharedDepartments |
| 实体别名 | user/role/department 的 SingularAlias、PluralAlias |
| 数据源测试 | 仅允许「重新测试连接」，不允许修改连接参数 |

### 2.3 不可修改项

| 不可修改项 | 说明 |
|------------|------|
| AppKey | 应用唯一标识，创建后不可改 |
| DataSourceId | 数据源绑定，创建后不可改 |

---

## 三、信息架构与路由

### 3.1 三段路由结构

```
/login                      → 登录
/register                   → 注册

/console                    → 平台控制台（ConsoleLayout，纯顶部导航）
/console/apps               → 应用卡片网格
/console/datasources        → 平台数据源管理
/console/migration          → 数据迁移中心
/console/settings/users     → 平台用户管理
/console/settings/roles     → 平台角色管理
/console/settings/depts     → 平台部门管理

/apps/:appId                → 应用工作台（AppWorkspaceLayout，左侧边栏）
/apps/:appId/dashboard      → 应用仪表盘
/apps/:appId/forms          → 表单管理
/apps/:appId/builder        → 低代码设计器
/apps/:appId/approval       → 审批流
/apps/:appId/workflow       → 工作流
/apps/:appId/settings       → 应用设置
/apps/:appId/settings/datasource  → 数据源（只读 + 测试连接）
/apps/:appId/settings/sharing     → 数据共享策略
/apps/:appId/settings/aliases     → 实体别名

/settings/*                 → 原有系统设置（保持兼容）
```

### 3.2 登录后默认跳转

登录成功后跳转至 `/console`（不再跳转至 `/profile`）。

### 3.3 布局差异

| 布局 | 导航方式 | 适用场景 |
|------|----------|----------|
| **ConsoleLayout** | 纯顶部横向导航，无侧边栏 | 平台控制台、应用列表、数据源、迁移 |
| **AppWorkspaceLayout** | 左侧边栏 + 顶部栏 | 进入某应用后的工作空间 |

---

## 四、数据模型

### 4.1 LowCodeApp 扩展

```csharp
// 新增/修改属性

/// <summary>绑定的应用级数据源 ID（null 表示使用平台默认数据源）</summary>
/// <remarks>创建后不可更改，仅构造函数可设</remarks>
public long? DataSourceId { get; private set; }

/// <summary>是否继承平台用户池</summary>
public bool UseSharedUsers { get; private set; } = true;

/// <summary>是否继承平台角色</summary>
public bool UseSharedRoles { get; private set; } = true;

/// <summary>是否继承平台部门</summary>
public bool UseSharedDepartments { get; private set; } = true;
```

### 4.2 TenantDataSource 扩展

```csharp
// 新增属性

/// <summary>所属应用 ID（null=平台级；有值=应用专属）</summary>
public long? AppId { get; set; }

/// <summary>连接池最大连接数</summary>
public int MaxPoolSize { get; set; } = 100;

/// <summary>连接超时（秒）</summary>
public int ConnectionTimeoutSeconds { get; set; } = 30;

/// <summary>最近一次测试结果</summary>
public bool? LastTestSuccess { get; set; }
public DateTimeOffset? LastTestedAt { get; set; }
```

### 4.3 AppEntityAlias（新增实体）

```csharp
/// <summary>应用内实体显示别名（创建后可修改）</summary>
public sealed class AppEntityAlias : EntityBase
{
    public long AppId { get; set; }

    /// <summary>"user" | "role" | "department"</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>单数别名，如"员工"</summary>
    public string SingularAlias { get; set; } = string.Empty;

    /// <summary>复数别名，如"员工列表"（可选）</summary>
    public string? PluralAlias { get; set; }
}
```

### 4.4 关系示意

```
Tenant (租户)
  ├── TenantDataSource [AppId = null]  ← 平台级数据源
  │
  └── LowCodeApp (应用)
        ├── DataSourceId → TenantDataSource [AppId = AppId] ← 应用专属（创建后不可改）
        ├── UseSharedUsers / Roles / Departments
        └── AppEntityAlias[] ← 实体别名（可修改）
```

---

## 五、应用创建向导（三步）

数据源在创建时一次性决定，必须通过**三步向导**完成。

### 5.1 步骤 1：基本信息

| 字段 | 必填 | 说明 |
|------|------|------|
| 应用名称 | ✓ | 显示名称 |
| 应用标识 (AppKey) | ✓ | 唯一，创建后不可改 |
| 描述 | - | |
| 图标 / 分类 | - | |

### 5.2 步骤 2：绑定数据源（创建后不可更改）

| 选项 | 说明 |
|------|------|
| 使用平台默认数据源 | DataSourceId = null |
| 选择已有数据源 | 从租户下已有数据源下拉选择 |
| 创建新数据源 | 填写连接参数，测试通过后创建并绑定 |

**必显提示**：`⚠️ 数据源绑定后不可更改，请谨慎选择`

**校验**：若选择「创建新数据源」，必须「测试连接」通过后才能进入下一步。

### 5.3 步骤 3：数据共享策略与别名（创建后可修改）

| 配置项 | 说明 |
|--------|------|
| 用户账号来源 | 平台共享 / 应用独立 |
| 角色权限来源 | 平台共享 / 应用独立 |
| 部门组织来源 | 平台共享 / 应用独立 |
| 实体别名 | user→员工、role→岗位、department→部门 等（可选） |

**校验**：若 UseSharedXxx = false，则 DataSourceId 必须非空（独立数据需独立数据源存储）。

---

## 六、应用设置页

### 6.1 数据源（只读 + 测试）

```
🔒 数据源（不可更改）

类型：MySQL
服务器：192.168.1.100:3306
数据库：order_db
状态：● 连接正常

[重新测试连接]   ← 仅允许测试，不允许修改连接参数
```

### 6.2 数据共享策略（可修改）

```
用户账号  [● 继承平台  ○ 应用独立]
角色权限  [● 继承平台  ○ 应用独立]
部门组织  [● 继承平台  ○ 应用独立]

⚠️ 从「共享」切换为「独立」时，将从平台数据初始化一份副本。
   切换前请确认应用专属数据源已配置。

[保存配置]
```

### 6.3 实体别名（可修改）

```
用户(user)   称为  [员工    ]  列表称为  [员工列表    ]
角色(role)   称为  [岗位    ]  列表称为  [岗位列表    ]
部门(dept)   称为  [部门    ]  列表称为  [部门列表    ]

[保存]
```

---

## 七、API 设计

### 7.1 应用相关

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/apps` | 创建应用，请求体含 `dataSourceId`、`useShared*`、`aliases` |
| PUT | `/api/v1/apps/{id}` | 更新应用，**不含** `dataSourceId`、`appKey` |
| GET | `/api/v1/apps/{id}/datasource` | 获取应用数据源（只读，脱敏） |
| POST | `/api/v1/apps/{id}/datasource/test` | 测试连接 |
| GET | `/api/v1/apps/{id}/sharing-policy` | 获取共享策略 |
| PUT | `/api/v1/apps/{id}/sharing-policy` | 更新共享策略 |
| GET | `/api/v1/apps/{id}/entity-aliases` | 获取实体别名 |
| PUT | `/api/v1/apps/{id}/entity-aliases` | 更新实体别名 |

### 7.2 数据迁移

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/migration/dry-run` | 预演迁移 |
| POST | `/api/v1/migration/execute` | 执行迁移 |

### 7.3 平台数据源

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/v1/tenant-datasources?scope=platform` | 平台级数据源列表 |
| GET | `/api/v1/tenant-datasources?scope=app&appId={id}` | 应用专属数据源 |

---

## 八、SqlSugar 多驱动支持

### 8.1 NuGet 包补充

```xml
<!-- Atlas.Infrastructure.csproj -->

<PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.1" />
<PackageReference Include="MySqlConnector" Version="2.4.0" />
<PackageReference Include="Npgsql" Version="9.0.4" />
<!-- Oracle 按需：Oracle.ManagedDataAccess.Core -->
```

### 8.2 支持的 DbType

| 类型 | 枚举值 | 说明 |
|------|--------|------|
| SQLite | Sqlite | 内置支持 |
| SQL Server | SqlServer | Microsoft.Data.SqlClient |
| MySQL | MySql | MySqlConnector |
| PostgreSQL | PostgreSQL | Npgsql |
| Oracle | Oracle | Oracle.ManagedDataAccess.Core（可选） |

### 8.3 数据源表单（按类型动态渲染）

- SQLite：文件路径
- SQL Server：服务器、端口(1433)、数据库、用户名、密码、加密选项
- MySQL：服务器、端口(3306)、数据库、用户名、密码
- PostgreSQL：服务器、端口(5432)、数据库、用户名、密码

密码字段：只写不读，存储时 AES-256 加密。

---

## 九、数据迁移中心（/console/migration）

### 9.1 流程

1. 选择来源应用
2. 选择目标应用
3. 选择迁移类型：用户账号、角色定义、部门组织、字典数据
4. 冲突策略：跳过重复 / 覆盖更新 / 中止
5. 预演 (Dry Run) → 显示影响条数
6. 执行迁移 → 进度条 + 逐条结果
7. 下载迁移报告

### 9.2 约束

- 目标应用必须已配置独立数据源（若迁移独立数据）
- 来源与目标不能相同

---

## 十、执行摘要（规划版）

采用 **8 个 Sprint + 1 个发布关口** 的增量策略，从“平台/应用信息架构骨架”演进到“低代码 + 审批 + 数据仓储 + 模板生态 + 移动端”完整闭环。

当前基线（已确认）：

- 前端虽已有 `ConsoleLayout`/`AppWorkspaceLayout`，但仍需持续做路由、契约、工作台体验的一致性收敛。
- `LowCodeApp` 与 `TenantDataSource` 已扩展基础字段，仍需围绕应用域治理、运行态、模板与移动端补齐全链路。
- 工作流、审批、动态表、模板能力已有基础，但仍存在前后端契约与运行语义收敛空间。
- 发布关口新增为**强制项**：完整 GUI 手工测试、提交 `atlas.db`。

---

## 十一、总体实施原则（全 Sprint 生效）

- 小步闭环：每个 Sprint 拆分前后端独立可验收 Case（B/F/D/T）。
- 契约先行：先更新 `docs/contracts.md`，再改 Controller/Service/前端调用。
- 安全基线不退化：写接口持续满足 `Idempotency-Key + X-CSRF-TOKEN`。
- 数据库约束：禁止循环内数据库操作，优先批量查询/写入。
- 灰度策略：旧入口保留，新入口并行，最终再切默认落点。
- 文档驱动：同步更新 `docs/plan-*.md`、Case 状态与 `.http` 测试文件。

---

## 十二、关键技术决策

### D1 控制台应用卡片数据源

- 优先复用：`GET /api/v1/lowcode-apps`。
- 若需平台语义隔离，再补 `/api/v1/apps/cards` 适配层。

### D2 X-App-Id 生效顺序（本次已落地受控策略）

- 已登录请求按“受控 Header > JWT `app_id` > 默认 AppId”解析。
- 通过 `App.AllowHeaderOverrideWhenAuthenticated` 控制是否允许覆盖。
- 默认要求工作台标记头（`X-App-Workspace: 1`）后才允许已登录覆盖，避免全局放开。

### D3 DB 文件提交策略

- 默认必须提交：`src/backend/Atlas.WebApi/atlas.db`。
- `hangfire.db` 仅在验收明确要求时提交。

---

## 十三、分 Sprint 实施计划（规划）

### Sprint 1（P0）：平台/应用信息架构骨架

- 后端：菜单种子补齐 `/console*`、控制台卡片接口策略确认。
- 前端：控制台首页、应用工作台、路由与动态 fallback、登录默认落点。
- 文档/测试：更新 `docs/contracts.md` 与 `Bosch.http/LowCodeApps.http`。
- 验收：登录默认进入 `/console`，可进入 `/apps/:appId`，旧入口不退化。

### Sprint 2（P0）：应用级资源域最小集

- 后端：应用数据源不可变、共享策略、实体别名、应用设置 API、AppContext 覆盖受控化。
- 前端：三步创建向导、应用设置页、工作台请求上下文注入。
- 验收：创建应用时绑定数据源，创建后不可变；共享策略与别名可维护。

### Sprint 3（P0/P1）：低代码运行态闭环

- 后端：运行态控制器/服务、发布态读取策略统一、表单提交持久化。
- 前端：运行态渲染器、`/apps/:appId/run/:pageKey` 路由与入口。
- 验收：发布页面可访问、可提交并落库、权限不足返回 `FORBIDDEN`。

### Sprint 4（P0/P1）：仓储闭环产品化（权限域与应用域）

- 后端：`app:admin` / `app:user` 权限、动态表 `AppId` 隔离、审批写回增强。
- 前端：动态表应用筛选、审批任务中心应用过滤、写回监控增强。
- 验收：应用间数据不可互见，AppUser 在授权域可完成 CRUD 与审批。

### Sprint 5（P1）：流程能力统一（审批流 × 工作流）

- 后端：`ApprovalStep`、step-types `supported` 元数据、审批事件桥接工作流等待事件。
- 前端：序列化器放宽（顺序 + If 分支）、审批节点设计面板、规划中节点标记。
- 验收：审批步骤可配置并可运行，不支持节点不会“可画不可跑”。

### Sprint 6（P1）：预置模板与资产市场

- 后端：内置模板种子、模板查询增强、模板实例化流程接入。
- 前端：模板市场、从模板创建、保存为模板。
- 验收：可见内置模板、可复用创建、可沉淀用户模板。

### Sprint 7（P2）：移动端运行态

- 后端：审批/通知 API 移动兼容校验，通知链路支持深链。
- 前端：移动渲染与审批页面适配、通知直达审批处理。
- 验收：移动端可完整填表、审批与通知跳转闭环。

### Sprint 8（贯穿）：12 Case 联调验收并行推进

- 与 Sprint 1~7 并行推进 `docs/plan-功能补齐总览.md` 的 Case 勾选。
- 每个 Case 至少完成：契约同步、`.http` 可跑通、GUI 验证可复现。

---

## 十四、发布关口（强制）

### Gate-R1：完整 GUI 手工测试（强制）

- 链路覆盖：控制台 → 应用创建 → 运行态 → 动态表 → 审批 → 工作流 → 模板 → 移动适配。
- 产出物：手工测试清单、模块截图/录屏、问题清单与回归记录。

### Gate-R2：数据库文件提交（强制）

- 必须提交：`src/backend/Atlas.WebApi/atlas.db`。
- 生成规范：干净环境初始化 + 核心场景验收数据 + 敏感信息检查 + 停服后提交。

---

## 十五、统一测试策略

- 后端：`dotnet build`（0 错误 0 警告）。
- 测试：`dotnet test tests/Atlas.WorkflowCore.Tests`、`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"`。
- 前端：`npm run build`。
- 接口：受影响控制器对应 `.http` 文件同步更新并可执行。
- 回归重点：登录与路由守卫、租户/应用/项目上下文头、幂等与 CSRF、动态 fallback 与 404。

---

## 十六、里程碑、风险与 DoD

### 里程碑

- M1（S1）：控制台骨架 + 灰度可用
- M2（S2）：应用级数据源/共享策略闭环
- M3（S3+S4）：运行态数据闭环 + 动态表应用化
- M4（S5+S6）：审批/工作流统一 + 模板生态可用
- M5（S7+S8+Gate）：移动端适配 + 全链路验收 + DB 提交

### 风险与预案

- 路由重构：新旧并行 + 默认开关切换。
- App 上下文安全：受控 Header 覆盖 + 权限校验。
- DB 二进制冲突：Gate 阶段单分支统一生成并提交。
- 工作流契约偏差：先 `supported` 标记，再渐进开放设计器能力。

### 完成定义（DoD）

- Sprint 对应前后端 Case 全闭环并通过验收。
- `docs/contracts.md`、相关 `docs/plan-*.md`、`.http` 同步更新。
- 自动化构建通过、关键手工场景通过。
- 最终阶段完成完整 GUI 手工测试与 `atlas.db` 提交。

---

## 十七、等保映射

| 控制点 | 对应能力 |
|--------|----------|
| 8.1.4 访问控制 | 应用域权限隔离、AppContext 受控覆盖 |
| 8.1.5 审计要求 | 控制台操作、模板/发布/回滚、审批与写回留痕 |
| 8.1.6 数据安全 | 幂等与 CSRF、防重放、凭据脱敏、数据库样例数据治理 |

---

## 十八、变更记录

| 日期 | 变更内容 |
|------|----------|
| 2025-03-07 | 初稿：平台控制台、应用数据源、共享策略、实体别名、数据源不可变约束 |
| 2026-03-07 | 升级为 8 Sprint + 强制 Gate 路线图，加入 GUI 全链路测试与 `atlas.db` 提交要求，并补充 X-App-Id 受控覆盖策略 |
