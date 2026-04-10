using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class ConversationHistoryNodeExecutor : INodeExecutor
{
    private readonly IConversationService _conversationService;

    public ConversationHistoryNodeExecutor(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.ConversationHistory;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = ConversationNodeHelper.ResolveLong(context, "userId", "user_id");
        var conversationId = ConversationNodeHelper.ResolveLong(context, "conversationId", "conversation_id");
        if (userId <= 0 || conversationId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "ConversationHistory 缺少 userId 或 conversationId。");
        }

        var limit = context.GetConfigInt32("limit", 20);
        var includeContextMarkers = context.GetConfigBoolean("includeContextMarkers", false);
        var messages = await _conversationService.GetMessagesAsync(
            context.TenantId,
            userId,
            conversationId,
            includeContextMarkers,
            limit > 0 ? limit : null,
            cancellationToken);
        outputs["messages"] = JsonSerializer.SerializeToElement(messages);
        return new NodeExecutionResult(true, outputs);
    }
}
