namespace Atlas.Application.LowCode.Models;

public sealed record DashboardDefinitionListItem(string Id, string Name, string? Description, string? Category, int Version, string Status, bool IsLargeScreen, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record DashboardDefinitionDetail(string Id, string Name, string? Description, string? Category, string LayoutJson, int Version, string Status, bool IsLargeScreen, int? CanvasWidth, int? CanvasHeight, string? ThemeJson, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long CreatedBy, long UpdatedBy);
public sealed record DashboardDefinitionCreateRequest(string Name, string? Description, string? Category, string LayoutJson, bool IsLargeScreen, int? CanvasWidth, int? CanvasHeight);
public sealed record DashboardDefinitionUpdateRequest(string Name, string? Description, string? Category, string LayoutJson, bool IsLargeScreen, int? CanvasWidth, int? CanvasHeight, string? ThemeJson);
