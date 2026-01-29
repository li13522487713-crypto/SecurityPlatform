namespace Atlas.WorkflowDemo.Models;

/// <summary>
/// 审批工作流数据模型
/// </summary>
public class ApprovalWorkflowData
{
    /// <summary>
    /// 申请标题
    /// </summary>
    public string ApplicationTitle { get; set; } = string.Empty;

    /// <summary>
    /// 申请人
    /// </summary>
    public string Applicant { get; set; } = string.Empty;

    /// <summary>
    /// 是否批准
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// 审批人
    /// </summary>
    public string Approver { get; set; } = string.Empty;
}
