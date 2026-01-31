namespace Atlas.Application.Identity.Models;

public sealed record ProjectListItem(
    string Id,
    string Code,
    string Name,
    bool IsActive,
    string? Description,
    int SortOrder);

public sealed record ProjectDetail(
    string Id,
    string Code,
    string Name,
    bool IsActive,
    string? Description,
    int SortOrder,
    IReadOnlyList<long> UserIds,
    IReadOnlyList<long> DepartmentIds,
    IReadOnlyList<long> PositionIds);

public sealed record ProjectCreateRequest(
    string Code,
    string Name,
    bool IsActive,
    string? Description,
    int SortOrder);

public sealed record ProjectUpdateRequest(
    string Name,
    bool IsActive,
    string? Description,
    int SortOrder);

public sealed record ProjectAssignUsersRequest(IReadOnlyList<long> UserIds);

public sealed record ProjectAssignDepartmentsRequest(IReadOnlyList<long> DepartmentIds);

public sealed record ProjectAssignPositionsRequest(IReadOnlyList<long> PositionIds);
