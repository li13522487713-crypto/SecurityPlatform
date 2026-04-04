using System.Threading;
using Atlas.Application.Platform.Models;

namespace Atlas.Presentation.Shared.Services;

public sealed class MigrationGovernanceMetricsStore
{
    private long _totalApiHits;
    private long _legacyRouteHits;
    private long _rewriteHits;
    private long _v1EntryHits;
    private long _v2EntryHits;
    private long _notFoundCount;
    private long _fallbackCount;
    private readonly DateTimeOffset _windowStartedAt = DateTimeOffset.UtcNow;

    public void Record(string originalPath, string rewrittenPath, bool rewritten, int statusCode)
    {
        if (!IsApiPath(originalPath) && !IsApiPath(rewrittenPath))
        {
            return;
        }

        Interlocked.Increment(ref _totalApiHits);

        if (IsLegacyApiPath(originalPath))
        {
            Interlocked.Increment(ref _legacyRouteHits);
        }

        if (rewritten)
        {
            Interlocked.Increment(ref _rewriteHits);
        }

        if (rewrittenPath.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _v1EntryHits);
        }
        else if (rewrittenPath.StartsWith("/api/v2", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _v2EntryHits);
        }

        if (statusCode == StatusCodes.Status404NotFound)
        {
            Interlocked.Increment(ref _notFoundCount);
            if (rewritten)
            {
                Interlocked.Increment(ref _fallbackCount);
            }
        }
    }

    public MigrationGovernanceOverview GetSnapshot()
    {
        var totalApiHits = Interlocked.Read(ref _totalApiHits);
        var legacyRouteHits = Interlocked.Read(ref _legacyRouteHits);
        var rewriteHits = Interlocked.Read(ref _rewriteHits);
        var v1EntryHits = Interlocked.Read(ref _v1EntryHits);
        var v2EntryHits = Interlocked.Read(ref _v2EntryHits);
        var notFoundCount = Interlocked.Read(ref _notFoundCount);
        var fallbackCount = Interlocked.Read(ref _fallbackCount);
        var notFoundRate = totalApiHits == 0
            ? 0
            : decimal.Round((decimal)notFoundCount / totalApiHits, 4, MidpointRounding.AwayFromZero);
        var versionedHits = v1EntryHits + v2EntryHits;
        var newEntryCoverageRate = versionedHits == 0
            ? 0
            : decimal.Round((decimal)v2EntryHits / versionedHits, 4, MidpointRounding.AwayFromZero);

        return new MigrationGovernanceOverview(
            _windowStartedAt.ToString("O"),
            totalApiHits,
            legacyRouteHits,
            rewriteHits,
            v1EntryHits,
            v2EntryHits,
            notFoundCount,
            fallbackCount,
            notFoundRate,
            newEntryCoverageRate);
    }

    private static bool IsApiPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
               && path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyApiPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !(path.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase)
                 || path.StartsWith("/api/v2", StringComparison.OrdinalIgnoreCase)
                 || path.StartsWith("/api/v3", StringComparison.OrdinalIgnoreCase));
    }
}
