using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 治理 M-G10-C2（S16）：Agent 卡片（飞书 / 微信 / 通用 schema）。
/// </summary>
[SugarTable("AgentCard")]
public sealed class AgentCard : TenantEntity
{
    public AgentCard()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        CardType = "interactive";
        SchemaJson = "{}";
        IsEnabled = false;
        CreatedAt = DateTime.UtcNow;
    }

    public AgentCard(
        TenantId tenantId,
        long agentId,
        string name,
        string cardType,
        string schemaJson,
        bool isEnabled,
        long createdBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        AgentId = agentId;
        Name = name.Trim();
        CardType = cardType.Trim().ToLowerInvariant();
        SchemaJson = string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson;
        IsEnabled = isEnabled;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AgentId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string CardType { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
    public string SchemaJson { get; private set; }

    public bool IsEnabled { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string name, string cardType, string schemaJson, bool isEnabled)
    {
        Name = name.Trim();
        CardType = cardType.Trim().ToLowerInvariant();
        SchemaJson = string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
