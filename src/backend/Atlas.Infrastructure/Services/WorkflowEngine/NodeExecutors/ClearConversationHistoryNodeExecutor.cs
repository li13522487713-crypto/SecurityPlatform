using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class ClearConversationHistoryNodeExecutor : INodeExecutor
{
    private readonly IConversationService _conversationService;

    public ClearConversationHistoryNodeExecutor(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.ClearConversationHistory;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = ConversationNodeHelper.ResolveLong(context, "userId", "user_id");
        var conversationId = ConversationNodeHelper.ResolveLong(context, "conversationId", "conversation_id");
        if (userId <= 0 || conversationId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "ClearConversationHistory 缺少 userId 或 conversationId。");
        }

        await _conversationService.ClearContextAsync(context.TenantId, userId, conversationId, cancellationToken);
        outputs["cleared"] = JsonSerializer.SerializeToElement(true);
        return new NodeExecutionResult(true, outputs);
    }
}
