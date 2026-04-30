using System.Text.Json;

namespace Atlas.Application.Microflows.Runtime;

public sealed record NodeExecutionResultSummary
{
    public string ObjectId { get; init; } = string.Empty;

    public string? ActionId { get; init; }

    public string Status { get; init; } = "success";

    public int DurationMs { get; init; }

    public string? OutgoingFlowId { get; init; }

    public string? Message { get; init; }

    public JsonElement? Output { get; init; }
}
