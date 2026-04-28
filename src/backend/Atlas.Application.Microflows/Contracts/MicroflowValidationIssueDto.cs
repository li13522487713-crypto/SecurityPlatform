using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Contracts;

public sealed record MicroflowValidationIssueDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("microflowId")]
    public string MicroflowId { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "error";

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("flowId")]
    public string? FlowId { get; init; }

    [JsonPropertyName("actionId")]
    public string? ActionId { get; init; }

    [JsonPropertyName("parameterId")]
    public string? ParameterId { get; init; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("blockSave")]
    public bool BlockSave { get; init; }

    [JsonPropertyName("blockPublish")]
    public bool BlockPublish { get; init; }

    [JsonPropertyName("quickFixes")]
    public IReadOnlyList<MicroflowValidationQuickFixDto> QuickFixes { get; init; } = Array.Empty<MicroflowValidationQuickFixDto>();

    [JsonPropertyName("relatedObjectIds")]
    public IReadOnlyList<string> RelatedObjectIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("relatedFlowIds")]
    public IReadOnlyList<string> RelatedFlowIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("details")]
    public string? Details { get; init; }
}

public sealed record MicroflowValidationQuickFixDto
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("patch")]
    public string? Patch { get; init; }
}
