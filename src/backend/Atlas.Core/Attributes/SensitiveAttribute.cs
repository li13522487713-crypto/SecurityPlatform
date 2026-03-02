namespace Atlas.Core.Attributes;

/// <summary>
/// 敏感字段脱敏类型枚举
/// </summary>
public enum SensitiveMaskType
{
    /// <summary>手机号：138****5678</summary>
    Phone,
    /// <summary>邮箱：u***@example.com</summary>
    Email,
    /// <summary>姓名：张**（三字及以上）/ 张*（两字）</summary>
    Name,
    /// <summary>身份证：1101********1234</summary>
    IdCard,
    /// <summary>IP 地址：192.168.*.*</summary>
    IpAddress,
    /// <summary>自定义：保留前 N 位 + *** + 后 M 位</summary>
    Custom
}

/// <summary>
/// 标注需要在 API 响应中脱敏的字段（等保2.0 数据保密性要求）
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SensitiveAttribute : Attribute
{
    public SensitiveMaskType MaskType { get; }
    public int PrefixLength { get; }
    public int SuffixLength { get; }

    public SensitiveAttribute(SensitiveMaskType maskType = SensitiveMaskType.Phone)
    {
        MaskType = maskType;
    }

    public SensitiveAttribute(int prefixLength, int suffixLength)
    {
        MaskType = SensitiveMaskType.Custom;
        PrefixLength = prefixLength;
        SuffixLength = suffixLength;
    }
}
