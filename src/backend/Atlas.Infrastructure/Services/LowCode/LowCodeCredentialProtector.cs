using Atlas.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 低代码凭据保护（M18 收尾，等保 2.0）。
///
/// 用 AES（DataProtectionService 提供的 AES-CBC + PKCS7）封装插件 API Key / OAuth 客户端密钥等敏感字段，
/// 与 MigrationSecretProtector 一致的"带前缀+幂等"语义，避免重复加密与误把密文当明文。
///
/// 主密钥优先级：
///   Security:LowCode:CredentialProtectorKey
/// → Security:SetupConsole:MigrationProtectorKey（与 MigrationSecretProtector 复用，便于运维统一）
/// → Security:BootstrapAdmin:Password
/// → DefaultDevKey（仅开发；生产强制替换）
///
/// 为避免循环依赖，本类仅引用 Atlas.Infrastructure.Security.DataProtectionService。
/// </summary>
public sealed class LowCodeCredentialProtector
{
    public const string ProtectedPrefix = "lcp:";
    private const string DefaultDevKey = "atlas-lowcode-credential-default-dev-key-change-in-prod";

    private readonly DataProtectionService _inner;

    public LowCodeCredentialProtector(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var key = configuration["Security:LowCode:CredentialProtectorKey"]
            ?? configuration["Security:SetupConsole:MigrationProtectorKey"]
            ?? configuration["Security:BootstrapAdmin:Password"]
            ?? DefaultDevKey;
        _inner = new DataProtectionService(key);
    }

    /// <summary>加密；空字符串原样返回；已加密则幂等。</summary>
    public string Encrypt(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        if (plain.StartsWith(ProtectedPrefix, StringComparison.Ordinal)) return plain;
        return ProtectedPrefix + _inner.Encrypt(plain);
    }

    /// <summary>解密；非密文（无前缀）原样返回（向后兼容旧 base64 占位）。</summary>
    public string Decrypt(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return string.Empty;
        if (!cipher.StartsWith(ProtectedPrefix, StringComparison.Ordinal)) return cipher;
        var payload = cipher[ProtectedPrefix.Length..];
        return _inner.Decrypt(payload);
    }

    /// <summary>对外只露脱敏字符串：保留前 4 与后 2，中间 ****。</summary>
    public static string Mask(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        if (plain.Length <= 6) return new string('*', plain.Length);
        return $"{plain[..4]}****{plain[^2..]}";
    }

    public bool IsEncrypted(string? value)
        => !string.IsNullOrEmpty(value) && value.StartsWith(ProtectedPrefix, StringComparison.Ordinal);
}
