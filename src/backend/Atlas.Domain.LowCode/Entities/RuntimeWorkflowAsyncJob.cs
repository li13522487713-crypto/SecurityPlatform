using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 运行时工作流异步任务（M09 S09-2）。
/// 状态：pending / running / success / failed / cancelled。
/// </summary>
public sealed class RuntimeWorkflowAsyncJob : TenantEntity
{
#pragma warning disable CS8618
    public RuntimeWorkflowAsyncJob()
        : base(TenantId.Empty)
    {
        JobId = string.Empty;
        WorkflowId = string.Empty;
        Status = "pending";
    }
#pragma warning restore CS8618

    public RuntimeWorkflowAsyncJob(TenantId tenantId, long id, string jobId, string workflowId, string requestJson, long submittedByUserId)
        : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        WorkflowId = workflowId;
        RequestJson = requestJson;
        Status = "pending";
        SubmittedByUserId = submittedByUserId;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string JobId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string WorkflowId { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? RequestJson { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ResultJson { get; private set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; private set; }

    public int? ProgressPercent { get; private set; }

    public long SubmittedByUserId { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>可选 webhook 回调 URL（M19 S19-3）；终态时由后台作业调用。</summary>
    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? WebhookUrl { get; private set; }

    public void SetWebhook(string? webhookUrl)
    {
        WebhookUrl = string.IsNullOrWhiteSpace(webhookUrl) ? null : webhookUrl;
    }

    public void MarkRunning() { Status = "running"; }

    public void MarkSucceeded(string resultJson)
    {
        Status = "success";
        ResultJson = resultJson;
        CompletedAt = DateTimeOffset.UtcNow;
        ProgressPercent = 100;
    }

    public void MarkFailed(string error)
    {
        Status = "failed";
        ErrorMessage = error;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCancelled()
    {
        Status = "cancelled";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProgress(int percent) { ProgressPercent = Math.Clamp(percent, 0, 100); }
}
