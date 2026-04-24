using System.Text.Json;
using Atlas.Core.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;

namespace Atlas.Application.AiPlatform.Models;

public sealed record CozeWorkflowCreateCommand(
    string Name,
    string? Description,
    WorkflowMode Mode,
    long? WorkspaceId = null);

public sealed record CozeWorkflowSaveDraftCommand(
    string SchemaJson,
    string? CommitId);

public sealed record CozeWorkflowSaveDraftResult(
    string CommitId,
    int WorkflowVersion);

public sealed record CozeWorkflowUpdateMetaCommand(
    string Name,
    string? Description);

public sealed record CozeWorkflowPublishCommand(
    string? ChangeLog);

public sealed record CozeWorkflowRunCommand(
    string? InputsJson,
    string? Source = null);

public sealed record CozeWorkflowNodeDebugCommand(
    string NodeKey,
    string? InputsJson,
    string? Source = null,
    long? VersionId = null);

public sealed record CozeWorkflowListItem(
    long Id,
    string Name,
    string? Description,
    WorkflowMode Mode,
    WorkflowLifecycleStatus Status,
    int LatestVersionNumber,
    long CreatorId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt);

public sealed record CozeWorkflowDetailDto(
    long Id,
    string Name,
    string? Description,
    WorkflowMode Mode,
    WorkflowLifecycleStatus Status,
    int LatestVersionNumber,
    long CreatorId,
    string SchemaJson,
    string? CommitId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt);

public sealed record CozeWorkflowVersionDto(
    long Id,
    long WorkflowId,
    int VersionNumber,
    string? ChangeLog,
    string SchemaJson,
    DateTime PublishedAt,
    long PublishedByUserId);

public sealed record CozeWorkflowRunResult(
    string ExecutionId,
    ExecutionStatus? Status = null,
    string? OutputsJson = null,
    string? ErrorMessage = null,
    string? DebugNodeKey = null);

public sealed record CozeWorkflowExecutionDto(
    long Id,
    long WorkflowId,
    int VersionNumber,
    ExecutionStatus Status,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<DagWorkflowNodeExecutionDto> NodeExecutions);

public sealed record CozeWorkflowHistorySchemaDto(
    string WorkflowId,
    string? CommitId,
    string SchemaJson,
    string Name,
    string? Description,
    DateTime SnapshotAt);

public sealed record CozeWorkflowCompileResult(
    bool IsSuccess,
    CanvasSchema? Canvas,
    IReadOnlyList<CanvasValidationIssue> Errors);

public sealed record CozeWorkflowReferenceDto(
    long WorkflowId,
    IReadOnlyList<DagWorkflowDependencyItemDto> SubWorkflows,
    IReadOnlyList<DagWorkflowDependencyItemDto> Plugins,
    IReadOnlyList<DagWorkflowDependencyItemDto> KnowledgeBases,
    IReadOnlyList<DagWorkflowDependencyItemDto> Databases,
    IReadOnlyList<DagWorkflowDependencyItemDto> Variables,
    IReadOnlyList<DagWorkflowDependencyItemDto> Conversations)
{
    public static readonly CozeWorkflowReferenceDto Empty = new(
        0,
        Array.Empty<DagWorkflowDependencyItemDto>(),
        Array.Empty<DagWorkflowDependencyItemDto>(),
        Array.Empty<DagWorkflowDependencyItemDto>(),
        Array.Empty<DagWorkflowDependencyItemDto>(),
        Array.Empty<DagWorkflowDependencyItemDto>(),
        Array.Empty<DagWorkflowDependencyItemDto>());
}
