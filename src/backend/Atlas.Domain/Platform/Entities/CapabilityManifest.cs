using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Platform.Entities;

public sealed class CapabilityManifest : TenantEntity
{
    public CapabilityManifest()
        : base(TenantId.Empty)
    {
        CapabilityKey = string.Empty;
        Title = string.Empty;
        Category = string.Empty;
        HostModesJson = "[]";
        PlatformRoute = string.Empty;
        AppRoute = string.Empty;
        RequiredPermissionsJson = "[]";
        NavigationJson = "{}";
        SupportedCommandsJson = "[]";
        IsEnabled = true;
    }

    public CapabilityManifest(
        TenantId tenantId,
        long id,
        string capabilityKey,
        string title,
        string category,
        DateTimeOffset updatedAt,
        long updatedBy)
        : base(tenantId)
    {
        Id = id;
        CapabilityKey = capabilityKey;
        Title = title;
        Category = category;
        HostModesJson = "[]";
        PlatformRoute = string.Empty;
        AppRoute = string.Empty;
        RequiredPermissionsJson = "[]";
        NavigationJson = "{}";
        SupportedCommandsJson = "[]";
        SupportsExposure = false;
        IsEnabled = true;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    [SugarColumn(Length = 128)]
    public string CapabilityKey { get; private set; }

    [SugarColumn(Length = 256)]
    public string Title { get; private set; }

    [SugarColumn(Length = 128)]
    public string Category { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string HostModesJson { get; private set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string PlatformRoute { get; private set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string AppRoute { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string RequiredPermissionsJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string NavigationJson { get; private set; }

    public bool SupportsExposure { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SupportedCommandsJson { get; private set; }

    public bool IsEnabled { get; private set; }

    public long UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string title,
        string category,
        string hostModesJson,
        string? platformRoute,
        string? appRoute,
        string requiredPermissionsJson,
        string navigationJson,
        bool supportsExposure,
        string supportedCommandsJson,
        bool isEnabled,
        DateTimeOffset updatedAt,
        long updatedBy)
    {
        Title = title;
        Category = category;
        HostModesJson = hostModesJson;
        PlatformRoute = platformRoute ?? string.Empty;
        AppRoute = appRoute ?? string.Empty;
        RequiredPermissionsJson = requiredPermissionsJson;
        NavigationJson = navigationJson;
        SupportsExposure = supportsExposure;
        SupportedCommandsJson = supportedCommandsJson;
        IsEnabled = isEnabled;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }
}
