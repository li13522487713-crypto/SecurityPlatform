using Atlas.Core.Abstractions;

namespace Atlas.Domain.License;

/// <summary>
/// 授权证书激活记录（全局实体，不隔离租户）
/// </summary>
public sealed class LicenseRecord : EntityBase
{
    public LicenseRecord() { }

    public LicenseRecord(
        long id,
        Guid licenseId,
        int revision,
        LicenseEdition edition,
        DateTimeOffset issuedAt,
        DateTimeOffset? expiresAt,
        bool isPermanent,
        string? machineFingerprintHash,
        string payloadHash,
        string rawLicenseCiphertext,
        string featuresJson,
        string limitsJson,
        DateTimeOffset activatedAt,
        string customerId = "",
        string customerName = "")
    {
        SetId(id);
        LicenseId = licenseId;
        Revision = revision;
        Edition = edition;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        IsPermanent = isPermanent;
        MachineFingerprintHash = string.IsNullOrWhiteSpace(machineFingerprintHash)
            ? string.Empty
            : machineFingerprintHash;
        PayloadHash = payloadHash;
        RawLicenseCiphertext = rawLicenseCiphertext;
        FeaturesJson = featuresJson;
        LimitsJson = limitsJson;
        Status = LicenseStatus.Active;
        ActivatedAt = activatedAt;
        LastValidatedAt = activatedAt;
        MaxObservedUtc = activatedAt;
        CustomerId = customerId ?? string.Empty;
        CustomerName = customerName ?? string.Empty;
    }

    /// <summary>证书唯一标识（同一客户续签保持不变）</summary>
    public Guid LicenseId { get; private set; }

    /// <summary>证书版本号，续签时单调递增</summary>
    public int Revision { get; private set; }

    /// <summary>套餐版本</summary>
    public LicenseEdition Edition { get; private set; }

    /// <summary>颁发时间</summary>
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>到期时间（永久授权时为 null）</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>是否永久授权</summary>
    public bool IsPermanent { get; private set; }

    /// <summary>绑定机器码哈希（不绑定时为空字符串）</summary>
    public string? MachineFingerprintHash { get; private set; }

    /// <summary>Payload 内容哈希，防止替换攻击</summary>
    public string PayloadHash { get; private set; } = string.Empty;

    /// <summary>当前证书状态</summary>
    public LicenseStatus Status { get; private set; }

    /// <summary>首次激活时间</summary>
    public DateTimeOffset ActivatedAt { get; private set; }

    /// <summary>最后校验通过时间</summary>
    public DateTimeOffset LastValidatedAt { get; private set; }

    /// <summary>历史观测到的最大 UTC 时间（用于时间回拨检测）</summary>
    public DateTimeOffset MaxObservedUtc { get; private set; }

    /// <summary>原始证书内容（加密存储）</summary>
    public string RawLicenseCiphertext { get; private set; } = string.Empty;

    /// <summary>Payload 中的功能开关（JSON 序列化，空串表示全用版本默认值）</summary>
    public string FeaturesJson { get; private set; } = string.Empty;

    /// <summary>Payload 中的限额配置（JSON 序列化，空串表示全用版本默认值）</summary>
    public string LimitsJson { get; private set; } = string.Empty;

    /// <summary>证书颁发时的客户 ID（可为 GUID 格式的租户 ID）</summary>
    public string CustomerId { get; private set; } = string.Empty;

    /// <summary>证书颁发时的客户名称（组织名）</summary>
    public string CustomerName { get; private set; } = string.Empty;

    public void MarkValidated(DateTimeOffset now)
    {
        LastValidatedAt = now;
        if (now > MaxObservedUtc)
        {
            MaxObservedUtc = now;
        }
    }

    public void MarkExpired()
    {
        Status = LicenseStatus.Expired;
    }

    public void MarkInvalid()
    {
        Status = LicenseStatus.Invalid;
    }
}
