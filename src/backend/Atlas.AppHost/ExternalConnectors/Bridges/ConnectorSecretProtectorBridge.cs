using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Atlas.AppHost.ExternalConnectors.Bridges;

/// <summary>
/// 把 Atlas.Infrastructure 的 DataProtectionService 桥接为 ISecretProtector，
/// 让 Atlas.Infrastructure.ExternalConnectors 不必直接依赖 Atlas.Infrastructure。
///
/// master key 优先取 ExternalConnectors:DataProtectionKey；
/// 缺省时回退 Security:SetupConsole:MigrationProtectorKey 与开发默认 key（同 PlatformHost 桥接保持一致，便于跨进程读写同一份密文）。
/// 生产环境必须显式配置；否则连接器密钥与本地数据库泄漏等价。
/// </summary>
public sealed class ConnectorSecretProtectorBridge : ISecretProtector
{
    private const string DefaultDevMasterKey = "atlas-external-connectors-default-dev-key-change-in-prod";

    private readonly DataProtectionService _innerService;

    public ConnectorSecretProtectorBridge(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var key = configuration["ExternalConnectors:DataProtectionKey"]
            ?? configuration["Security:SetupConsole:MigrationProtectorKey"]
            ?? DefaultDevMasterKey;
        _innerService = new DataProtectionService(key);
    }

    public string Encrypt(string plainText) => _innerService.Encrypt(plainText);

    public string Decrypt(string cipherText) => _innerService.Decrypt(cipherText);
}
