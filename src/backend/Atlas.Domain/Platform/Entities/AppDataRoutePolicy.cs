using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// 应用数据路由策略：控制应用运行时对主库 / 应用库的读写路由。
/// </summary>
public sealed class AppDataRoutePolicy : TenantEntity
{
    public AppDataRoutePolicy()
        : base(TenantId.Empty)
    {
    }

    public AppDataRoutePolicy(
        TenantId tenantId,
        long appInstanceId,
        string mode,
        bool readOnlyWindow,
        bool dualWriteEnabled,
        long updatedBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        SetId(id);
        AppInstanceId = appInstanceId;
        Mode = mode;
        ReadOnlyWindow = readOnlyWindow;
        DualWriteEnabled = dualWriteEnabled;
        UpdatedBy = updatedBy;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long AppInstanceId { get; set; }

    /// <summary>路由模式：MainOnly / AppOnly / DualWrite</summary>
    public string Mode { get; set; } = "MainOnly";

    /// <summary>是否处于只读迁移窗口</summary>
    public bool ReadOnlyWindow { get; set; }

    /// <summary>是否启用主库与应用库双写</summary>
    public bool DualWriteEnabled { get; set; }

    public long UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public void SetMode(string mode, bool readOnlyWindow, bool dualWriteEnabled, long updatedBy, DateTimeOffset now)
    {
        Mode = mode;
        ReadOnlyWindow = readOnlyWindow;
        DualWriteEnabled = dualWriteEnabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
