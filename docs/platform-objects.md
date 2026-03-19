# 平台统一对象词汇表（P0+P1）

## 1. 文档目的

本文用于沉淀平台底座对象模型的统一词汇，解决 `Application`、`Runtime` 等裸词多义问题，作为前后端命名对齐和后续演进基线。

## 2. 对象全集（20 项）

| 序号 | 对象 | 英文名 | 归属层 | 说明（边界定义） |
|---|---|---|---|---|
| 1 | 租户 | Tenant | 平台级 | 平台中的隔离主体，承载组织、权限、审计边界。 |
| 2 | 用户 | User | 租户级 | 租户内身份主体。 |
| 3 | 部门 | Department | 租户级 | 租户组织架构节点。 |
| 4 | 角色 | Role | 租户级 | 权限聚合载体。 |
| 5 | 权限 | Permission | 平台级 | 系统授权原子能力。 |
| 6 | 菜单 | Menu | 平台级 | 导航与功能入口配置。 |
| 7 | 项目 | Project | 租户级 | 业务项目维度（项目模式开启后生效）。 |
| 8 | 应用目录 | ApplicationCatalog | 平台级 | 平台提供的可开通应用定义（不承载租户运行态）。 |
| 9 | 租户开通关系 | TenantApplication | 租户级 | 租户与应用目录的订阅/开通关系。 |
| 10 | 租户应用实例 | TenantAppInstance | 应用级 | 租户开通后的实际运行载体。 |
| 11 | 租户数据源 | TenantDataSource | 租户级/应用级 | 租户可管理的数据连接资源（平台共享或应用专属）。 |
| 12 | 资源绑定 | ResourceBinding | 应用级 | 应用实例与资源（数据源等）的显式绑定关系。 |
| 13 | 发布快照 | AppRelease | 应用级 | 可回滚、可审计的发布版本快照。 |
| 14 | 运行路由 | RuntimeRoute | 应用级 | appKey + pageKey 到发布态页面的映射。 |
| 15 | 运行上下文 | RuntimeContext | 应用级 | 执行前上下文快照（环境、版本、路由等）。 |
| 16 | 运行执行 | RuntimeExecution | 应用级 | 一次具体运行实例（状态、输入输出、错误）。 |
| 17 | 审计轨迹 | AuditTrail | 平台级/租户级 | 安全审计与运行追踪证据。 |
| 18 | 制品包 | PackageArtifact | 平台级 | 导入/导出与发布分发制品。 |
| 19 | 授权授予 | LicenseGrant | 平台级 | 版本、席位、功能项授权实体。 |
| 20 | 工具授权策略 | ToolAuthorizationPolicy | 平台级 | 工具级访问、限流和审计策略。 |

## 3. 生命周期视角（按层）

### 3.1 平台级对象

- `Tenant`、`Permission`、`Menu`、`ApplicationCatalog`、`PackageArtifact`、`LicenseGrant`、`ToolAuthorizationPolicy`
- 生命周期特征：平台初始化 -> 配置维护 -> 发布治理 -> 审计归档

### 3.2 租户级对象

- `User`、`Department`、`Role`、`Project`、`TenantApplication`、`TenantDataSource`
- 生命周期特征：租户入驻 -> 组织与权限配置 -> 应用开通 -> 运行治理

### 3.3 应用级对象

- `TenantAppInstance`、`ResourceBinding`、`AppRelease`、`RuntimeRoute`、`RuntimeContext`、`RuntimeExecution`
- 生命周期特征：实例创建 -> 资源绑定 -> 发布 -> 路由生效 -> 运行执行 -> 审计追溯

## 4. v1 / v2 命名映射

> 与 `docs/contracts.md` 的命名兼容策略保持一致（v1 保留兼容窗口，v2 为目标命名）。

| v1 表达（兼容） | v2 表达（目标） | 说明 |
|---|---|---|
| `/api/v1/app-manifests/*` | `/api/v2/application-catalogs/*` | 平台目录定义统一为 `ApplicationCatalog` |
| `/api/v1/lowcode-apps/*` | `/api/v2/tenant-app-instances/*` | 运行实例统一为 `TenantAppInstance` |
| `Tenant-App`（文档旧称） | `TenantApplication` | 租户开通关系统一命名 |
| `/api/v1/runtime/*`（混用） | `/api/v2/runtime-contexts/*` + `/api/v2/runtime-executions/*` | 运行上下文与执行实例分离 |
| `Workflow`（裸词） | `WorkflowDefinition` / `RuntimeExecution` | 定义态与运行态强拆分 |
| `DataSource`（裸词） | `TenantDataSource` | 必须带层级语义 |

## 5. 对象关系链（主闭环）

平台目录定义（ApplicationCatalog）  
→ 租户开通（TenantApplication）  
→ 租户实例运行（TenantAppInstance）  
→ 发布快照（AppRelease）  
→ 路由生效（RuntimeRoute / RuntimeContext）  
→ 执行记录（RuntimeExecution）  
→ 审计追溯（AuditTrail）

## 6. 命名约束

1. 禁止裸用 `Application`，必须明确为 `ApplicationCatalog / TenantApplication / TenantAppInstance`。
2. 禁止裸用 `Runtime`，必须明确为 `RuntimeContext / RuntimeExecution`。
3. 前后端契约、页面文案、接口命名需统一使用本词汇表术语。
