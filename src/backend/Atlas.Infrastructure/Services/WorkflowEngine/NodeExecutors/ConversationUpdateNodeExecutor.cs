using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class ConversationUpdateNodeExecutor : INodeExecutor
{
    private readonly IConversationService _conversationService;

    public ConversationUpdateNodeExecutor(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.ConversationUpdate;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = ConversationNodeHelper.ResolveLong(context, "userId", "user_id");
        var conversationId = ConversationNodeHelper.ResolveLong(context, "conversationId", "conversation_id");
        if (userId <= 0 || conversationId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "ConversationUpdate 缺少 userId 或 conversationId。");
        }

        var title = context.ReplaceVariables(context.GetConfigString("title"));
        if (string.IsNullOrWhiteSpace(title))
        {
            return new NodeExecutionResult(false, outputs, "ConversationUpdate 缺少 title。");
        }

        await _conversationService.UpdateAsync(
            context.TenantId,
            userId,
            conversationId,
            new ConversationUpdateRequest(title),
            cancellationToken);
        outputs["conversation_id"] = JsonSerializer.SerializeToElement(conversationId);
        return new NodeExecutionResult(true, outputs);
    }
}
