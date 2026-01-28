# 审批模块用户/角色系统集成指南

## 概述

审批模块通过**接口契约**的方式抽象了用户/角色/部门查询能力，允许开发者替换默认实现以接入自有用户系统，避免审批规则被 demo 表绑死。

## 接口架构

审批模块采用**分层接口契约**设计，提供了三个细粒度的接口契约和一个组合接口：

### 核心接口契约（可替换）

1. **`IApprovalUserService`** - 用户查询服务接口契约
   - 验证用户ID有效性
   - 查询直属领导
   - 层层审批（Loop）
   - 指定层级审批（Level）
   - 查询HRBP

2. **`IApprovalRoleService`** - 角色查询服务接口契约
   - 按角色代码查询用户ID列表

3. **`IApprovalDepartmentService`** - 部门查询服务接口契约
   - 查询部门负责人用户ID

### 组合接口（统一入口）

**`IApprovalUserQueryService`** - 审批模块用户查询服务接口（组合式）
- 组合上述三个接口契约，提供完整的审批人查询能力
- 当前实现 `ApprovalUserQueryService` 通过依赖注入组合三个接口契约

## 接口能力

`IApprovalUserQueryService` 提供了以下6个核心方法，覆盖所有审批人策略的查询需求：

### 1. GetUserIdsByRoleCodeAsync
- **用途**：支持 `AssigneeType.Role` 审批人策略
- **输入**：租户ID、角色代码
- **输出**：该角色下的所有用户ID列表

### 2. GetDirectLeaderUserIdAsync
- **用途**：支持 `AssigneeType.DirectLeader` 审批人策略
- **输入**：租户ID、用户ID
- **输出**：该用户的直属领导用户ID（如果存在）

### 3. GetLoopApproversAsync
- **用途**：支持 `AssigneeType.Loop` 层层审批策略
- **输入**：租户ID、起始用户ID、最大查找层级
- **输出**：向上逐级查找的审批人用户ID列表（按层级从低到高）

### 4. GetLevelApproverAsync
- **用途**：支持 `AssigneeType.Level` 指定层级审批策略
- **输入**：租户ID、起始用户ID、目标层级
- **输出**：指定层级的审批人用户ID（如果层级不足则返回null）

### 5. GetHrbpUserIdAsync
- **用途**：支持 `AssigneeType.Hrbp` HRBP审批策略
- **输入**：租户ID、用户ID
- **输出**：该用户的HRBP用户ID（如果存在）

### 6. ValidateUserIdsAsync
- **用途**：验证用户ID列表的有效性（用于所有审批人策略的最终校验）
- **输入**：租户ID、用户ID列表
- **输出**：有效的用户ID列表（过滤掉不存在的用户）

## 默认实现

### 接口契约实现

当前默认实现基于仓储模式：

- **`ApprovalUserService`** (实现 `IApprovalUserService`)
  - 基于 `IUserAccountRepository`、`IUserDepartmentRepository`、`IApprovalDepartmentLeaderRepository`、`IRoleRepository`

- **`ApprovalRoleService`** (实现 `IApprovalRoleService`)
  - 基于 `IRoleRepository`、`IUserRoleRepository`

- **`ApprovalDepartmentService`** (实现 `IApprovalDepartmentService`)
  - 基于 `IApprovalDepartmentLeaderRepository`

### 组合实现

- **`ApprovalUserQueryService`** (实现 `IApprovalUserQueryService`)
  - 通过依赖注入组合 `IApprovalUserService`、`IApprovalRoleService`、`IApprovalDepartmentService`
  - 实现了接口契约与具体实现的解耦

## 替换实现方式

### 方式1：替换单个接口契约（推荐）

如果只需要替换部分能力（如只替换用户查询），可以实现对应的接口契约：

```csharp
using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Tenancy;

namespace YourCompany.YourModule.Services;

/// <summary>
/// 自定义用户查询服务实现（接入自有用户系统）
/// </summary>
public sealed class CustomUserService : IApprovalUserService
{
    private readonly IYourUserService _yourUserService;

    public CustomUserService(IYourUserService yourUserService)
    {
        _yourUserService = yourUserService;
    }

    public async Task<IReadOnlyList<long>> ValidateUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        // 调用自有系统验证用户
        return await _yourUserService.ValidateUserIdsAsync(tenantId, userIds, cancellationToken);
    }

    public async Task<long?> GetDirectLeaderUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        // 调用自有系统查询直属领导
        return await _yourUserService.GetDirectLeaderAsync(tenantId, userId, cancellationToken);
    }

    // ... 实现其他方法
}
```

然后在 DI 容器中注册：

```csharp
// 替换用户服务接口契约
services.AddScoped<IApprovalUserService, CustomUserService>();

// 角色和部门服务保持默认实现
// ApprovalUserQueryService 会自动使用新的用户服务实现
```

### 方式2：替换所有接口契约

如果需要完全替换所有查询能力：

```csharp
// 替换所有接口契约
services.AddScoped<IApprovalUserService, CustomUserService>();
services.AddScoped<IApprovalRoleService, CustomRoleService>();
services.AddScoped<IApprovalDepartmentService, CustomDepartmentService>();

// ApprovalUserQueryService 会自动使用新的实现
```

### 方式3：替换组合接口（完全自定义）

如果需要完全自定义实现逻辑：

```csharp
using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Tenancy;

namespace YourCompany.YourModule.Services;

/// <summary>
/// 完全自定义的用户查询服务实现
/// </summary>
public sealed class CustomUserQueryService : IApprovalUserQueryService
{
    private readonly IYourUserService _yourUserService;
    private readonly IYourRoleService _yourRoleService;
    private readonly IYourDepartmentService _yourDepartmentService;

    public CustomUserQueryService(
        IYourUserService yourUserService,
        IYourRoleService yourRoleService,
        IYourDepartmentService yourDepartmentService)
    {
        _yourUserService = yourUserService;
        _yourRoleService = yourRoleService;
        _yourDepartmentService = yourDepartmentService;
    }

    public async Task<IReadOnlyList<long>> GetUserIdsByRoleCodeAsync(
        TenantId tenantId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        // 调用自有系统的角色服务查询用户
        var users = await _yourRoleService.GetUsersByRoleCodeAsync(tenantId, roleCode, cancellationToken);
        return users.Select(u => u.Id).ToList();
    }

    // ... 实现其他方法
}
```

注册：

```csharp
// 替换组合接口
services.AddScoped<IApprovalUserQueryService, CustomUserQueryService>();
```

## 实现注意事项

### 1. 多租户隔离
所有方法必须通过 `TenantId` 参数实现多租户数据隔离。

### 2. 性能优化
- 避免 N+1 查询问题
- 对于批量查询，尽量使用批量接口
- 考虑缓存常用查询结果（如角色用户列表）

### 3. 错误处理
- 如果查询不到结果，返回空列表或 null（不要抛出异常）
- 只有系统级错误（如数据库连接失败）才抛出异常

### 4. 数据一致性
- 确保返回的用户ID在当前租户下确实存在
- `ValidateUserIdsAsync` 方法应严格验证用户有效性

### 5. 组织架构查询
- `GetLoopApproversAsync` 和 `GetLevelApproverAsync` 需要访问组织架构数据
- 如果自有系统没有组织架构，可以返回空列表或仅返回直属领导

## 审批人策略映射

| 审批人策略 | 使用的接口方法 | 说明 |
|-----------|--------------|------|
| User | ValidateUserIdsAsync | 直接指定用户，仅需验证有效性 |
| Role | GetUserIdsByRoleCodeAsync | 按角色查询用户 |
| DepartmentLeader | GetDirectLeaderUserIdAsync + 部门负责人逻辑 | 查询部门负责人 |
| Loop | GetLoopApproversAsync | 层层审批 |
| Level | GetLevelApproverAsync | 指定层级 |
| DirectLeader | GetDirectLeaderUserIdAsync | 直属领导 |
| StartUser | ValidateUserIdsAsync | 发起人，仅需验证有效性 |
| HRBP | GetHrbpUserIdAsync | HRBP查询 |
| Customize | ValidateUserIdsAsync | 自选模块，仅需验证有效性 |
| BusinessTable | ValidateUserIdsAsync | 从业务数据获取，仅需验证有效性 |
| OutSideAccess | ValidateUserIdsAsync | 外部传入，仅需验证有效性 |

## 测试建议

替换实现后，建议测试以下场景：
1. 各种审批人策略的查询准确性
2. 多租户数据隔离正确性
3. 边界情况处理（如用户不存在、角色不存在、组织架构不完整等）
4. 性能测试（大量并发查询）

## 接口契约设计优势

1. **解耦**：审批模块不直接依赖具体的仓储实现，而是依赖抽象的接口契约
2. **可替换**：可以按需替换单个或多个接口契约，无需替换整个实现
3. **可测试**：接口契约便于单元测试和模拟
4. **可扩展**：未来可以轻松添加新的查询能力（如新的接口契约）

## 相关文件

### 接口契约定义
- `IApprovalUserService`：`src/backend/Atlas.Application.Approval/Abstractions/IApprovalUserService.cs`
- `IApprovalRoleService`：`src/backend/Atlas.Application.Approval/Abstractions/IApprovalRoleService.cs`
- `IApprovalDepartmentService`：`src/backend/Atlas.Application.Approval/Abstractions/IApprovalDepartmentService.cs`
- `IApprovalUserQueryService`：`src/backend/Atlas.Application.Approval/Abstractions/IApprovalUserQueryService.cs`

### 默认实现
- `ApprovalUserService`：`src/backend/Atlas.Infrastructure/Services/ApprovalFlow/ApprovalUserService.cs`
- `ApprovalRoleService`：`src/backend/Atlas.Infrastructure/Services/ApprovalFlow/ApprovalRoleService.cs`
- `ApprovalDepartmentService`：`src/backend/Atlas.Infrastructure/Services/ApprovalFlow/ApprovalDepartmentService.cs`
- `ApprovalUserQueryService`：`src/backend/Atlas.Infrastructure/Services/ApprovalFlow/ApprovalUserQueryService.cs`

### 使用位置
- `FlowEngine.cs`：`src/backend/Atlas.Infrastructure/Services/ApprovalFlow/FlowEngine.cs`
