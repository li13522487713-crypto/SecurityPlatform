using Atlas.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 数据迁移连接串保护服务（M8）。
///
/// - 用 <see cref="DataProtectionService"/> 对 SourceConnectionString / TargetConnectionString 做 AES-CBC 加密
///   后再写 <c>setup_data_migration_job</c> 表，避免数据库泄漏即等同于密码泄漏（等保 2.0 红线）。
/// - master key 优先取 <c>Security:SetupConsole:MigrationProtectorKey</c>；
///   未配置时退化到 <c>Security:BootstrapAdmin:Password</c>（首装时已要求生产环境替换为强密码）；
///   都没有则用编译期默认值 + 警告（仅 dev 环境可用）。
/// - 通过 <see cref="Encrypt"/> / <see cref="Decrypt"/> 显式区分密文（带 <c>"protected:"</c> 前缀），
///   避免把密文当明文连接 SQLite/MySQL 时报"connection string format invalid"。
/// </summary>
public sealed class MigrationSecretProtector
{
    private const string ProtectedPrefix = "protected:";
    private const string DefaultDevMasterKey = "atlas-setup-console-default-dev-key-change-in-prod";

    private readonly DataProtectionService _innerService;

    public MigrationSecretProtector(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var key = configuration["Security:SetupConsole:MigrationProtectorKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            key = configuration["Security:BootstrapAdmin:Password"];
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            key = DefaultDevMasterKey;
        }
        _innerService = new DataProtectionService(key);
    }

    /// <summary>
    /// 加密明文连接串；返回带 <c>"protected:"</c> 前缀的密文。
    /// 已经是密文（带前缀）的输入将原样返回，保证幂等。
    /// </summary>
    public string Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }
        if (plainText.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return plainText;
        }
        return ProtectedPrefix + _innerService.Encrypt(plainText);
    }

    /// <summary>
    /// 解密密文连接串；不带前缀的输入按"未加密旧数据"原样返回（迁移期兼容）。
    /// </summary>
    public string Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }
        if (!cipherText.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return cipherText;
        }
        var payload = cipherText[ProtectedPrefix.Length..];
        var decrypted = _innerService.Decrypt(payload);
        // DataProtectionService 解密失败时会返回原 payload；为防止把密文误用为连接串，
        // 这里检测：解密结果如果完全等于密文 payload，认为解密失败。
        if (string.Equals(decrypted, payload, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Failed to decrypt migration connection string; master key may have been rotated.");
        }
        return decrypted;
    }
}
