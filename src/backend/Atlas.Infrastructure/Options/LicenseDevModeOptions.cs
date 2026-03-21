namespace Atlas.Infrastructure.Options;

/// <summary>
/// License 调试模式配置（仅限开发/测试环境）。
/// 开启后平台跳过证书上传，直接以此处固定配置视为已激活的 Enterprise 永久授权。
/// 生产环境必须保持 Enabled = false 或不存在此配置节。
/// </summary>
public sealed class LicenseDevModeOptions
{
    public bool Enabled { get; init; }

    /// <summary>调试授权绑定的租户 ID，应与 BootstrapAdmin.TenantId 保持一致</summary>
    public string TenantId { get; init; } = "00000000-0000-0000-0000-000000000001";

    /// <summary>模拟的证书版本（Trial / Pro / Enterprise）</summary>
    public string Edition { get; init; } = "Enterprise";

    /// <summary>显示在授权状态中的客户/组织名称</summary>
    public string CustomerName { get; init; } = string.Empty;
}
