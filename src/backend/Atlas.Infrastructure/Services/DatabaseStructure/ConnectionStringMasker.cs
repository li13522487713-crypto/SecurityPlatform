using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public static partial class ConnectionStringMasker
{
    public static string Mask(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        return SensitiveKeyPattern().Replace(
            connectionString,
            match => $"{match.Groups["key"].Value}=***");
    }

    [GeneratedRegex(@"(?i)(?<key>Password|Pwd|User Password)\s*=\s*[^;]*")]
    private static partial Regex SensitiveKeyPattern();
}
