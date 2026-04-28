using System.Text.RegularExpressions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed record AiDatabasePhysicalNames(
    string LogicalName,
    string DraftName,
    string OnlineName,
    string DraftFileName,
    string OnlineFileName);

public static partial class AiDatabasePhysicalNameBuilder
{
    public static AiDatabasePhysicalNames Build(TenantId tenantId, long databaseId, string driverCode)
    {
        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException("tenantId is required.", nameof(tenantId));
        }

        if (databaseId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(databaseId), "databaseId must be positive.");
        }

        var driver = SafeIdentifier(driverCode);
        var tenantSegment = tenantId.Value.ToString("N");
        var logical = SafeIdentifier($"atlas_aidb_{driver}_{tenantSegment}_{databaseId}");
        var draft = $"{logical}_draft";
        var online = $"{logical}_online";
        return new AiDatabasePhysicalNames(logical, draft, online, $"{draft}.db", $"{online}.db");
    }

    public static string SafeIdentifier(string value)
    {
        var normalized = UnsafeIdentifierChars().Replace(value.Trim().ToLowerInvariant(), "_");
        normalized = RepeatedUnderscores().Replace(normalized, "_").Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Identifier cannot be empty.", nameof(value));
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = $"d_{normalized}";
        }

        return normalized;
    }

    public static string BuildSqlitePath(string root, string fileName)
    {
        if (Path.IsPathRooted(fileName) || fileName.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsafe SQLite database file name.");
        }

        var safeFile = SafeSqliteFileName().IsMatch(fileName)
            ? fileName
            : throw new InvalidOperationException("Unsafe SQLite database file name.");
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, safeFile));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQLite database path escapes configured root.");
        }

        return fullPath;
    }

    [GeneratedRegex(@"[^a-z0-9_]+")]
    private static partial Regex UnsafeIdentifierChars();

    [GeneratedRegex(@"_+")]
    private static partial Regex RepeatedUnderscores();

    [GeneratedRegex(@"^[a-zA-Z0-9_.-]+\.db$")]
    private static partial Regex SafeSqliteFileName();
}
