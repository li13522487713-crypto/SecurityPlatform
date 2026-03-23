using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// Agent 节点：调用指定 Agent 完成一次问答。
/// Config 参数：agentId、message、conversationId、enableRag、userId、outputKey
/// </summary>
public sealed class AgentNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Agent;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var outputKey = context.GetConfigString("outputKey", "agent_output");
        var agentId = context.GetConfigInt64("agentId");
        if (agentId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "Agent 节点缺少有效的 agentId 配置");
        }

        var messageTemplate = context.GetConfigString("message");
        var message = context.ReplaceVariables(messageTemplate);
        if (string.IsNullOrWhiteSpace(message))
        {
            return new NodeExecutionResult(false, outputs, "Agent 节点消息不能为空");
        }

        var conversationId = context.GetConfigInt64("conversationId");
        var userId = context.GetConfigInt64("userId", 0);
        var enableRag = context.GetConfigBoolean("enableRag", false);

        try
        {
            var chatService = context.ServiceProvider.GetRequiredService<IAgentChatService>();
            var response = await chatService.ChatAsync(
                context.TenantId,
                userId,
                agentId,
                new AgentChatRequest(
                    conversationId > 0 ? conversationId : null,
                    message,
                    enableRag),
                cancellationToken);

            outputs[outputKey] = VariableResolver.CreateStringElement(response.Content);
            outputs["agent_conversation_id"] = JsonSerializer.SerializeToElement(response.ConversationId);
            outputs["agent_message_id"] = JsonSerializer.SerializeToElement(response.MessageId);
            outputs["agent_sources"] = VariableResolver.CreateStringElement(response.Sources ?? string.Empty);

            await context.EmitEventAsync("agent_output", response.Content, cancellationToken);
            return new NodeExecutionResult(true, outputs);
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult(false, outputs, $"Agent 调用失败: {ex.Message}");
        }
    }
}
