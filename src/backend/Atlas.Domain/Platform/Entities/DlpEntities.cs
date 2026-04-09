using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Platform.Entities;

[SugarTable("DataClassification")]
[SugarIndex("UX_DataClassification_Tenant_Code", nameof(TenantIdValue), OrderByType.Asc, nameof(Code), OrderByType.Asc, true)]
public sealed class DataClassification : TenantEntity
{
    public DataClassification()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        Scope = "tenant";
        BaselineJson = "{}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public DataClassification(
        TenantId tenantId,
        long id,
        string code,
        string name,
        int level,
        string scope,
        string baselineJson,
        long updatedBy,
        DateTimeOffset updatedAt)
        : base(tenantId)
    {
        Id = id;
        Code = code;
        Name = name;
        Level = level;
        Scope = scope;
        BaselineJson = baselineJson;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public int Level { get; private set; }
    public string Scope { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string BaselineJson { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

[SugarTable("SensitiveLabel")]
[SugarIndex("UX_SensitiveLabel_Tenant_Code", nameof(TenantIdValue), OrderByType.Asc, nameof(Code), OrderByType.Asc, true)]
public sealed class SensitiveLabel : TenantEntity
{
    public SensitiveLabel()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        TargetType = "field";
        RuleJson = "{}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public SensitiveLabel(
        TenantId tenantId,
        long id,
        string code,
        string name,
        string targetType,
        string ruleJson,
        long updatedBy,
        DateTimeOffset updatedAt)
        : base(tenantId)
    {
        Id = id;
        Code = code;
        Name = name;
        TargetType = targetType;
        RuleJson = ruleJson;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string TargetType { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string RuleJson { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

[SugarTable("DlpPolicy")]
public sealed class DlpPolicy : TenantEntity
{
    public DlpPolicy()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        ScopeJson = "{}";
        ChannelRuleJson = "{}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public DlpPolicy(
        TenantId tenantId,
        long id,
        string name,
        bool enabled,
        string scopeJson,
        string channelRuleJson,
        long updatedBy,
        DateTimeOffset updatedAt)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Enabled = enabled;
        ScopeJson = scopeJson;
        ChannelRuleJson = channelRuleJson;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    public string Name { get; private set; }
    public bool Enabled { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ScopeJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ChannelRuleJson { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

[SugarTable("DlpOutboundChannel")]
[SugarIndex("UX_DlpOutboundChannel_Tenant_Key", nameof(TenantIdValue), OrderByType.Asc, nameof(ChannelKey), OrderByType.Asc, true)]
public sealed class DlpOutboundChannel : TenantEntity
{
    public DlpOutboundChannel()
        : base(TenantId.Empty)
    {
        ChannelKey = string.Empty;
        DisplayName = string.Empty;
        ChannelType = string.Empty;
        ConfigJson = "{}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public DlpOutboundChannel(
        TenantId tenantId,
        long id,
        string channelKey,
        string displayName,
        string channelType,
        bool enabled,
        string configJson,
        long updatedBy,
        DateTimeOffset updatedAt)
        : base(tenantId)
    {
        Id = id;
        ChannelKey = channelKey;
        DisplayName = displayName;
        ChannelType = channelType;
        Enabled = enabled;
        ConfigJson = configJson;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    public string ChannelKey { get; private set; }
    public string DisplayName { get; private set; }
    public string ChannelType { get; private set; }
    public bool Enabled { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ConfigJson { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

[SugarTable("LeakageEvent")]
public sealed class LeakageEvent : TenantEntity
{
    public LeakageEvent()
        : base(TenantId.Empty)
    {
        DataSet = string.Empty;
        ChannelKey = string.Empty;
        Decision = string.Empty;
        Reason = string.Empty;
        TargetSummary = string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public LeakageEvent(
        TenantId tenantId,
        long id,
        long? policyId,
        long appInstanceId,
        string dataSet,
        string channelKey,
        string decision,
        string reason,
        string targetSummary,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        PolicyId = policyId;
        AppInstanceId = appInstanceId;
        DataSet = dataSet;
        ChannelKey = channelKey;
        Decision = decision;
        Reason = reason;
        TargetSummary = targetSummary;
        CreatedAt = createdAt;
    }

    [SugarColumn(IsNullable = true)]
    public long? PolicyId { get; private set; }
    public long AppInstanceId { get; private set; }
    public string DataSet { get; private set; }
    public string ChannelKey { get; private set; }
    public string Decision { get; private set; }
    public string Reason { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string TargetSummary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

[SugarTable("EvidencePackage")]
public sealed class EvidencePackage : TenantEntity
{
    public EvidencePackage()
        : base(TenantId.Empty)
    {
        SummaryJson = "{}";
        Status = "Ready";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public EvidencePackage(
        TenantId tenantId,
        long id,
        long leakageEventId,
        string summaryJson,
        string status,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        LeakageEventId = leakageEventId;
        SummaryJson = summaryJson;
        Status = status;
        CreatedAt = createdAt;
    }

    public long LeakageEventId { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string SummaryJson { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

[SugarTable("ExternalShareApproval")]
public sealed class ExternalShareApproval : TenantEntity
{
    public ExternalShareApproval()
        : base(TenantId.Empty)
    {
        DataSet = string.Empty;
        Target = string.Empty;
        Reason = string.Empty;
        Status = "Pending";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public ExternalShareApproval(
        TenantId tenantId,
        long id,
        long appInstanceId,
        string dataSet,
        string target,
        string reason,
        string status,
        long createdBy,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        DataSet = dataSet;
        Target = target;
        Reason = reason;
        Status = status;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public long AppInstanceId { get; private set; }
    public string DataSet { get; private set; }
    public string Target { get; private set; }
    public string Reason { get; private set; }
    public string Status { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
