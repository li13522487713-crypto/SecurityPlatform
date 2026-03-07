using System.Security.Cryptography;
using System.Text;

namespace Atlas.LicenseIssuer.Services;

/// <summary>
/// 密钥管理服务：生成 ECDSA P-256 密钥对，私钥使用颁发密码加密存储。
/// </summary>
public sealed class KeyManagementService
{
    private static readonly string KeyDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Atlas", "LicenseIssuer");

    private static readonly string PrivateKeyPath = Path.Combine(KeyDir, "private_key.enc");
    private static readonly string PublicKeyPath = Path.Combine(KeyDir, "public_key.pem");

    private ECDsa? _loadedKey;

    public bool IsKeyInitialized() => File.Exists(PrivateKeyPath) && File.Exists(PublicKeyPath);

    /// <summary>生成新密钥对并以颁发密码加密存储</summary>
    public void GenerateKeyPair(string password)
    {
        Directory.CreateDirectory(KeyDir);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // 导出公钥 PEM（明文，可分发给平台方嵌入）
        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(PublicKeyPath, publicKeyPem);

        // 导出私钥 DER，用密码加密后存储
        var privateKeyDer = ecdsa.ExportECPrivateKey();
        var encrypted = EncryptPrivateKey(privateKeyDer, password);
        File.WriteAllBytes(PrivateKeyPath, encrypted);
    }

    /// <summary>用颁发密码解密并加载私钥</summary>
    public bool TryLoadPrivateKey(string password)
    {
        try
        {
            if (!File.Exists(PrivateKeyPath))
                return false;

            var encrypted = File.ReadAllBytes(PrivateKeyPath);
            var privateKeyDer = DecryptPrivateKey(encrypted, password);
            if (privateKeyDer is null)
                return false;

            var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(privateKeyDer, out _);
            _loadedKey = ecdsa;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>导出公钥 PEM（用于嵌入平台）</summary>
    public string ExportPublicKeyPem()
    {
        if (!File.Exists(PublicKeyPath))
            throw new InvalidOperationException("尚未初始化密钥对");
        return File.ReadAllText(PublicKeyPath);
    }

    public ECDsa GetPrivateKey()
    {
        if (_loadedKey is null)
            throw new InvalidOperationException("私钥尚未加载，请先验证颁发密码");
        return _loadedKey;
    }

    private static byte[] EncryptPrivateKey(byte[] privateKeyDer, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = DeriveKey(password, salt);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[privateKeyDer.Length];

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, privateKeyDer, ciphertext, tag);

        // salt(16) + nonce(12) + tag(16) + ciphertext
        var result = new byte[salt.Length + nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(nonce, 0, result, salt.Length, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, salt.Length + nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, salt.Length + nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    private static byte[]? DecryptPrivateKey(byte[] data, string password)
    {
        try
        {
            const int saltLen = 16, nonceLen = 12, tagLen = 16;
            if (data.Length < saltLen + nonceLen + tagLen)
                return null;

            var salt = data[..saltLen];
            var nonce = data[saltLen..(saltLen + nonceLen)];
            var tag = data[(saltLen + nonceLen)..(saltLen + nonceLen + tagLen)];
            var ciphertext = data[(saltLen + nonceLen + tagLen)..];
            var plaintext = new byte[ciphertext.Length];

            var key = DeriveKey(password, salt);
            using var aes = new AesGcm(key, tagLen);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            200_000,
            HashAlgorithmName.SHA256,
            32);
    }
}
