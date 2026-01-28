namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批人分配策略类型
/// </summary>
public enum AssigneeType
{
    /// <summary>指定用户</summary>
    User = 0,

    /// <summary>按角色</summary>
    Role = 1,

    /// <summary>部门负责人</summary>
    DepartmentLeader = 2,

    /// <summary>层层审批（Loop）- 向上逐级查找审批人</summary>
    Loop = 3,

    /// <summary>指定层级（Level）- 向上查找指定层级的审批人</summary>
    Level = 4,

    /// <summary>直属领导（DirectLeader）- 当前用户的直属上级</summary>
    DirectLeader = 5,

    /// <summary>发起人（StartUser）- 流程发起人</summary>
    StartUser = 6,

    /// <summary>HRBP - 人力资源业务伙伴</summary>
    Hrbp = 7,

    /// <summary>自选模块（Customize）- 由发起人选择审批人</summary>
    Customize = 8,

    /// <summary>关联业务表（BusinessTable）- 从业务数据中获取审批人</summary>
    BusinessTable = 9,

    /// <summary>外部传入人员（OutSideAccess）- 从外部系统传入的审批人</summary>
    OutSideAccess = 10
}
