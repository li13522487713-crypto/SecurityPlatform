using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// UI Builder FAQ 条目（M14 S14-5）。
/// </summary>
public sealed class AppFaqEntry : TenantEntity
{
#pragma warning disable CS8618
    public AppFaqEntry() : base(TenantId.Empty)
    {
        Title = string.Empty;
        Body = string.Empty;
    }
#pragma warning restore CS8618

    public AppFaqEntry(TenantId tenantId, long id, string title, string body, string? tags)
        : base(tenantId)
    {
        Id = id;
        Title = title;
        Body = body;
        Tags = tags;
        Hits = 0;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string Title { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Body { get; private set; }

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? Tags { get; private set; }

    public int Hits { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string title, string body, string? tags)
    {
        Title = title;
        Body = body;
        Tags = tags;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementHits()
    {
        Hits += 1;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
