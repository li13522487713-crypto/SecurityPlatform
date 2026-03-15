namespace Atlas.Core.Models;

public sealed record PagedRequest
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; }

    public PagedRequest()
    {
    }

    public PagedRequest(int pageIndex, int pageSize, string? keyword = null, string? sortBy = null, bool sortDesc = false)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        Keyword = keyword;
        SortBy = sortBy;
        SortDesc = sortDesc;
    }
}