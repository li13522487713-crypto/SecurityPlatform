#pragma warning disable SKEXP0110
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using DomainAgent = Atlas.Domain.AiPlatform.Entities.Agent;
using TeamAgentMode = Atlas.Domain.AiPlatform.Entities.TeamAgentMode;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class FrameworkAwareTeamAgentOrchestrationRuntime : ITeamAgentOrchestrationRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentRepository _agentRepository;
    private readonly IChatClientFactory _chatClientFactory;
    private readonly IKernelFactory _kernelFactory;
    private readonly IAgentRuntimeFactory _agentRuntimeFactory;
    private readonly IOptionsMonitor<AgentFrameworkOptions> _optionsMonitor;
    private readonly ILogger<FrameworkAwareTeamAgentOrchestrationRuntime> _logger;

    public FrameworkAwareTeamAgentOrchestrationRuntime(
        AgentRepository agentRepository,
        IChatClientFactory chatClientFactory,
        IKernelFactory kernelFactory,
        IAgentRuntimeFactory agentRuntimeFactory,
        IOptionsMonitor<AgentFrameworkOptions> optionsMonitor,
        ILogger<FrameworkAwareTeamAgentOrchestrationRuntime> logger)
    {
        _agentRepository = agentRepository;
        _chatClientFactory = chatClientFactory;
        _kernelFactory = kernelFactory;
        _agentRuntimeFactory = agentRuntimeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<TeamAgentOrchestrationRuntimeResult> ExecuteAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        Func<TeamAgentRunEvent, Task> onEventAsync,
        Func<TeamAgentMemberContribution, Task> onContributionAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onEventAsync);
        ArgumentNullException.ThrowIfNull(onContributionAsync);

        if (request.Members.Count == 0)
        {
            throw new BusinessException("Team Agent 至少需要一个启用成员。", ErrorCodes.ValidationError);
        }

        var runtime = await _agentRuntimeFactory.ResolveRuntimeAsync(
            request.TenantId,
            request.TeamAgent.TeamMode,
            request.Members,
            cancellationToken);

        if (_optionsMonitor.CurrentValue.EmitRuntimeSelectionEvent)
        {
            await onEventAsync(new TeamAgentRunEvent(
                "orchestration.runtime.selected",
                JsonSerializer.Serialize(new
                {
                    runtime.RuntimeKey,
                    runtime.RuntimeDisplayName,
                    runtime.FrameworkFamily,
                    runtime.PackageId,
                    runtime.PackageVersion,
                    teamMode = request.TeamAgent.TeamMode.ToString()
                }, JsonOptions)));
        }

        var enabledMembers = request.Members
            .Where(member => member.IsEnabled)
            .OrderBy(member => member.SortOrder)
            .ThenBy(member => member.RoleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (enabledMembers.Count == 0)
        {
            throw new BusinessException("Team Agent 至少需要一个启用成员。", ErrorCodes.ValidationError);
        }

        var agentIds = enabledMembers
            .Where(member => member.AgentId.HasValue && member.AgentId.Value > 0)
            .Select(member => member.AgentId!.Value)
            .Distinct()
            .ToList();
        if (agentIds.Count == 0)
        {
            throw new BusinessException("Team Agent 至少需要一个已绑定的启用成员。", ErrorCodes.ValidationError);
        }

        var agents = await _agentRepository.QueryByIdsAsync(request.TenantId, agentIds, cancellationToken);
        var agentMap = agents.ToDictionary(agent => agent.Id);
        foreach (var member in enabledMembers)
        {
            if (!member.AgentId.HasValue || !agentMap.ContainsKey(member.AgentId.Value))
            {
                throw new BusinessException($"团队成员 Agent 不存在: {member.AgentId}", ErrorCodes.NotFound);
            }
        }

        return request.TeamAgent.TeamMode switch
        {
            TeamAgentMode.GroupChat => await ExecuteGroupChatAsync(request, enabledMembers, agentMap, runtime, onEventAsync, onContributionAsync, cancellationToken),
            TeamAgentMode.Workflow => await ExecuteWorkflowAsync(request, enabledMembers, agentMap, runtime, onEventAsync, onContributionAsync, cancellationToken),
            TeamAgentMode.Handoff => await ExecuteHandoffAsync(request, enabledMembers, agentMap, runtime, onEventAsync, onContributionAsync, cancellationToken),
            _ => throw new BusinessException("未知 Team Agent 模式。", ErrorCodes.ValidationError)
        };
    }

    private async Task<TeamAgentOrchestrationRuntimeResult> ExecuteGroupChatAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        IReadOnlyList<TeamAgentMemberItem> members,
        IReadOnlyDictionary<long, DomainAgent> agentMap,
        TeamAgentRuntimeDescriptor runtime,
        Func<TeamAgentRunEvent, Task> onEventAsync,
        Func<TeamAgentMemberContribution, Task> onContributionAsync,
        CancellationToken cancellationToken)
    {
        var participantMap = new Dictionary<string, (TeamAgentMemberItem Member, DomainAgent Agent)>(StringComparer.OrdinalIgnoreCase);
        var participants = new List<ChatCompletionAgent>(members.Count);

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var agent = agentMap[member.AgentId!.Value];
            var kernel = await _kernelFactory.CreateAsync(request.TenantId, agent.ModelConfigId, agent.ModelName, cancellationToken);
            participants.Add(new ChatCompletionAgent
            {
                Name = member.RoleName,
                Description = string.IsNullOrWhiteSpace(member.Responsibility) ? member.RoleName : member.Responsibility,
                Instructions = BuildAgentInstructions(member, request.TeamAgent.TeamMode),
                Kernel = kernel
            });
            participantMap[member.RoleName] = (member, agent);
        }

        var steps = new List<TeamAgentExecutionStep>(members.Count);
        var contributions = new List<TeamAgentMemberContribution>(members.Count);
        var roundCounter = 0;
        var sharedInput = request.Message.Trim();

        async ValueTask HandleResponseAsync(ChatMessageContent message)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var authorName = message.AuthorName ?? string.Empty;
            if (!participantMap.TryGetValue(authorName, out var participant))
            {
                _logger.LogDebug("GroupChat 返回了未映射成员: {AuthorName}", authorName);
                return;
            }

            roundCounter++;
            var output = ExtractText(message);
            var startedAt = DateTime.UtcNow;
            var completedAt = DateTime.UtcNow;

            await onEventAsync(new TeamAgentRunEvent("round.started", JsonSerializer.Serialize(new
            {
                round = roundCounter,
                member = participant.Member.RoleName,
                runtime = runtime.RuntimeKey
            }, JsonOptions)));

            var step = new TeamAgentExecutionStep(
                0,
                participant.Member.AgentId,
                participant.Agent.Name,
                participant.Member.RoleName,
                participant.Member.Alias,
                sharedInput,
                output,
                "completed",
                null,
                startedAt,
                completedAt);
            steps.Add(step);

            var contribution = new TeamAgentMemberContribution(
                participant.Member.AgentId!.Value,
                participant.Agent.Name,
                participant.Member.RoleName,
                participant.Member.Alias,
                sharedInput,
                output,
                roundCounter,
                startedAt,
                completedAt);
            contributions.Add(contribution);

            await onContributionAsync(contribution);
            await onEventAsync(new TeamAgentRunEvent("member.message", JsonSerializer.Serialize(new
            {
                round = roundCounter,
                member = participant.Member.RoleName,
                agentId = participant.Member.AgentId,
                agentName = participant.Agent.Name,
                content = output
            }, JsonOptions)));
            await onEventAsync(new TeamAgentRunEvent("execution.step", JsonSerializer.Serialize(step, JsonOptions)));

            sharedInput = UpdateCurrentMessage(request.TeamAgent.TeamMode, sharedInput, participant.Member.RoleName, output);
        }

        var orchestration = new GroupChatOrchestration(
            new Microsoft.SemanticKernel.Agents.Orchestration.GroupChat.RoundRobinGroupChatManager
            {
                MaximumInvocationCount = Math.Max(members.Count, _optionsMonitor.CurrentValue.GroupChatMaximumRounds)
            },
            participants.ToArray())
        {
            ResponseCallback = HandleResponseAsync
        };

        var runtimeHost = new InProcessRuntime();
        await runtimeHost.StartAsync();
        var result = await orchestration.InvokeAsync(request.Message.Trim(), runtimeHost);
        var finalMessage = NormalizeText(await result.GetValueAsync());

        return new TeamAgentOrchestrationRuntimeResult(finalMessage, steps, contributions, runtime);
    }

    private async Task<TeamAgentOrchestrationRuntimeResult> ExecuteWorkflowAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        IReadOnlyList<TeamAgentMemberItem> members,
        IReadOnlyDictionary<long, DomainAgent> agentMap,
        TeamAgentRuntimeDescriptor runtime,
        Func<TeamAgentRunEvent, Task> onEventAsync,
        Func<TeamAgentMemberContribution, Task> onContributionAsync,
        CancellationToken cancellationToken)
    {
        var builtAgents = await BuildWorkflowAgentsAsync(request, members, agentMap, cancellationToken);
        var workflow = AgentWorkflowBuilder.BuildSequential(
            $"team-agent-{request.TeamAgent.Id}-workflow",
            builtAgents.Select(item => item.RuntimeAgent).ToArray());
        return await ExecuteWorkflowRunAsync(
            request,
            members,
            runtime,
            workflow,
            builtAgents.ToDictionary(item => item.ExecutorId, StringComparer.OrdinalIgnoreCase),
            onEventAsync,
            onContributionAsync,
            cancellationToken);
    }

    private async Task<TeamAgentOrchestrationRuntimeResult> ExecuteHandoffAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        IReadOnlyList<TeamAgentMemberItem> members,
        IReadOnlyDictionary<long, DomainAgent> agentMap,
        TeamAgentRuntimeDescriptor runtime,
        Func<TeamAgentRunEvent, Task> onEventAsync,
        Func<TeamAgentMemberContribution, Task> onContributionAsync,
        CancellationToken cancellationToken)
    {
        var builtAgents = await BuildWorkflowAgentsAsync(request, members, agentMap, cancellationToken);
        if (builtAgents.Count == 0)
        {
            throw new BusinessException("Team Agent 至少需要一个启用成员。", ErrorCodes.ValidationError);
        }

        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(builtAgents[0].RuntimeAgent);
        for (var index = 0; index < builtAgents.Count - 1; index++)
        {
            builder = builder.WithHandoff(
                builtAgents[index].RuntimeAgent,
                builtAgents[index + 1].RuntimeAgent,
                $"由 {builtAgents[index].Member.RoleName} 交接给 {builtAgents[index + 1].Member.RoleName}");
        }

        return await ExecuteWorkflowRunAsync(
            request,
            members,
            runtime,
            builder.Build(),
            builtAgents.ToDictionary(item => item.ExecutorId, StringComparer.OrdinalIgnoreCase),
            onEventAsync,
            onContributionAsync,
            cancellationToken);
    }

    private static async Task<TeamAgentOrchestrationRuntimeResult> ExecuteWorkflowRunAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        IReadOnlyList<TeamAgentMemberItem> members,
        TeamAgentRuntimeDescriptor runtime,
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        IReadOnlyDictionary<string, BuiltWorkflowAgent> participantMap,
        Func<TeamAgentRunEvent, Task> onEventAsync,
        Func<TeamAgentMemberContribution, Task> onContributionAsync,
        CancellationToken cancellationToken)
    {
        var steps = new List<TeamAgentExecutionStep>(members.Count);
        var contributions = new List<TeamAgentMemberContribution>(members.Count);
        var stepCounter = 0;
        var currentInput = request.Message.Trim();
        var startedAtByExecutorId = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var finalMessage = currentInput;

        await using var execution = await InProcessExecution.RunAsync(
            workflow,
            request.Message.Trim(),
            $"team-agent-{request.TeamAgent.Id}",
            cancellationToken);

        foreach (var workflowEvent in execution.NewEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (workflowEvent)
            {
                case ExecutorInvokedEvent invokedEvent:
                    startedAtByExecutorId[invokedEvent.ExecutorId] = DateTime.UtcNow;
                    if (participantMap.TryGetValue(invokedEvent.ExecutorId, out var startedParticipant))
                    {
                        await onEventAsync(new TeamAgentRunEvent("round.started", JsonSerializer.Serialize(new
                        {
                            round = stepCounter + 1,
                            member = startedParticipant.Member.RoleName,
                            runtime = runtime.RuntimeKey
                        }, JsonOptions)));
                    }
                    break;

                case AgentResponseEvent responseEvent:
                    if (!participantMap.TryGetValue(responseEvent.ExecutorId, out var participant))
                    {
                        break;
                    }

                    stepCounter++;
                    var output = ExtractText(responseEvent.Response.Messages);
                    var startedAt = startedAtByExecutorId.TryGetValue(responseEvent.ExecutorId, out var trackedStartedAt)
                        ? trackedStartedAt
                        : DateTime.UtcNow;
                    var completedAt = DateTime.UtcNow;
                    var step = new TeamAgentExecutionStep(
                        0,
                        participant.Member.AgentId,
                        participant.Agent.Name,
                        participant.Member.RoleName,
                        participant.Member.Alias,
                        currentInput,
                        output,
                        "completed",
                        null,
                        startedAt,
                        completedAt);
                    steps.Add(step);

                    var contribution = new TeamAgentMemberContribution(
                        participant.Member.AgentId!.Value,
                        participant.Agent.Name,
                        participant.Member.RoleName,
                        participant.Member.Alias,
                        currentInput,
                        output,
                        stepCounter,
                        startedAt,
                        completedAt);
                    contributions.Add(contribution);

                    await onContributionAsync(contribution);
                    await onEventAsync(new TeamAgentRunEvent("member.message", JsonSerializer.Serialize(new
                    {
                        round = stepCounter,
                        member = participant.Member.RoleName,
                        agentId = participant.Member.AgentId,
                        agentName = participant.Agent.Name,
                        content = output
                    }, JsonOptions)));
                    await onEventAsync(new TeamAgentRunEvent("execution.step", JsonSerializer.Serialize(step, JsonOptions)));

                    currentInput = UpdateCurrentMessage(request.TeamAgent.TeamMode, currentInput, participant.Member.RoleName, output);
                    break;

                case ExecutorFailedEvent failedEvent:
                    throw new TeamAgentOrchestrationExecutionException(
                        failedEvent.Data?.Message ?? "Team Agent 工作流执行失败。",
                        steps,
                        contributions,
                        failedEvent.Data ?? new InvalidOperationException("Team Agent 工作流执行失败。"));

                case WorkflowErrorEvent errorEvent:
                    throw new TeamAgentOrchestrationExecutionException(
                        errorEvent.Exception?.Message ?? "Team Agent 工作流执行失败。",
                        steps,
                        contributions,
                        errorEvent.Exception ?? new InvalidOperationException("Team Agent 工作流执行失败。"));

                case WorkflowOutputEvent outputEvent:
                    finalMessage = NormalizeText(outputEvent.Data);
                    break;
            }
        }

        return new TeamAgentOrchestrationRuntimeResult(finalMessage, steps, contributions, runtime);
    }

    private async Task<List<BuiltWorkflowAgent>> BuildWorkflowAgentsAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        IReadOnlyList<TeamAgentMemberItem> members,
        IReadOnlyDictionary<long, DomainAgent> agentMap,
        CancellationToken cancellationToken)
    {
        var built = new List<BuiltWorkflowAgent>(members.Count);
        for (var index = 0; index < members.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var member = members[index];
            var agent = agentMap[member.AgentId!.Value];
            var chatClient = await _chatClientFactory.CreateAsync(request.TenantId, agent.ModelConfigId, agent.ModelName, cancellationToken);
            var executorId = BuildExecutorId(member, index);
            var runtimeAgent = new ChatClientAgent(
                chatClient,
                new ChatClientAgentOptions
                {
                    Id = executorId,
                    Name = member.RoleName,
                    Description = string.IsNullOrWhiteSpace(member.Responsibility) ? member.RoleName : member.Responsibility,
                    ChatOptions = new ChatOptions
                    {
                        Instructions = BuildAgentInstructions(member, request.TeamAgent.TeamMode)
                    }
                });

            built.Add(new BuiltWorkflowAgent(executorId, member, agent, runtimeAgent));
        }

        return built;
    }

    private static string BuildAgentInstructions(TeamAgentMemberItem member, TeamAgentMode mode)
    {
        var prefix = string.IsNullOrWhiteSpace(member.PromptPrefix) ? string.Empty : $"{member.PromptPrefix.Trim()}\n";
        var modeInstructions = mode switch
        {
            TeamAgentMode.GroupChat => "你是团队群聊中的固定成员。请结合已有讨论继续推进，不要重复前一位成员的内容。",
            TeamAgentMode.Workflow => "你是工作流中的执行节点。请输出可直接被下一节点消费的结构化结论。",
            TeamAgentMode.Handoff => "你处于接力协作链路中。请完成当前任务，并输出明确交接摘要、风险和下一步建议。",
            _ => "请根据输入完成当前任务。"
        };

        var capabilityText = member.CapabilityTags.Count == 0
            ? "无显式能力标签"
            : string.Join("、", member.CapabilityTags);

        return $"{prefix}角色：{member.RoleName}\n职责：{member.Responsibility ?? "未提供"}\n能力标签：{capabilityText}\n{modeInstructions}";
    }

    private static string BuildExecutorId(TeamAgentMemberItem member, int index)
        => $"member-{index + 1}-{Slugify(member.RoleName)}";

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "agent";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string UpdateCurrentMessage(TeamAgentMode mode, string currentMessage, string roleName, string output)
        => mode switch
        {
            TeamAgentMode.GroupChat => $"{currentMessage}\n\n[{roleName}] {output}",
            TeamAgentMode.Handoff => $"上一环节成员：{roleName}\n交接摘要：{output}",
            _ => output
        };

    private static string ExtractText(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        var content = string.Join(
            "\n",
            messages
                .Select(NormalizeText)
                .Where(item => !string.IsNullOrWhiteSpace(item)));

        return string.IsNullOrWhiteSpace(content) ? "已完成当前环节，但未返回文本内容。" : content.Trim();
    }

    private static string ExtractText(ChatMessageContent message)
        => NormalizeText(message);

    private static string NormalizeText(object? value)
    {
        if (value is null)
        {
            return "已完成当前环节，但未返回文本内容。";
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "已完成当前环节，但未返回文本内容。" : text.Trim();
        }

        return string.IsNullOrWhiteSpace(value.ToString())
            ? "已完成当前环节，但未返回文本内容。"
            : value.ToString()!.Trim();
    }

    private sealed record BuiltWorkflowAgent(
        string ExecutorId,
        TeamAgentMemberItem Member,
        DomainAgent Agent,
        AIAgent RuntimeAgent);
}
