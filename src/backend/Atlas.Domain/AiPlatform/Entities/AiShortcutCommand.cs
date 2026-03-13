using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiShortcutCommand : TenantEntity
{
    public AiShortcutCommand()
        : base(TenantId.Empty)
    {
        CommandKey = string.Empty;
        DisplayName = string.Empty;
        TargetPath = string.Empty;
        Description = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiShortcutCommand(
        TenantId tenantId,
        string commandKey,
        string displayName,
        string targetPath,
        string? description,
        int sortOrder,
        long id)
        : base(tenantId)
    {
        Id = id;
        CommandKey = commandKey;
        DisplayName = displayName;
        TargetPath = targetPath;
        Description = description ?? string.Empty;
        SortOrder = sortOrder;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string CommandKey { get; private set; }
    public string DisplayName { get; private set; }
    public string TargetPath { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(string displayName, string targetPath, string? description, int sortOrder, bool isEnabled)
    {
        DisplayName = displayName;
        TargetPath = targetPath;
        Description = description ?? string.Empty;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
