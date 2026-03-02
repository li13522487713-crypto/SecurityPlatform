namespace Atlas.Application.LowCode.Models;

public sealed record ReportDefinitionListItem(string Id, string Name, string? Description, string? Category, int Version, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ReportDefinitionDetail(string Id, string Name, string? Description, string? Category, string ConfigJson, string? DataSourceJson, string? PrintTemplateJson, int Version, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long CreatedBy, long UpdatedBy);
public sealed record ReportDefinitionCreateRequest(string Name, string? Description, string? Category, string ConfigJson, string? DataSourceJson);
public sealed record ReportDefinitionUpdateRequest(string Name, string? Description, string? Category, string ConfigJson, string? DataSourceJson, string? PrintTemplateJson);
