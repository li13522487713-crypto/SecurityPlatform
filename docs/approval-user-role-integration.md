# 审批模块用户/角色系统集成指南

## 概述

`IApprovalUserQueryService` 接口抽象了审批流程所需的最小用户/角色/部门查询能力，允许开发者替换默认实现以接入自有用户系统。

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

当前默认实现 `ApprovalUserQueryService` 基于以下仓储：
- `IRoleRepository` - 角色查询
- `IUserRoleRepository` - 用户角色关联查询
- `IUserDepartmentRepository` - 用户部门关联查询
- `IDepartmentRepository` - 部门查询
- `IApprovalDepartmentLeaderRepository` - 部门负责人查询
- `IUserAccountRepository` - 用户账户查询

## 替换实现步骤

### 步骤1：创建自定义实现类

```csharp
using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Tenancy;

namespace YourCompany.YourModule.Services;

/// <summary>
/// 自定义用户查询服务实现（接入自有用户系统）
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

    public async Task<long?> GetDirectLeaderUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        // 调用自有系统的用户服务查询直属领导
        var leader = await _yourUserService.GetDirectLeaderAsync(tenantId, userId, cancellationToken);
        return leader?.Id;
    }

    // ... 实现其他方法
}
```

### 步骤2：注册自定义实现

在 `ServiceCollectionExtensions.cs` 或 `Program.cs` 中：

```csharp
// 替换默认实现
services.AddScoped<IApprovalUserQueryService, CustomUserQueryService>();

// 或者使用条件注册
if (configuration.GetValue<bool>("UseCustomUserService"))
{
    services.AddScoped<IApprovalUserQueryService, CustomUserQueryService>();
}
else
{
    services.AddScoped<IApprovalUserQueryService, ApprovalUserQueryService>();
}
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

## 相关文件

- 接口定义：`src/backend/Atlas.Application.Approval/Abstractions/IApprovalUserQueryService.cs`
- 默认实现：`src/backend/Atlas.Infrastructure/Services/ApprovalFlow/ApprovalUserQueryService.cs`
- 使用位置：`src/backend/Atlas.Infrastructure/Services/ApprovalFlow/FlowEngine.cs`
