namespace Atlas.Application.License.Abstractions;

/// <summary>
/// 本地激活状态密封服务：将激活状态持久化到机器绑定的加密存储。
/// Windows 使用 DPAPI，Linux 回退到 AES 文件加密。
/// </summary>
public interface ILicenseStateSealService
{
    /// <summary>密封并保存本地激活状态</summary>
    void Seal(LicenseSealedState state);

    /// <summary>读取并解封本地激活状态（无记录或解密失败时返回 null）</summary>
    LicenseSealedState? Unseal();

    /// <summary>删除本地激活状态（用于重置）</summary>
    void Clear();
}

public sealed class LicenseSealedState
{
    public Guid LicenseId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string ActivationNonce { get; set; } = string.Empty;
    public DateTimeOffset FirstActivatedAt { get; set; }
    public DateTimeOffset LastValidatedAt { get; set; }
    public DateTimeOffset MaxObservedUtc { get; set; }
}
