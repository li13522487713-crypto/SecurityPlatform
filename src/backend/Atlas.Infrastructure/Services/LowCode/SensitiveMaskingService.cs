using System.Text.RegularExpressions;
using Atlas.Application.LowCode.Abstractions;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>简单脱敏（M13 S13-4 + lowcode-resilience-spec §7）。</summary>
public sealed class SensitiveMaskingService : ISensitiveMaskingService
{
    private static readonly (Regex Re, string Replacement)[] Patterns =
    {
        (new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled), "***REDACTED_AWSKEY***"),
        (new Regex(@"sk-[A-Za-z0-9_\-]{20,}", RegexOptions.Compiled), "***REDACTED_APIKEY***"),
        (new Regex(@"\b(1[3-9]\d)(\d{4})(\d{4})\b", RegexOptions.Compiled), "$1****$3"),
        (new Regex(@"([A-Za-z0-9._%+-]+)@([A-Za-z0-9.-]+\.[A-Za-z]{2,})", RegexOptions.Compiled), "***@$2"),
        (new Regex(@"\b\d{17}[\dXx]\b", RegexOptions.Compiled), "***REDACTED_IDCARD***")
    };

    public string Mask(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
        var s = input;
        foreach (var (re, rep) in Patterns) s = re.Replace(s, rep);
        return s;
    }
}
