# Plan: 多数据源（Multi-DataSource）

## 1. 功能说明

多数据源允许系统根据租户 ID 动态切换数据库连接，实现数据库级别的租户隔离。相比单库多 Schema 方案，多数据源在安全性和故障隔离方面更符合等保2.0 要求。

### 1.1 应用场景

| 场景 | 说明 |
|---|---|
| 租户独立数据库 | 每个租户拥有独立的 SQLite/SQL Server 实例 |
| 读写分离（预留） | 主从数据库配置 |
| 灾备切换（预留） | 主备自动切换 |

### 1.2 本期实现范围

- 在 `TenantDataSource` 配置表中存储租户数据库连接字符串
- 在 `ITenantDbConnectionFactory` 中根据租户 ID 动态获取连接字符串
- 在 `ISqlSugarClient` 构建时注入对应连接，实现透明切换
- 系统管理员可在界面配置/测试租户数据源

## 2. 等保 2.0 要求

| 要求 | 对应控制 |
|---|---|
| 数据隔离 | 不同租户数据物理隔离（独立数据库文件） |
| 连接字符串加密 | 数据库连接信息使用 AES-256 加密存储 |
| 操作审计 | 添加/修改数据源连接需记录审计日志 |

## 3. 数据模型

### 3.1 TenantDataSource 实体

```csharp
// Atlas.Domain.System.Entities.TenantDataSource（新增）
public class TenantDataSource : BaseEntity
{
    public string TenantIdValue { get; set; }   // 外键关联到 Tenant
    public string Name { get; set; }             // 数据源名称
    public string EncryptedConnectionString { get; set; } // 加密存储
    public string DbType { get; set; }           // "SQLite" / "SqlServer"
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

## 4. 接口设计

```
GET    /api/v1/tenant-datasources         - 列表（超管）
POST   /api/v1/tenant-datasources         - 创建
PUT    /api/v1/tenant-datasources/{id}    - 更新
DELETE /api/v1/tenant-datasources/{id}    - 删除
POST   /api/v1/tenant-datasources/{id}/test - 测试连接
```

## 5. 后端实现步骤

1. 新增 `TenantDataSource` 实体
2. 新增 `ITenantDbConnectionFactory` 接口，方法：`GetConnectionStringAsync(tenantId, ct)`
3. 新增 `TenantDbConnectionFactory` 实现：查询 `TenantDataSource`，解密连接字符串，缓存结果
4. 修改 `ISqlSugarClient` 注册，使用 `TenantDbConnectionFactory` 动态切换连接
5. 新增 `TenantDataSourcesController`，含测试连接端点
6. 连接字符串加密/解密复用现有 `DatabaseEncryptionOptions`
7. 更新 `DatabaseInitializerHostedService` 初始化 `TenantDataSource` 表

## 6. 前端实现步骤

1. 新增 `TenantDataSourcesPage.vue`（系统管理 > 数据源管理）
2. 包含数据源列表、新增/编辑抽屉、连接测试按钮
3. 在路由中注册

## 7. 验收标准

- [ ] 超管可添加/编辑/删除租户数据源
- [ ] 连接字符串加密存储
- [ ] 可测试连接是否有效
- [ ] 租户切换数据源后，该租户的查询使用新数据源
- [ ] 所有操作写入审计日志
