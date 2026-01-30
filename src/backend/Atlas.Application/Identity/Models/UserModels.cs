namespace Atlas.Application.Identity.Models;

public sealed record UserListItem(
    string Id,
    string Username,
    string DisplayName,
    bool IsActive,
    string? Email,
    string? PhoneNumber,
    DateTimeOffset? LastLoginAt);

public sealed record UserDetail(
    string Id,
    string Username,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsSystem,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<long> RoleIds,
    IReadOnlyList<long> DepartmentIds,
    IReadOnlyList<long> PositionIds);

public sealed record UserCreateRequest(
    string Username,
    string Password,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    IReadOnlyList<long> RoleIds,
    IReadOnlyList<long> DepartmentIds,
    IReadOnlyList<long> PositionIds);

public sealed record UserUpdateRequest(
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool IsActive);

public sealed record UserAssignRolesRequest(IReadOnlyList<long> RoleIds);

public sealed record UserAssignDepartmentsRequest(IReadOnlyList<long> DepartmentIds);

public sealed record UserAssignPositionsRequest(IReadOnlyList<long> PositionIds);
