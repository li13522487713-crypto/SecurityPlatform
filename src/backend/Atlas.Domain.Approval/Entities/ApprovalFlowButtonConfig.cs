using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流按钮配置（流程级别的按钮能力配置）
/// </summary>
public sealed class ApprovalFlowButtonConfig : TenantEntity
{
    public ApprovalFlowButtonConfig()
        : base(TenantId.Empty)
    {
        ButtonType = ApprovalButtonType.Submit;
        ViewType = ApprovalViewType.Initiator;
        ButtonName = string.Empty;
        Remark = string.Empty;
    }

    public ApprovalFlowButtonConfig(
        TenantId tenantId,
        long definitionId,
        ApprovalViewType viewType,
        ApprovalButtonType buttonType,
        string buttonName,
        long id,
        string? remark = null)
        : base(tenantId)
    {
        Id = id;
        DefinitionId = definitionId;
        ViewType = viewType;
        ButtonType = buttonType;
        ButtonName = buttonName;
        Remark = remark ?? string.Empty;
    }

    /// <summary>流程定义 ID</summary>
    public long DefinitionId { get; private set; }

    /// <summary>视图类型（发起人视图/审批人视图）</summary>
    public ApprovalViewType ViewType { get; private set; }

    /// <summary>按钮类型（对应 ApprovalOperationType）</summary>
    public ApprovalButtonType ButtonType { get; private set; }

    /// <summary>按钮显示名称</summary>
    public string ButtonName { get; private set; }

    /// <summary>备注</summary>
    public string Remark { get; private set; }

    public void Update(string buttonName, string? remark = null)
    {
        ButtonName = buttonName;
        if (remark != null)
        {
            Remark = remark;
        }
    }
}

/// <summary>
/// 审批视图类型
/// </summary>
public enum ApprovalViewType
{
    /// <summary>发起人视图</summary>
    Initiator = 1,

    /// <summary>审批人视图</summary>
    Approver = 2
}

/// <summary>
/// 审批按钮类型（对应 ApprovalOperationType，但仅包含前端可操作的按钮）
/// </summary>
public enum ApprovalButtonType
{
    /// <summary>提交</summary>
    Submit = 1,

    /// <summary>重新提交</summary>
    Resubmit = 2,

    /// <summary>同意</summary>
    Agree = 3,

    /// <summary>不同意</summary>
    Disagree = 4,

    /// <summary>打回修改</summary>
    BackToModify = 18,

    /// <summary>转办</summary>
    Transfer = 21,

    /// <summary>退回任意节点</summary>
    BackToAnyNode = 23,

    /// <summary>减签</summary>
    RemoveAssignee = 24,

    /// <summary>加签</summary>
    AddAssignee = 25,

    /// <summary>流程撤回</summary>
    ProcessDrawBack = 29,

    /// <summary>撤销同意</summary>
    DrawBackAgree = 32
}
