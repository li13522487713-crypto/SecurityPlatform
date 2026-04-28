using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Contracts;

public sealed record MicroflowRequestContext
{
    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; init; }

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    [JsonPropertyName("userName")]
    public string? UserName { get; init; }

    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    [JsonPropertyName("locale")]
    public string? Locale { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");
}
