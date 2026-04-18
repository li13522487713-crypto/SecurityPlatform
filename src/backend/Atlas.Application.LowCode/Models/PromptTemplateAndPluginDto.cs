using System.Text.Json;

namespace Atlas.Application.LowCode.Models;

public sealed record PromptTemplateDto(
    string Id,
    string Code,
    string Name,
    string Body,
    string Mode,
    string Version,
    string? Description,
    string ShareScope,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PromptTemplateUpsertRequest(
    string? Id,
    string Code,
    string Name,
    string Body,
    string? Mode,
    string? Description,
    string? ShareScope);

public sealed record PluginDefinitionDto(
    string Id,
    string PluginId,
    string Name,
    string? Description,
    string ToolsJson,
    string LatestVersion,
    string ShareScope,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PluginUpsertRequest(
    string? Id,
    string Name,
    string? Description,
    string ToolsJson,
    string? ShareScope);

public sealed record PluginPublishVersionRequest(string Version);

public sealed record PluginAuthorizationDto(
    string Id,
    string PluginId,
    string AuthKind,
    DateTimeOffset GrantedAt);

public sealed record PluginAuthorizeRequest(string AuthKind, string? Credential);

public sealed record PluginUsageDto(string PluginId, string Day, long InvocationCount, long ErrorCount);

public sealed record PluginInvokeRequest(
    string PluginId,
    string ToolName,
    Dictionary<string, JsonElement>? Args);

public sealed record PluginInvokeResult(
    string PluginId,
    string ToolName,
    string Status,
    Dictionary<string, JsonElement>? Outputs,
    string? ErrorMessage);
