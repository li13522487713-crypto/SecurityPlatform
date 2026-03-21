using Atlas.Core.Abstractions;
using SqlSugar;

namespace Atlas.Domain.System.Entities;

public enum TenantStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2
}

public sealed class Tenant : EntityBase
{
    public Tenant()
    {
        Name = string.Empty;
        Code = string.Empty;
        Description = string.Empty;
        IsActive = true;
    }

    public Tenant(
        long id,
        string name,
        string code,
        long createdBy,
        DateTimeOffset now)
    {
        SetId(id);
        Name = name;
        Code = code;
        Description = string.Empty;
        IsActive = true;
        Status = TenantStatus.Active;
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedBy = createdBy;
        UpdatedAt = now;
    }

    public string Name { get; private set; }
    public string Code { get; private set; }
    
    [SugarColumn(IsNullable = true)]
    public string? Description { get; private set; }
    
    public bool IsActive { get; private set; }
    public TenantStatus Status { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? AdminUserId { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? ExpiredAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? TrialEndsAt { get; private set; }

    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    
    public long UpdatedBy { get; private set; }
    
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(string name, string code, string? description, long updatedBy, DateTimeOffset now)
    {
        Name = name;
        Code = code;
        Description = description ?? string.Empty;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void SetAdminUser(long adminUserId)
    {
        AdminUserId = adminUserId;
    }

    public void ToggleStatus(bool isActive, long updatedBy, DateTimeOffset now)
    {
        IsActive = isActive;
        Status = isActive ? TenantStatus.Active : TenantStatus.Inactive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Renew(DateTimeOffset newExpiredAt, long updatedBy, DateTimeOffset now)
    {
        ExpiredAt = newExpiredAt;
        if (!IsActive)
        {
            IsActive = true;
            Status = TenantStatus.Active;
        }
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiredAt.HasValue && ExpiredAt.Value < now;
    }

    public bool IsTrialExpired(DateTimeOffset now)
    {
        return TrialEndsAt.HasValue && TrialEndsAt.Value < now;
    }

    public void Suspend(long updatedBy, DateTimeOffset now)
    {
        IsActive = false;
        Status = TenantStatus.Suspended;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
