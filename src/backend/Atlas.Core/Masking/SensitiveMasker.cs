using Atlas.Core.Attributes;

namespace Atlas.Core.Masking;

/// <summary>
/// 数据脱敏工具类（等保2.0 敏感数据保护）
/// </summary>
public static class SensitiveMasker
{
    private const string Asterisks = "****";

    public static string Mask(string? value, SensitiveMaskType maskType, int prefixLength = 0, int suffixLength = 0)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;

        return maskType switch
        {
            SensitiveMaskType.Phone => MaskPhone(value),
            SensitiveMaskType.Email => MaskEmail(value),
            SensitiveMaskType.Name => MaskName(value),
            SensitiveMaskType.IdCard => MaskIdCard(value),
            SensitiveMaskType.IpAddress => MaskIpAddress(value),
            SensitiveMaskType.Custom => MaskCustom(value, prefixLength, suffixLength),
            _ => Asterisks
        };
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 7) return Asterisks;
        return phone[..3] + "****" + phone[^4..];
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return Asterisks;
        var prefix = email[..atIndex];
        var domain = email[atIndex..];
        var maskedPrefix = prefix.Length > 1
            ? prefix[0] + "***"
            : "*";
        return maskedPrefix + domain;
    }

    private static string MaskName(string name)
    {
        if (name.Length <= 1) return "*";
        if (name.Length == 2) return name[0] + "*";
        return name[0] + new string('*', name.Length - 1);
    }

    private static string MaskIdCard(string idCard)
    {
        if (idCard.Length < 8) return Asterisks;
        return idCard[..4] + new string('*', idCard.Length - 8) + idCard[^4..];
    }

    private static string MaskIpAddress(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return ip;
        return $"{parts[0]}.{parts[1]}.*.*";
    }

    private static string MaskCustom(string value, int prefixLength, int suffixLength)
    {
        var total = prefixLength + suffixLength;
        if (value.Length <= total) return Asterisks;
        return value[..prefixLength] + "***" + value[^suffixLength..];
    }
}
