using System.Text.RegularExpressions;

namespace Atlas.Core.Exceptions;

public sealed class BusinessException : Exception
{
    private static readonly Regex ErrorCodePattern = new("^[A-Z][A-Z0-9_]+$", RegexOptions.Compiled);

    public BusinessException(string first, string second)
        : base(ResolveMessage(first, second))
    {
        Code = ResolveCode(first, second);
    }

    public string Code { get; }

    private static string ResolveMessage(string first, string second)
    {
        return LooksLikeCode(first) && !LooksLikeCode(second) ? second : first;
    }

    private static string ResolveCode(string first, string second)
    {
        return LooksLikeCode(first) && !LooksLikeCode(second) ? first : second;
    }

    private static bool LooksLikeCode(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && ErrorCodePattern.IsMatch(value);
    }
}
