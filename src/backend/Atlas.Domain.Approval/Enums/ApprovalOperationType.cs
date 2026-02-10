namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 审批流运行时操作类型（对应 AntFlow.net 的 ProcessOperationEnum）
/// </summary>
public enum ApprovalOperationType
{
    /// <summary>流程提交</summary>
    Submit = 1,

    /// <summary>重新提交</summary>
    Resubmit = 2,

    /// <summary>同意</summary>
    Agree = 3,

    /// <summary>不同意</summary>
    Disagree = 4,

    /// <summary>查看流程详情</summary>
    ViewBusinessProcess = 5,

    /// <summary>作废</summary>
    Abandon = 7,

    /// <summary>承办</summary>
    Undertake = 10,

    /// <summary>变更处理人</summary>
    ChangeAssignee = 11,

    /// <summary>终止</summary>
    Stop = 12,

    /// <summary>转发</summary>
    Forward = 15,

    /// <summary>打回修改（打回给发起人）</summary>
    BackToModify = 18,

    /// <summary>打回上节点修改（打回给上一个审批节点）</summary>
    BackToPrevModify = 17,

    /// <summary>加批</summary>
    AddApproval = 19,

    /// <summary>转办</summary>
    Transfer = 21,

    /// <summary>自选审批人</summary>
    ChooseAssignee = 22,

    /// <summary>退回任意节点</summary>
    BackToAnyNode = 23,

    /// <summary>减签</summary>
    RemoveAssignee = 24,

    /// <summary>加签</summary>
    AddAssignee = 25,

    /// <summary>变更未来节点处理人</summary>
    ChangeFutureAssignee = 26,

    /// <summary>未来节点减签</summary>
    RemoveFutureAssignee = 27,

    /// <summary>未来节点加签</summary>
    AddFutureAssignee = 28,

    /// <summary>流程撤回</summary>
    ProcessDrawBack = 29,

    /// <summary>保存草稿</summary>
    SaveDraft = 30,

    /// <summary>恢复已结束流程</summary>
    RecoverToHistory = 31,

    /// <summary>撤销同意</summary>
    DrawBackAgree = 32,

    /// <summary>流程推进</summary>
    ProcessMoveAhead = 33,

    /// <summary>预览流程/表单（仅UI操作，需要权限校验和审计记录）</summary>
    Preview = 34,

    /// <summary>打印流程/表单（仅UI操作，需要权限校验和审计记录）</summary>
    Print = 35,

    /// <summary>跳转</summary>
    Jump = 36,

    /// <summary>拿回</summary>
    Reclaim = 37,

    /// <summary>唤醒</summary>
    Resume = 38,

    /// <summary>认领</summary>
    Claim = 39,

    /// <summary>释放认领</summary>
    Release = 40,

    /// <summary>催办</summary>
    Urge = 41,

    /// <summary>沟通</summary>
    Communicate = 42,

    /// <summary>离职转办</summary>
    BatchTransfer = 43,

    /// <summary>委派</summary>
    Delegate = 44,

    /// <summary>追加处理人</summary>
    Append = 45,

    /// <summary>强制终止</summary>
    Terminate = 46
}
