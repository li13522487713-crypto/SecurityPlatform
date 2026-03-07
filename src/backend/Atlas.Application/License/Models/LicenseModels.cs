using Atlas.Domain.License;

namespace Atlas.Application.License.Models;

/// <summary>当前授权状态（供控制器和前端消费）</summary>
public sealed record LicenseStatusDto(
    string Status,
    string Edition,
    bool IsPermanent,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    int? RemainingDays,
    /// <summary>证书是否绑定到特定机器（false 表示任意机器可用）</summary>
    bool MachineBound,
    /// <summary>当前机器是否与证书绑定的机器匹配；未绑定时始终为 true</summary>
    bool MachineMatched,
    IReadOnlyDictionary<string, bool> Features,
    IReadOnlyDictionary<string, int> Limits,
    /// <summary>证书中的客户 ID（若为合法 GUID 则可直接用作租户 ID）</summary>
    string? TenantId,
    /// <summary>证书中的客户名称（组织名）</summary>
    string? TenantName
)
{
    public static LicenseStatusDto None() =>
        new(
            "None",
            LicenseEdition.Trial.ToString(),
            false,
            DateTimeOffset.MinValue,
            null,
            null,
            false,
            false,
            new Dictionary<string, bool>(),
            new Dictionary<string, int>(),
            null,
            null);
}

/// <summary>证书激活操作结果</summary>
public sealed record LicenseActivationResult(bool Success, string Message);

/// <summary>证书 Payload 结构（颁发工具与平台共享的数据契约）</summary>
public sealed class LicensePayload
{
    public Guid LicenseId { get; set; }
    public int Revision { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    /// <summary>证书中声明的平台租户 ID（颁发工具填入，平台激活时用于自动关联租户）</summary>
    public string TenantId { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsPermanent { get; set; }
    public string Edition { get; set; } = string.Empty;

    /// <summary>机器码哈希（为 null 表示不绑定机器）</summary>
    public string? MachineFingerprint { get; set; }

    public Dictionary<string, bool> Features { get; set; } = new();
    public Dictionary<string, int> Limits { get; set; } = new();
}

/// <summary>证书 Header</summary>
public sealed class LicenseHeader
{
    public string Version { get; set; } = "1.0";
    public string Algorithm { get; set; } = "ECDSA-SHA256";
    public string Kid { get; set; } = "atlas-2026-01";
}

/// <summary>完整证书信封（Header + Payload + Signature）</summary>
public sealed class LicenseEnvelope
{
    public LicenseHeader Header { get; set; } = new();
    public LicensePayload Payload { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
}
