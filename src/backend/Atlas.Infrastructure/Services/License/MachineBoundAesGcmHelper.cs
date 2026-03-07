using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Services.License;

internal static class MachineBoundAesGcmHelper
{
    private const int NonceLength = 12;
    private const int TagLength = 16;

    public static byte[] Encrypt(byte[] plainBytes, string domainSuffix)
    {
        var key = DeriveLinuxKey(domainSuffix);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var tag = new byte[TagLength];
        var ciphertext = new byte[plainBytes.Length];

        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        // 格式：nonce(12) + tag(16) + ciphertext
        var result = new byte[NonceLength + TagLength + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceLength);
        Buffer.BlockCopy(tag, 0, result, NonceLength, TagLength);
        Buffer.BlockCopy(ciphertext, 0, result, NonceLength + TagLength, ciphertext.Length);
        return result;
    }

    public static byte[]? Decrypt(byte[] data, string domainSuffix)
    {
        try
        {
            if (data.Length < NonceLength + TagLength)
                return null;

            var key = DeriveLinuxKey(domainSuffix);
            var nonce = data[..NonceLength];
            var tag = data[NonceLength..(NonceLength + TagLength)];
            var ciphertext = data[(NonceLength + TagLength)..];
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DeriveLinuxKey(string domainSuffix)
    {
        var seed = ReadMachineSeed();
        return SHA256.HashData(Encoding.UTF8.GetBytes(seed + "|" + domainSuffix));
    }

    private static string ReadMachineSeed()
    {
        try
        {
            return File.Exists("/etc/machine-id")
                ? File.ReadAllText("/etc/machine-id").Trim()
                : Environment.MachineName;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
