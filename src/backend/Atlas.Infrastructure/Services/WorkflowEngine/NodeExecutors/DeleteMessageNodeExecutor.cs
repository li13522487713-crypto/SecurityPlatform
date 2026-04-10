using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class DeleteMessageNodeExecutor : INodeExecutor
{
    private readonly IConversationService _conversationService;

    public DeleteMessageNodeExecutor(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.DeleteMessage;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = ConversationNodeHelper.ResolveLong(context, "userId", "user_id");
        var conversationId = ConversationNodeHelper.ResolveLong(context, "conversationId", "conversation_id");
        var messageId = ConversationNodeHelper.ResolveLong(context, "messageId", "message_id");
        if (userId <= 0 || conversationId <= 0 || messageId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "DeleteMessage 缺少 userId/conversationId/messageId。");
        }

        await _conversationService.DeleteMessageAsync(
            context.TenantId,
            userId,
            conversationId,
            messageId,
            cancellationToken);
        outputs["deleted"] = JsonSerializer.SerializeToElement(true);
        return new NodeExecutionResult(true, outputs);
    }
}
