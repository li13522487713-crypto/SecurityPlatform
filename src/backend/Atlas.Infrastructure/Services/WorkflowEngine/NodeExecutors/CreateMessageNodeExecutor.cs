using System.Text.Json;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class CreateMessageNodeExecutor : INodeExecutor
{
    private readonly ChatMessageRepository _chatMessageRepository;
    private readonly ConversationRepository _conversationRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public CreateMessageNodeExecutor(
        ChatMessageRepository chatMessageRepository,
        ConversationRepository conversationRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _chatMessageRepository = chatMessageRepository;
        _conversationRepository = conversationRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.CreateMessage;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var conversationId = ConversationNodeHelper.ResolveLong(context, "conversationId", "conversation_id");
        if (conversationId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "CreateMessage 缺少 conversationId。");
        }

        var role = context.GetConfigString("role", "user");
        var content = context.ReplaceVariables(context.GetConfigString("content"));
        var metadata = context.GetConfigString("metadata");
        var message = new ChatMessage(
            context.TenantId,
            conversationId,
            role,
            content,
            metadata,
            isContextCleared: false,
            _idGeneratorAccessor.NextId());
        await _chatMessageRepository.AddAsync(message, cancellationToken);

        var conversation = await _conversationRepository.FindByIdAsync(context.TenantId, conversationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.AddMessage(message.CreatedAt);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }

        outputs["message_id"] = JsonSerializer.SerializeToElement(message.Id);
        return new NodeExecutionResult(true, outputs);
    }
}
