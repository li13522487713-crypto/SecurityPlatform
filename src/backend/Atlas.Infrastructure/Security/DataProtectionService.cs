using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// 数据保护服务（用于加密敏感数据，如SecretKey）
/// </summary>
public sealed class DataProtectionService
{
    private readonly byte[] _key;

    public DataProtectionService(string masterKey)
    {
        if (string.IsNullOrWhiteSpace(masterKey))
        {
            throw new ArgumentException("Master key cannot be empty", nameof(masterKey));
        }

        // 使用SHA256从主密钥派生32字节密钥
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(masterKey));
    }

    /// <summary>
    /// 加密数据
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // 将IV和密文组合：IV(16字节) + 密文
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密数据
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }

        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            if (cipherBytes.Length < 16)
            {
                throw new CryptographicException("Invalid cipher text");
            }

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // 提取IV（前16字节）
            var iv = new byte[16];
            Buffer.BlockCopy(cipherBytes, 0, iv, 0, 16);
            aes.IV = iv;

            // 提取密文（剩余字节）
            var cipherData = new byte[cipherBytes.Length - 16];
            Buffer.BlockCopy(cipherBytes, 16, cipherData, 0, cipherData.Length);

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherData, 0, cipherData.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // 如果解密失败，可能是旧数据（未加密），返回原值
            // 在生产环境中，应该记录日志并处理
            return cipherText;
        }
    }
}
