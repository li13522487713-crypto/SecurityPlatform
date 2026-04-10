namespace Atlas.Application.AiPlatform.Models;

public sealed record CanvasValidationIssue(
    string Code,
    string Message,
    string? NodeKey = null,
    string? SourcePort = null,
    string? TargetPort = null);

public sealed record CanvasValidationResult(
    bool IsValid,
    IReadOnlyList<CanvasValidationIssue> Errors);

