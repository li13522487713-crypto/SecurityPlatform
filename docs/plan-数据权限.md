# Plan: 数据权限（Data Scope）

## 1. 功能说明

数据权限控制哪些数据行对哪个用户可见，是角色权限（"能否访问此功能"）之上的第二层授权（"能看哪些数据"）。

### 1.1 核心概念

| 数据权限类型 | 说明 |
|---|---|
| `All` | 全部数据（超管） |
| `CurrentTenant` | 当前租户所有数据（默认，多租户隔离已实现） |
| `CustomDept` | 自定义部门数据（本期预留） |
| `CurrentDept` | 本部门数据（本期预留） |
| `CurrentDeptAndBelow` | 本部门及下级（本期预留） |
| `OnlySelf` | 仅自己创建的数据 |

### 1.2 本期实现范围

- 在角色上配置 `DataScopeType`
- 在查询用户列表时按数据权限过滤（以 Users 模块为示例）
- 在角色管理前端新增"数据权限"Tab

## 2. 等保 2.0 要求

| 要求 | 对应控制 |
|---|---|
| 最小化授权 | 默认 `OnlySelf`，仅超管或明确授权才能查看他人数据 |
| 访问控制记录 | 通过现有 AuditLog 记录用户操作 |
| 权限变更审计 | 修改角色数据权限时记录审计日志 |

## 3. 数据模型变更

### 3.1 Role 实体新增字段

```csharp
// Atlas.Domain.Identity.Entities.Role 新增：
public DataScopeType DataScope { get; private set; } = DataScopeType.CurrentTenant;
```

### 3.2 DataScopeType 枚举

```csharp
// Atlas.Core.Enums.DataScopeType
public enum DataScopeType
{
    All = 0,
    CurrentTenant = 1,
    OnlySelf = 2,
    CustomDept = 10,
    CurrentDept = 11,
    CurrentDeptAndBelow = 12
}
```

## 4. 接口设计

```
PUT /api/v1/roles/{id}/data-scope
Request: { "dataScope": 1 }
Response: ApiResponse<object>
```

## 5. 后端实现步骤

1. 在 `Atlas.Core` 添加 `DataScopeType` 枚举
2. 在 `Role` 实体新增 `DataScope` 属性
3. 在 `IDataScopeFilter` 接口定义 `Apply<T>()` 方法
4. 在 `DataScopeFilter` 实现中读取当前用户角色的 DataScope，构建过滤条件
5. 在 `UserRepository` / `UsersController` 注入并应用过滤器
6. 新增 `PUT /api/v1/roles/{id}/data-scope` 端点
7. 更新数据库（CodeFirst）

## 6. 前端实现步骤

1. 在 `RolesPage.vue` 角色抽屉/详情中增加"数据权限"Tab
2. 展示 `DataScopeType` 单选按钮（全部/当前租户/仅本人）
3. 调用 `PUT /api/v1/roles/{id}/data-scope` 保存

## 7. 验收标准

- [ ] 角色可配置数据权限类型
- [ ] `OnlySelf` 用户只能看到自己创建的用户记录
- [ ] `CurrentTenant` 可见租户内所有数据
- [ ] 修改数据权限写入审计日志
- [ ] 前端角色管理页有"数据权限"Tab
