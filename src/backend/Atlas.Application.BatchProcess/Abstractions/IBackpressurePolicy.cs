using Atlas.Application.BatchProcess.Models;

namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// 背压策略：根据系统负载指标自适应调整批处理并发度和批次大小。
/// </summary>
public interface IBackpressurePolicy
{
    BackpressureDecision Evaluate(BackpressureMetrics metrics);
}
