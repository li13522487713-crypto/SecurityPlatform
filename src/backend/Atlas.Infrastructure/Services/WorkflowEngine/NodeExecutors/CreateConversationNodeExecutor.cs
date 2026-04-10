using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class CreateConversationNodeExecutor : INodeExecutor
{
    private readonly IConversationService _conversationService;

    public CreateConversationNodeExecutor(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.CreateConversation;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var userId = ConversationNodeHelper.ResolveLong(context, "userId", "user_id");
        var agentId = ConversationNodeHelper.ResolveLong(context, "agentId", "agent_id");
        if (userId <= 0 || agentId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "CreateConversation 缺少 userId 或 agentId。");
        }

        var title = context.ReplaceVariables(context.GetConfigString("title"));
        var conversationId = await _conversationService.CreateAsync(
            context.TenantId,
            userId,
            new ConversationCreateRequest(agentId, title),
            cancellationToken);
        outputs["conversation_id"] = JsonSerializer.SerializeToElement(conversationId);
        return new NodeExecutionResult(true, outputs);
    }
}
