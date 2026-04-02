using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;

namespace Atlas.Infrastructure.BatchProcess.Scheduling;

/// <summary>
/// 自适应背压策略：根据 CPU / 内存 / 延迟 / 错误率等指标决定是否降级并发度和批次大小。
/// </summary>
public sealed class BackpressurePolicy : IBackpressurePolicy
{
    private const double CpuThresholdHigh = 80.0;
    private const double MemoryThresholdHigh = 85.0;
    private const double ErrorRateThresholdHigh = 0.1;
    private const double LatencyThresholdMs = 5000.0;

    public BackpressureDecision Evaluate(BackpressureMetrics metrics)
    {
        var reasons = new List<string>();

        if (metrics.CpuUsagePercent > CpuThresholdHigh)
            reasons.Add($"CPU usage {metrics.CpuUsagePercent:F1}% > {CpuThresholdHigh}%");

        if (metrics.MemoryUsagePercent > MemoryThresholdHigh)
            reasons.Add($"Memory usage {metrics.MemoryUsagePercent:F1}% > {MemoryThresholdHigh}%");

        if (metrics.ErrorRate > ErrorRateThresholdHigh)
            reasons.Add($"Error rate {metrics.ErrorRate:P1} > {ErrorRateThresholdHigh:P1}");

        if (metrics.AverageLatencyMs > LatencyThresholdMs)
            reasons.Add($"Avg latency {metrics.AverageLatencyMs:F0}ms > {LatencyThresholdMs}ms");

        if (reasons.Count == 0)
        {
            return new BackpressureDecision
            {
                ShouldThrottle = false,
                RecommendedConcurrency = metrics.MaxConcurrency,
                RecommendedBatchSize = 0,
                Reason = null
            };
        }

        var reductionFactor = Math.Max(0.25, 1.0 - reasons.Count * 0.2);
        var recommendedConcurrency = Math.Max(1, (int)(metrics.MaxConcurrency * reductionFactor));

        return new BackpressureDecision
        {
            ShouldThrottle = true,
            RecommendedConcurrency = recommendedConcurrency,
            RecommendedBatchSize = 0,
            Reason = string.Join("; ", reasons)
        };
    }
}
