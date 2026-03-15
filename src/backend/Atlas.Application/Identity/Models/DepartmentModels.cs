namespace Atlas.Application.Identity.Models;

public sealed record DepartmentListItem(
    string Id,
    string Name,
    string Code,
    long? ParentId,
    int SortOrder);

public sealed record DepartmentCreateRequest(
    string Name,
    string Code,
    long? ParentId,
    int SortOrder);

public sealed record DepartmentUpdateRequest(
    string Name,
    string Code,
    long? ParentId,
    int SortOrder);
