using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class KnowledgeRetrieverStep : StepBodyAsync
{
    private readonly IRagRetrievalService _ragRetrievalService;

    public KnowledgeRetrieverStep(IRagRetrievalService ragRetrievalService)
    {
        _ragRetrievalService = ragRetrievalService;
    }

    public string? Query { get; set; }
    public string KnowledgeBaseIds { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public string OutputKey { get; set; } = "ragResults";

    public override async Task<ExecutionResult> RunAsync(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var query = WorkflowStepDataHelper.ResolveTemplate(Query, data);
        if (string.IsNullOrWhiteSpace(query))
        {
            data[OutputKey] = Array.Empty<object>();
            return ExecutionResult.Next();
        }

        var knowledgeBaseIds = ParseKnowledgeBaseIds(data);
        var tenantId = ResolveTenantId(data);
        var result = await _ragRetrievalService.SearchAsync(tenantId, knowledgeBaseIds, query, TopK, context.CancellationToken);
        data[OutputKey] = result;
        return ExecutionResult.Next();
    }

    private List<long> ParseKnowledgeBaseIds(IReadOnlyDictionary<string, object?> data)
    {
        var raw = WorkflowStepDataHelper.ResolveTemplate(KnowledgeBaseIds, data);
        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => long.TryParse(x, out var id) ? id : 0L)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    private static TenantId ResolveTenantId(IReadOnlyDictionary<string, object?> data)
    {
        if (data.TryGetValue("tenantId", out var raw) && Guid.TryParse(Convert.ToString(raw), out var id))
        {
            return new TenantId(id);
        }

        return TenantId.Empty;
    }
}
