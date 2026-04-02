namespace Atlas.Core.Observability;

public interface ITraceCorrelator
{
    string GetCurrentTraceId();
    string StartSpan(string operationName);
    void EndSpan(string spanId);
    void SetTag(string spanId, string key, string value);
}
