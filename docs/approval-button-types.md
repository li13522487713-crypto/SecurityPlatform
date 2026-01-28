# 审批流按钮类型分类说明

## 概述

审批流按钮分为两大类：
1. **操作按钮**：需要后端处理，会改变流程状态或数据
2. **UI按钮**：仅前端展示/交互，但需要权限校验和审计记录

## 按钮类型分类

### 一、操作按钮（需要后端处理）

这些按钮会触发后端操作，改变流程状态或数据，需要实现对应的 `IApprovalOperationHandler`。

| 按钮类型 | 操作类型 | 说明 | 是否需要落库 | 是否需要回调 |
|---------|---------|------|------------|------------|
| Submit | Submit | 流程提交 | ✅ | ✅ |
| Resubmit | Resubmit | 重新提交 | ✅ | ✅ |
| Agree | Agree | 同意 | ✅ | ✅ |
| Disagree | Disagree | 不同意 | ✅ | ✅ |
| BackToModify | BackToModify | 打回修改（打回给发起人） | ✅ | ✅ |
| BackToPrevModify | BackToPrevModify | 打回上节点修改（打回给上一个审批节点） | ✅ | ✅ |
| BackToAnyNode | BackToAnyNode | 退回任意节点 | ✅ | ✅ |
| Transfer | Transfer | 转办 | ✅ | ✅ |
| AddAssignee | AddAssignee | 加签 | ✅ | ✅ |
| RemoveAssignee | RemoveAssignee | 减签 | ✅ | ✅ |
| ProcessDrawBack | ProcessDrawBack | 流程撤回 | ✅ | ✅ |
| DrawBackAgree | DrawBackAgree | 撤销同意 | ✅ | ✅ |
| Undertake | Undertake | 承办 | ✅ | ✅ |
| Forward | Forward | 转发 | ✅ | ✅ |
| ChangeAssignee | ChangeAssignee | 变更处理人 | ✅ | ✅ |
| AddApproval | AddApproval | 加批 | ✅ | ✅ |
| ChooseAssignee | ChooseAssignee | 自选审批人 | ✅ | ✅ |
| ChangeFutureAssignee | ChangeFutureAssignee | 变更未来节点处理人 | ✅ | ✅ |
| RemoveFutureAssignee | RemoveFutureAssignee | 未来节点减签 | ✅ | ✅ |
| AddFutureAssignee | AddFutureAssignee | 未来节点加签 | ✅ | ✅ |
| SaveDraft | SaveDraft | 保存草稿 | ✅ | ❌ |
| ProcessMoveAhead | ProcessMoveAhead | 流程推进（管理员跳过） | ✅ | ✅ |
| RecoverToHistory | RecoverToHistory | 恢复已结束流程 | ✅ | ✅ |
| Abandon | Abandon | 作废 | ✅ | ✅ |
| Stop | Stop | 终止 | ✅ | ✅ |

### 二、UI按钮（仅前端操作，需要权限校验和审计记录）

这些按钮不改变流程状态，但需要：
- **权限校验**：检查用户是否有权限执行该操作
- **审计记录**：记录用户的操作行为（查看/打印）

| 按钮类型 | 操作类型 | 说明 | 是否需要权限校验 | 是否需要审计记录 | 是否需要落库 |
|---------|---------|------|---------------|---------------|------------|
| Preview | Preview | 预览流程/表单 | ✅ | ✅ | ✅（仅审计记录） |
| Print | Print | 打印流程/表单 | ✅ | ✅ | ✅（仅审计记录） |
| ViewBusinessProcess | ViewBusinessProcess | 查看流程详情 | ✅ | ✅ | ✅（仅审计记录） |

**注意**：
- UI按钮不会触发流程状态变更
- UI按钮不会触发外部回调
- UI按钮需要记录审计日志（`ApprovalOperationRecord`）
- UI按钮需要权限校验（基于流程定义和用户角色）

## 按钮配置

### 流程级别配置

每个流程定义可以配置可用的按钮，通过 `ApprovalFlowButtonConfig` 实体管理：
- `DefinitionId`：流程定义ID（0表示全局默认配置）
- `ViewType`：视图类型（发起人视图/审批人视图）
- `ButtonType`：按钮类型
- `ButtonName`：按钮显示名称
- `Remark`：备注

### 默认按钮配置

系统提供默认按钮配置（`DefinitionId = 0`），所有流程定义可以继承或覆盖：

**发起人视图默认按钮**：
- 提交（Submit）
- 重新提交（Resubmit）
- 撤回（ProcessDrawBack）
- 预览（Preview）
- 打印（Print）

**审批人视图默认按钮**：
- 同意（Agree）
- 不同意（Disagree）
- 打回修改（BackToModify）
- 打回上节点修改（BackToPrevModify）
- 退回任意节点（BackToAnyNode）
- 转办（Transfer）
- 加签（AddAssignee）
- 减签（RemoveAssignee）
- 撤销同意（DrawBackAgree）
- 预览（Preview）
- 打印（Print）

## 实现说明

### 操作按钮实现

操作按钮需要实现 `IApprovalOperationHandler` 接口：

```csharp
public interface IApprovalOperationHandler
{
    ApprovalOperationType SupportedOperationType { get; }
    
    Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken);
}
```

### UI按钮实现

UI按钮需要：
1. **权限校验**：在 Controller 层检查用户权限
2. **审计记录**：记录操作到 `ApprovalOperationRecord`
3. **返回数据**：返回流程/表单数据供前端展示/打印

示例实现：

```csharp
[HttpGet("instances/{instanceId}/preview")]
[Authorize]
public async Task<IActionResult> PreviewInstanceAsync(
    long instanceId,
    CancellationToken cancellationToken)
{
    // 1. 权限校验
    var instance = await _queryService.GetInstanceByIdAsync(...);
    if (!await _permissionService.CanViewAsync(instance, _currentUserId))
    {
        return Forbid();
    }

    // 2. 记录审计
    await _operationRecordService.RecordAsync(
        ApprovalOperationType.Preview,
        instanceId,
        null,
        _currentUserId,
        cancellationToken);

    // 3. 返回数据
    return Ok(instance);
}
```

## 按钮类型与操作类型映射

`ApprovalButtonType` 枚举对应 `ApprovalOperationType` 枚举，但 `ApprovalButtonType` 仅包含前端可操作的按钮类型。

**映射规则**：
- 每个 `ApprovalButtonType` 对应一个 `ApprovalOperationType`
- 不是所有的 `ApprovalOperationType` 都有对应的 `ApprovalButtonType`（例如：`ViewBusinessProcess` 是操作类型，但通常不作为按钮）

## 相关文件

- 按钮类型枚举：`src/backend/Atlas.Domain.Approval/Entities/ApprovalFlowButtonConfig.cs`
- 操作类型枚举：`src/backend/Atlas.Domain.Approval/Enums/ApprovalOperationType.cs`
- 按钮配置实体：`src/backend/Atlas.Domain.Approval/Entities/ApprovalFlowButtonConfig.cs`
- 操作处理器接口：`src/backend/Atlas.Application.Approval/Abstractions/IApprovalOperationHandler.cs`
