using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class ConversationListNodeExecutor : INodeExecutor
{
    private readonly IConversationService _conversationService;

    public ConversationListNodeExecutor(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.ConversationList;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = ConversationNodeHelper.ResolveLong(context, "userId", "user_id");
        if (userId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "ConversationList 缺少 userId。");
        }

        var pageIndex = ConversationNodeHelper.ResolveInt(context, "pageIndex", 1);
        var pageSize = Math.Clamp(context.GetConfigInt32("pageSize", 20), 1, 200);
        var agentId = ConversationNodeHelper.ResolveLong(context, "agentId", "agent_id");
        var result = agentId > 0
            ? await _conversationService.ListByAgentAsync(context.TenantId, agentId, userId, pageIndex, pageSize, cancellationToken)
            : await _conversationService.ListByUserAsync(context.TenantId, userId, pageIndex, pageSize, cancellationToken);
        outputs["conversations"] = JsonSerializer.SerializeToElement(result.Items);
        outputs["total"] = JsonSerializer.SerializeToElement(result.Total);
        return new NodeExecutionResult(true, outputs);
    }
}
