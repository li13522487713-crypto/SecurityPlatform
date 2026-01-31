using Atlas.Application.TableViews.Models;

namespace Atlas.Application.Options;

public sealed class TableViewDefaultOptions
{
    public Dictionary<string, TableViewConfig> Defaults { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public TableViewConfig? Fallback { get; init; }
}
