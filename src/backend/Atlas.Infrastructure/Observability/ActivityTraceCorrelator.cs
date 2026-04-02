using System.Collections.Concurrent;
using System.Diagnostics;
using Atlas.Core.Observability;

namespace Atlas.Infrastructure.Observability;

public sealed class ActivityTraceCorrelator : ITraceCorrelator
{
    private static readonly ActivitySource Source = new("Atlas.SecurityPlatform.LogicFlow");
    private readonly ConcurrentDictionary<string, Activity> _activities = new(StringComparer.Ordinal);

    public string GetCurrentTraceId()
    {
        var id = Activity.Current?.TraceId.ToHexString();
        return string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
    }

    public string StartSpan(string operationName)
    {
        var activity = Source.StartActivity(operationName);
        var spanId = activity?.SpanId.ToHexString() ?? Guid.NewGuid().ToString("N")[..16];
        if (activity is not null)
            _activities[spanId] = activity;
        return spanId;
    }

    public void EndSpan(string spanId)
    {
        if (_activities.TryRemove(spanId, out var activity))
            activity.Dispose();
    }

    public void SetTag(string spanId, string key, string value)
    {
        if (_activities.TryGetValue(spanId, out var activity))
            activity.SetTag(key, value);
    }
}
