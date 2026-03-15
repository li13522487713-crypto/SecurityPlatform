namespace Atlas.Application.Identity.Models;

public sealed class RouterVo
{
    public bool AlwaysShow { get; set; }
    public bool Hidden { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Redirect { get; set; }
    public string? Query { get; set; }
    public string? Component { get; set; }
    public RouterMeta? Meta { get; set; }
    public List<RouterVo>? Children { get; set; }
}

public sealed class RouterMeta
{
    public string Title { get; set; } = string.Empty;
    public string? TitleKey { get; set; }
    public string? Icon { get; set; }
    public bool NoCache { get; set; }
    public string? Link { get; set; }
    public string? Permi { get; set; }
}
