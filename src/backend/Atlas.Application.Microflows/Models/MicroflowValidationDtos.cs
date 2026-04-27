using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Contracts;

namespace Atlas.Application.Microflows.Models;

public sealed record ValidateMicroflowRequestDto
{
    [JsonPropertyName("schema")]
    public JsonElement? Schema { get; init; }

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("includeWarnings")]
    public bool? IncludeWarnings { get; init; }

    [JsonPropertyName("includeInfo")]
    public bool? IncludeInfo { get; init; }
}

public sealed record ValidateMicroflowResponseDto
{
    [JsonPropertyName("issues")]
    public IReadOnlyList<MicroflowValidationIssueDto> Issues { get; init; } = Array.Empty<MicroflowValidationIssueDto>();

    [JsonPropertyName("summary")]
    public MicroflowValidationSummaryDto Summary { get; init; } = new();

    [JsonPropertyName("serverValidatedAt")]
    public DateTimeOffset ServerValidatedAt { get; init; }
}

public sealed record MicroflowValidationSummaryDto
{
    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; init; }

    [JsonPropertyName("warningCount")]
    public int WarningCount { get; init; }

    [JsonPropertyName("infoCount")]
    public int InfoCount { get; init; }
}
