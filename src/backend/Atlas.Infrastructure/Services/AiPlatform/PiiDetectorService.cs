using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class PiiDetectorService : IPiiDetector
{
    private static readonly Regex EmailRegex = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex MobileRegex = new(@"(?<!\d)1[3-9]\d{9}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex IdCardRegex = new(@"(?<!\d)\d{17}[\dXx](?!\d)", RegexOptions.Compiled);
    private static readonly Regex BankCardRegex = new(@"(?<!\d)\d{16,19}(?!\d)", RegexOptions.Compiled);

    public PiiDetectionResult Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new PiiDetectionResult(false, [], text ?? string.Empty);
        }

        var findings = new List<string>();
        var sanitized = text;
        sanitized = ReplaceWithMask(EmailRegex, sanitized, "EMAIL", findings);
        sanitized = ReplaceWithMask(MobileRegex, sanitized, "MOBILE", findings);
        sanitized = ReplaceWithMask(IdCardRegex, sanitized, "ID_CARD", findings);
        sanitized = ReplaceWithMask(BankCardRegex, sanitized, "BANK_CARD", findings);

        return new PiiDetectionResult(
            findings.Count > 0,
            findings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            sanitized);
    }

    private static string ReplaceWithMask(Regex regex, string input, string label, ICollection<string> findings)
    {
        return regex.Replace(input, match =>
        {
            findings.Add(label);
            return $"[{label}_MASKED]";
        });
    }
}
