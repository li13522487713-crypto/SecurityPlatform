namespace Atlas.Application.AiPlatform.Models;

public sealed record PromptGuardResult(
    bool IsSafe,
    string Reason);

public sealed record PiiDetectionResult(
    bool ContainsSensitive,
    IReadOnlyList<string> Findings,
    string SanitizedText);
