using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Models;

public sealed class BatchJobExecutionResponse
{
    public string Id { get; set; } = string.Empty;
    public string JobDefinitionId { get; set; } = string.Empty;
    public JobExecutionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalShards { get; set; }
    public int CompletedShards { get; set; }
    public int FailedShards { get; set; }
    public long TotalRecords { get; set; }
    public long ProcessedRecords { get; set; }
    public long FailedRecords { get; set; }
    public string? ErrorMessage { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
}
