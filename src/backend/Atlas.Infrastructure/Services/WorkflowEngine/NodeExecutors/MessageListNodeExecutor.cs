using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class MessageListNodeExecutor : INodeExecutor
{
    private readonly IConversationService _conversationService;

    public MessageListNodeExecutor(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.MessageList;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = ConversationNodeHelper.ResolveLong(context, "userId", "user_id");
        var conversationId = ConversationNodeHelper.ResolveLong(context, "conversationId", "conversation_id");
        if (userId <= 0 || conversationId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "MessageList 缺少 userId 或 conversationId。");
        }

        var pageSize = Math.Clamp(context.GetConfigInt32("pageSize", 20), 1, 200);
        var pageIndex = Math.Max(1, context.GetConfigInt32("pageIndex", 1));
        var limit = pageIndex * pageSize;
        var messages = await _conversationService.GetMessagesAsync(
            context.TenantId,
            userId,
            conversationId,
            includeContextMarkers: false,
            limit,
            cancellationToken);
        var pagedItems = messages
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        outputs["messages"] = JsonSerializer.SerializeToElement(pagedItems);
        outputs["total"] = JsonSerializer.SerializeToElement(messages.Count);
        return new NodeExecutionResult(true, outputs);
    }
}
