namespace Atlas.Application.AiPlatform.Models;

public sealed record CodeExecutionRequest(
    string Code,
    IReadOnlyDictionary<string, object?> Variables,
    int TimeoutSeconds);

public sealed record CodeExecutionResult(
    bool Success,
    object? Output,
    string? ErrorMessage,
    bool TimedOut,
    long DurationMs);
