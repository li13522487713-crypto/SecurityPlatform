using Atlas.Core.Models;

public sealed record UserQueryRequest(
    int PageIndex,
    int PageSize,
    string? Keyword,
    string? SortBy,
    bool SortDesc,
    long? DepartmentId
);
