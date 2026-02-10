using Microsoft.Extensions.Caching.Memory;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 流程模型缓存
/// </summary>
public sealed class ProcessModelCache
{
    private readonly IMemoryCache _cache;

    public ProcessModelCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public FlowDefinition GetOrAdd(ApprovalFlowDefinition flowDef)
    {
        var key = $"FlowDefinition:{flowDef.Id}:{flowDef.Version}";
        return _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return FlowDefinitionParser.Parse(flowDef.DefinitionJson);
        })!;
    }

    public void Remove(long flowId, int version)
    {
        var key = $"FlowDefinition:{flowId}:{version}";
        _cache.Remove(key);
    }
}
