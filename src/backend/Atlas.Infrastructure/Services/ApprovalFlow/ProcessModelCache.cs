using Atlas.Domain.Approval.Entities;
using Atlas.Infrastructure.Caching;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 流程模型缓存
/// </summary>
public sealed class ProcessModelCache
{
    private readonly IAtlasHybridCache _cache;

    public ProcessModelCache(IAtlasHybridCache cache)
    {
        _cache = cache;
    }

    public FlowDefinition GetOrAdd(ApprovalFlowDefinition flowDef)
    {
        var key = $"approval:flow-definition:{flowDef.Id}:{flowDef.Version}";
        return HybridCacheSyncBridge.Run(_cache.GetOrCreateAsync(
                   key,
                   _ => new ValueTask<FlowDefinition?>(FlowDefinitionParser.Parse(flowDef.DefinitionJson)),
                   TimeSpan.FromHours(1)))!;
    }

    public void Remove(long flowId, int version)
    {
        var key = $"approval:flow-definition:{flowId}:{version}";
        HybridCacheSyncBridge.Run(_cache.RemoveAsync(key));
    }
}
