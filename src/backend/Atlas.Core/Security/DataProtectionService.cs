using System.Security.Cryptography;
using System.Text;

namespace Atlas.Core.Security;

public sealed class DataProtectionService
{
    private readonly byte[] _key;

    public DataProtectionService(string masterKey)
    {
        if (string.IsNullOrWhiteSpace(masterKey))
        {
            throw new ArgumentException("Master key cannot be empty", nameof(masterKey));
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(masterKey));
    }

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

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

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

            var iv = new byte[16];
            Buffer.BlockCopy(cipherBytes, 0, iv, 0, 16);
            aes.IV = iv;

            var cipherData = new byte[cipherBytes.Length - 16];
            Buffer.BlockCopy(cipherBytes, 16, cipherData, 0, cipherData.Length);

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherData, 0, cipherData.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return cipherText;
        }
    }
}
