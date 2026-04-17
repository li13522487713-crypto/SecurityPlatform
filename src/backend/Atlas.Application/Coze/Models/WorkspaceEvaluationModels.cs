using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Coze.Models;

public enum EvaluationStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

public sealed record EvaluationItemDto(
    string Id,
    string Name,
    string TargetType,
    string TargetId,
    string TestsetId,
    EvaluationStatus Status,
    string MetricSummary,
    DateTimeOffset StartedAt);

public sealed record EvaluationDetailDto(
    string Id,
    string Name,
    string TargetType,
    string TargetId,
    string TestsetId,
    EvaluationStatus Status,
    string MetricSummary,
    DateTimeOffset StartedAt,
    int TotalCount,
    int PassCount,
    int FailCount,
    string ReportJson);

public sealed record TestsetItemDto(
    string Id,
    string Name,
    string? Description,
    string? WorkflowId,
    int RowCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TestsetCreateRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Name,
    [StringLength(200)] string? Description,
    string? WorkflowId,
    IReadOnlyList<Dictionary<string, object?>>? Rows);
