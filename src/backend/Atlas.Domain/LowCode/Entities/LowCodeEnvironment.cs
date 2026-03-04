using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码应用环境配置（dev/test/prod），支持变量替换。
/// </summary>
public sealed class LowCodeEnvironment : TenantEntity
{
    public LowCodeEnvironment()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        VariablesJson = "{}";
    }

    public LowCodeEnvironment(
        TenantId tenantId,
        long appId,
        string name,
        string code,
        string? description,
        bool isDefault,
        string variablesJson,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Name = name;
        Code = code;
        Description = description;
        IsDefault = isDefault;
        VariablesJson = string.IsNullOrWhiteSpace(variablesJson) ? "{}" : variablesJson;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    public long AppId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public string VariablesJson { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }

    public void Update(
        string name,
        string? description,
        bool isDefault,
        string variablesJson,
        bool isActive,
        long updatedBy,
        DateTimeOffset now)
    {
        Name = name;
        Description = description;
        IsDefault = isDefault;
        VariablesJson = string.IsNullOrWhiteSpace(variablesJson) ? "{}" : variablesJson;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
