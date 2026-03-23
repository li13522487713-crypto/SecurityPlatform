namespace Atlas.Application.AiPlatform.Models;

public sealed record FunctionToolSchemaItem(
    string Name,
    string Description,
    string Method,
    string Path,
    string ToolSchemaJson,
    string? RequestSchemaJson,
    string? ResponseSchemaJson);

public sealed record OpenApiPluginParseResult(
    string OpenApiSpecJson,
    string ToolSchemaJson,
    IReadOnlyList<FunctionToolSchemaItem> Tools);
