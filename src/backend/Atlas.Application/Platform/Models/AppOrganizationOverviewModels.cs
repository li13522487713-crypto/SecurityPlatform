namespace Atlas.Application.Platform.Models;

public sealed record AppOrganizationOverviewResponse(
    string AppId,
    int MemberCount,
    int RoleCount,
    int DepartmentCount,
    int PositionCount,
    int ProjectCount,
    int UncoveredMemberCount,
    IReadOnlyList<AppOrganizationOverviewItem> RecentMembers,
    IReadOnlyList<AppOrganizationOverviewItem> RecentRoles,
    IReadOnlyList<AppOrganizationOverviewItem> RecentDepartments,
    IReadOnlyList<AppOrganizationOverviewItem> RecentPositions);

public sealed record AppOrganizationOverviewItem(
    string Id,
    string Title,
    string? Subtitle,
    string? Meta);
