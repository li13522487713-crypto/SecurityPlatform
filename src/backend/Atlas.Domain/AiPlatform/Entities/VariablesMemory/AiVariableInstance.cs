using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiVariableInstance : TenantEntity
{
    public AiVariableInstance() : base(TenantId.Empty)
    {
        OwnerType = string.Empty;
        ConnectorId = string.Empty;
        ConnectorUserId = string.Empty;
        ValueJson = "{}";
        Version = string.Empty;
    }

    public long VariableId { get; private set; }
    public string OwnerType { get; private set; }
    public long OwnerId { get; private set; }
    public string ConnectorId { get; private set; }
    public string ConnectorUserId { get; private set; }
    public string ValueJson { get; private set; }
    public string Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
