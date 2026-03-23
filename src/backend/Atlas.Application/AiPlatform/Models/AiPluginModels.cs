using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiPluginListItem(
    long Id,
    string Name,
    string? Description,
    string? Icon,
    string? Category,
    AiPluginType Type,
    AiPluginSourceType SourceType,
    AiPluginAuthType AuthType,
    AiPluginStatus Status,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt);

public sealed record AiPluginDetail(
    long Id,
    string Name,
    string? Description,
    string? Icon,
    string? Category,
    AiPluginType Type,
    AiPluginSourceType SourceType,
    AiPluginAuthType AuthType,
    AiPluginStatus Status,
    string DefinitionJson,
    string AuthConfigJson,
    string ToolSchemaJson,
    string OpenApiSpecJson,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<AiPluginApiItem> Apis);

public sealed record AiPluginCreateRequest(
    string Name,
    string? Description,
    string? Icon,
    string? Category,
    AiPluginType Type,
    string? DefinitionJson,
    AiPluginSourceType SourceType,
    AiPluginAuthType AuthType,
    string? AuthConfigJson,
    string? ToolSchemaJson,
    string? OpenApiSpecJson);

public sealed record AiPluginUpdateRequest(
    string Name,
    string? Description,
    string? Icon,
    string? Category,
    AiPluginType Type,
    string? DefinitionJson,
    AiPluginSourceType SourceType,
    AiPluginAuthType AuthType,
    string? AuthConfigJson,
    string? ToolSchemaJson,
    string? OpenApiSpecJson);

public sealed record AiPluginLockRequest(bool IsLocked);

public sealed record AiPluginDebugRequest(
    long? ApiId,
    string? InputJson);

public sealed record AiPluginDebugResult(
    bool Success,
    string OutputJson,
    string? ErrorMessage,
    long DurationMs);

public sealed record AiPluginOpenApiImportRequest(
    string OpenApiJson,
    bool Overwrite);

public sealed record AiPluginOpenApiImportResult(
    int ImportedCount,
    IReadOnlyList<string> ImportedApiNames);

public sealed record AiPluginApiItem(
    long Id,
    long PluginId,
    string Name,
    string? Description,
    string Method,
    string Path,
    string RequestSchemaJson,
    string ResponseSchemaJson,
    int TimeoutSeconds,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiPluginApiCreateRequest(
    string Name,
    string? Description,
    string Method,
    string Path,
    string? RequestSchemaJson,
    string? ResponseSchemaJson,
    int TimeoutSeconds);

public sealed record AiPluginApiUpdateRequest(
    string Name,
    string? Description,
    string Method,
    string Path,
    string? RequestSchemaJson,
    string? ResponseSchemaJson,
    int TimeoutSeconds,
    bool IsEnabled);

public sealed record AiPluginBuiltInMetaItem(
    string Code,
    string Name,
    string Description,
    string Category,
    string Version,
    IReadOnlyList<string> Tags);
