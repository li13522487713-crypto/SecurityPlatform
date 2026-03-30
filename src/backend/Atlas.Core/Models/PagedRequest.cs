namespace Atlas.Core.Models;

public sealed class PagedRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }

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