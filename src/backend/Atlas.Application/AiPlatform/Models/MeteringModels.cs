namespace Atlas.Application.AiPlatform.Models;

public sealed record LlmUsageRecordCreateRequest(
    string Provider,
    string? Model,
    string? Source,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal EstimatedCostUsd);
