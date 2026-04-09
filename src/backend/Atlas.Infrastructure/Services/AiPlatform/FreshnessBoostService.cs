using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class FreshnessBoostService
{
    public IReadOnlyList<RagSearchResult> Apply(
        IReadOnlyList<RagSearchResult> results,
        int halfLifeDays)
    {
        if (results.Count == 0 || halfLifeDays <= 0)
        {
            return results;
        }

        var now = DateTime.UtcNow;
        return results
            .Select(item =>
            {
                if (!item.DocumentCreatedAt.HasValue)
                {
                    return item;
                }

                var days = Math.Max(0d, (now - item.DocumentCreatedAt.Value).TotalDays);
                var freshness = Math.Exp(-Math.Log(2) * (days / halfLifeDays));
                var blended = (item.Score * 0.85f) + ((float)freshness * 0.15f);
                return item with { Score = blended };
            })
            .OrderByDescending(item => item.Score)
            .ToArray();
    }
}
