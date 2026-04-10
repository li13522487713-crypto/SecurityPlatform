using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class EditMessageNodeExecutor : INodeExecutor
{
    private readonly ChatMessageRepository _chatMessageRepository;

    public EditMessageNodeExecutor(ChatMessageRepository chatMessageRepository)
    {
        _chatMessageRepository = chatMessageRepository;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.EditMessage;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var conversationId = ConversationNodeHelper.ResolveLong(context, "conversationId", "conversation_id");
        var messageId = ConversationNodeHelper.ResolveLong(context, "messageId", "message_id");
        if (conversationId <= 0 || messageId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "EditMessage 缺少 conversationId 或 messageId。");
        }

        var message = await _chatMessageRepository.FindByConversationAndIdAsync(
            context.TenantId,
            conversationId,
            messageId,
            cancellationToken);
        if (message is null)
        {
            return new NodeExecutionResult(false, outputs, "EditMessage 未找到目标消息。");
        }

        var content = context.ReplaceVariables(context.GetConfigString("content"));
        message.UpdateContent(content, context.GetConfigString("metadata"));
        await _chatMessageRepository.UpdateAsync(message, cancellationToken);
        outputs["message_id"] = JsonSerializer.SerializeToElement(messageId);
        return new NodeExecutionResult(true, outputs);
    }
}
