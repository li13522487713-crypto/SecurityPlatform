using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Observability;

/// <summary>
/// 告警规则配置（可观测性 - 阈值触发通知，关联事件中心）
/// </summary>
public sealed class AlertRule : TenantEntity
{
    public AlertRule()
        : base(TenantId.Empty)
    {
    }

    public AlertRule(TenantId tenantId, long id, string name, string metricName)
        : base(tenantId)
    {
        SetId(id);
        Name = name;
        MetricName = metricName;
    }

    /// <summary>规则名称（如：登录失败率告警）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>关联的 Metric 名称（如：atlas.auth.login.fail.count）</summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>告警条件算子（gt/lt/gte/lte）</summary>
    public string Operator { get; set; } = "gt";

    /// <summary>告警触发阈值</summary>
    [SugarColumn(ColumnDataType = "decimal(18,4)")]
    public decimal Threshold { get; set; }

    /// <summary>统计时间窗口（分钟）</summary>
    public int WindowMinutes { get; set; } = 5;

    /// <summary>触发后发布的事件类型（关联事件订阅中心）</summary>
    public string EventType { get; set; } = "alert.triggered";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? LastTriggeredAt { get; set; }

    public bool EvaluateThreshold(decimal value) => Operator switch
    {
        "gt" => value > Threshold,
        "gte" => value >= Threshold,
        "lt" => value < Threshold,
        "lte" => value <= Threshold,
        _ => false
    };

    public void RecordTriggered(DateTimeOffset now)
    {
        LastTriggeredAt = now;
        UpdatedAt = now;
    }
}
