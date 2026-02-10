using Atlas.Application.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// TOTP implementation per RFC 6238 using HMAC-SHA1.
/// Supports 6-digit codes with 30-second time steps and +-1 step tolerance.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int SecretKeyLength = 20; // 160 bits
    private const int CodeDigits = 6;
    private const int TimeStepSeconds = 30;
    private const int ToleranceSteps = 1; // Allow 1 step before/after current

    private static readonly int[] DigitsPower = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000 };

    public string GenerateSecretKey()
    {
        var secretBytes = RandomNumberGenerator.GetBytes(SecretKeyLength);
        return Base32Encode(secretBytes);
    }

    public string GenerateCode(string secretKey, DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? DateTimeOffset.UtcNow;
        var timeStep = GetTimeStep(now);
        var key = Base32Decode(secretKey);
        return ComputeTotp(key, timeStep);
    }

    public bool ValidateCode(string secretKey, string code, DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeDigits)
        {
            return false;
        }

        var now = timestamp ?? DateTimeOffset.UtcNow;
        var currentStep = GetTimeStep(now);
        var key = Base32Decode(secretKey);

        for (var offset = -ToleranceSteps; offset <= ToleranceSteps; offset++)
        {
            var step = currentStep + offset;
            var expected = ComputeTotp(key, step);
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    public string GenerateProvisioningUri(string secretKey, string userIdentifier, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedUser = Uri.EscapeDataString(userIdentifier);
        return $"otpauth://totp/{encodedIssuer}:{encodedUser}?secret={secretKey}&issuer={encodedIssuer}&algorithm=SHA1&digits={CodeDigits}&period={TimeStepSeconds}";
    }

    private static long GetTimeStep(DateTimeOffset timestamp)
    {
        var unixTime = timestamp.ToUnixTimeSeconds();
        return unixTime / TimeStepSeconds;
    }

    private static string ComputeTotp(byte[] key, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeBytes);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var otp = binaryCode % DigitsPower[CodeDigits];
        return otp.ToString().PadLeft(CodeDigits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                sb.Append(alphabet[(buffer >> (bitsLeft - 5)) & 0x1F]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            sb.Append(alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return sb.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleanInput = base32.TrimEnd('=').ToUpperInvariant();
        var byteCount = cleanInput.Length * 5 / 8;
        var result = new byte[byteCount];

        int buffer = 0, bitsLeft = 0, index = 0;

        foreach (var c in cleanInput)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0)
            {
                throw new FormatException($"Invalid Base32 character: {c}");
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                result[index++] = (byte)(buffer >> (bitsLeft - 8));
                bitsLeft -= 8;
            }
        }

        return result;
    }
}
