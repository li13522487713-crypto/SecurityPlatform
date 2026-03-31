using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class FrameworkAwareTeamAgentOrchestrationRuntime : ITeamAgentOrchestrationRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentRepository _agentRepository;
    private readonly IAgentChatService _agentChatService;
    private readonly IOptionsMonitor<AgentFrameworkOptions> _optionsMonitor;
    private readonly ILogger<FrameworkAwareTeamAgentOrchestrationRuntime> _logger;

    public FrameworkAwareTeamAgentOrchestrationRuntime(
        AgentRepository agentRepository,
        IAgentChatService agentChatService,
        IOptionsMonitor<AgentFrameworkOptions> optionsMonitor,
        ILogger<FrameworkAwareTeamAgentOrchestrationRuntime> logger)
    {
        _agentRepository = agentRepository;
        _agentChatService = agentChatService;
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

        var options = _optionsMonitor.CurrentValue;
        var runtime = ResolveRuntimeDescriptor(request.TeamAgent.TeamMode, options);
        if (options.EmitRuntimeSelectionEvent)
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

        var turnPlan = BuildTurnPlan(request.Members, request.TeamAgent.TeamMode, options);
        var agentIds = turnPlan.Select(member => member.AgentId).Where(id => id > 0).Distinct().ToList();
        var agents = agentIds.Count == 0
            ? []
            : await _agentRepository.QueryByIdsAsync(request.TenantId, agentIds, cancellationToken);
        var agentMap = agents.ToDictionary(agent => agent.Id);

        var steps = new List<TeamAgentExecutionStep>(turnPlan.Count);
        var contributions = new List<TeamAgentMemberContribution>(turnPlan.Count);
        var currentMessage = request.Message.Trim();

        for (var index = 0; index < turnPlan.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var member = turnPlan[index];
            if (!agentMap.TryGetValue(member.AgentId, out var agent))
            {
                throw new BusinessException($"团队成员 Agent 不存在: {member.AgentId}", ErrorCodes.NotFound);
            }

            var round = index + 1;
            await onEventAsync(new TeamAgentRunEvent(
                "round.started",
                JsonSerializer.Serialize(new
                {
                    round,
                    member = member.RoleName,
                    runtime = runtime.RuntimeKey
                }, JsonOptions)));

            var inputMessage = currentMessage;
            var startedAt = DateTime.UtcNow;

            try
            {
                var prompt = BuildMemberPrompt(request.TeamAgent.TeamMode, member, inputMessage, round, runtime);
                var response = await _agentChatService.ChatAsync(
                    request.TenantId,
                    request.UserId,
                    member.AgentId,
                    new AgentChatRequest(null, prompt, request.EnableRag),
                    cancellationToken);
                var completedAt = DateTime.UtcNow;
                var output = string.IsNullOrWhiteSpace(response.Content) ? "已完成当前环节，但未返回文本内容。" : response.Content.Trim();

                var step = new TeamAgentExecutionStep(
                    member.AgentId,
                    agent.Name,
                    member.RoleName,
                    member.Alias,
                    inputMessage,
                    output,
                    "completed",
                    null,
                    startedAt,
                    completedAt);
                steps.Add(step);

                var contribution = new TeamAgentMemberContribution(
                    member.AgentId,
                    agent.Name,
                    member.RoleName,
                    member.Alias,
                    inputMessage,
                    output,
                    round,
                    startedAt,
                    completedAt);
                contributions.Add(contribution);
                await onContributionAsync(contribution);

                currentMessage = UpdateCurrentMessage(request.TeamAgent.TeamMode, inputMessage, member.RoleName, output);

                await onEventAsync(new TeamAgentRunEvent(
                    "member.message",
                    JsonSerializer.Serialize(new
                    {
                        round,
                        member = member.RoleName,
                        agentId = member.AgentId,
                        agentName = agent.Name,
                        content = output
                    }, JsonOptions)));

                if (request.TeamAgent.TeamMode == TeamAgentMode.Handoff && index < turnPlan.Count - 1)
                {
                    await onEventAsync(new TeamAgentRunEvent(
                        "execution.step",
                        JsonSerializer.Serialize(new
                        {
                            round,
                            handoffTo = turnPlan[index + 1].RoleName,
                            summary = output
                        }, JsonOptions)));
                }

                await onEventAsync(new TeamAgentRunEvent(
                    "execution.step",
                    JsonSerializer.Serialize(step, JsonOptions)));
            }
            catch (Exception ex)
            {
                var completedAt = DateTime.UtcNow;
                var failedStep = new TeamAgentExecutionStep(
                    member.AgentId,
                    agent.Name,
                    member.RoleName,
                    member.Alias,
                    inputMessage,
                    null,
                    "failed",
                    ex.Message,
                    startedAt,
                    completedAt);
                steps.Add(failedStep);
                await onEventAsync(new TeamAgentRunEvent(
                    "execution.step",
                    JsonSerializer.Serialize(failedStep, JsonOptions)));
                _logger.LogError(ex, "Team Agent 编排执行失败，teamAgentId={TeamAgentId}, memberAgentId={MemberAgentId}", request.TeamAgent.Id, member.AgentId);
                throw new TeamAgentOrchestrationExecutionException(ex.Message, steps.ToList(), contributions.ToList(), ex);
            }
        }

        return new TeamAgentOrchestrationRuntimeResult(currentMessage, steps, contributions, runtime);
    }

    private static TeamAgentRuntimeDescriptor ResolveRuntimeDescriptor(TeamAgentMode mode, AgentFrameworkOptions options)
    {
        if (!options.Enabled)
        {
            return new TeamAgentRuntimeDescriptor("atlas.internal", "Atlas Internal Runtime", "Atlas", "Atlas.Infrastructure", "local");
        }

        var preferredRuntime = options.PreferredRuntime.Trim();
        if (string.Equals(preferredRuntime, "semantic-kernel", StringComparison.OrdinalIgnoreCase))
        {
            return ToSemanticKernelDescriptor(options);
        }

        if (string.Equals(preferredRuntime, "microsoft-agent-framework", StringComparison.OrdinalIgnoreCase))
        {
            return ToMicrosoftAgentFrameworkDescriptor(mode, options);
        }

        return mode switch
        {
            TeamAgentMode.GroupChat when options.PreferSemanticKernelForGroupChat => ToSemanticKernelDescriptor(options),
            TeamAgentMode.Workflow when options.PreferMicrosoftAgentFrameworkForWorkflow => ToMicrosoftAgentFrameworkDescriptor(mode, options),
            TeamAgentMode.Handoff when options.PreferMicrosoftAgentFrameworkForHandoff => ToMicrosoftAgentFrameworkDescriptor(mode, options),
            _ => ToMicrosoftAgentFrameworkDescriptor(mode, options)
        };
    }

    private static TeamAgentRuntimeDescriptor ToSemanticKernelDescriptor(AgentFrameworkOptions options)
        => new(
            "semantic-kernel.group-chat",
            "Semantic Kernel Agents Orchestration",
            "Semantic Kernel",
            options.Packages.SemanticKernelOrchestration.PackageId,
            options.Packages.SemanticKernelOrchestration.Version);

    private static TeamAgentRuntimeDescriptor ToMicrosoftAgentFrameworkDescriptor(TeamAgentMode mode, AgentFrameworkOptions options)
    {
        var package = mode == TeamAgentMode.GroupChat
            ? options.Packages.MicrosoftAgentFrameworkCore
            : options.Packages.MicrosoftAgentFrameworkWorkflows;
        var runtimeKey = mode switch
        {
            TeamAgentMode.GroupChat => "microsoft-agent-framework.group-chat",
            TeamAgentMode.Workflow => "microsoft-agent-framework.workflow",
            TeamAgentMode.Handoff => "microsoft-agent-framework.handoff",
            _ => "microsoft-agent-framework"
        };
        var displayName = mode switch
        {
            TeamAgentMode.GroupChat => "Microsoft Agent Framework Group Chat",
            TeamAgentMode.Workflow => "Microsoft Agent Framework Workflow",
            TeamAgentMode.Handoff => "Microsoft Agent Framework Handoff",
            _ => "Microsoft Agent Framework"
        };
        return new TeamAgentRuntimeDescriptor(
            runtimeKey,
            displayName,
            "Microsoft Agent Framework",
            package.PackageId,
            package.Version);
    }

    private static List<TeamAgentMemberItem> BuildTurnPlan(
        IReadOnlyList<TeamAgentMemberItem> members,
        TeamAgentMode mode,
        AgentFrameworkOptions options)
    {
        if (mode != TeamAgentMode.GroupChat || members.Count == 0)
        {
            return members.ToList();
        }

        var maximumRounds = Math.Max(members.Count, options.GroupChatMaximumRounds);
        var turns = new List<TeamAgentMemberItem>(maximumRounds);
        for (var index = 0; index < maximumRounds; index++)
        {
            turns.Add(members[index % members.Count]);
        }

        return turns;
    }

    private static string BuildMemberPrompt(
        TeamAgentMode mode,
        TeamAgentMemberItem member,
        string message,
        int round,
        TeamAgentRuntimeDescriptor runtime)
    {
        var modeText = mode switch
        {
            TeamAgentMode.GroupChat => "请以团队群聊成员身份发言，先回应当前上下文，再补充可交给下一位成员继续讨论的结论。",
            TeamAgentMode.Workflow => "请以工作流节点身份输出结构化结论，确保下一节点能直接消费你的结果。",
            TeamAgentMode.Handoff => "请完成当前环节，并给出清晰的交接摘要、风险提示和下一步建议。",
            _ => string.Empty
        };
        return $"{member.PromptPrefix}\n角色：{member.RoleName}\n职责：{member.Responsibility}\n轮次：{round}\n框架：{runtime.RuntimeDisplayName}\n模式：{mode}\n{modeText}\n输入：{message}";
    }

    private static string UpdateCurrentMessage(TeamAgentMode mode, string currentMessage, string roleName, string output)
        => mode switch
        {
            TeamAgentMode.GroupChat => $"{currentMessage}\n\n[{roleName}] {output}",
            TeamAgentMode.Handoff => $"上一环节成员：{roleName}\n交接摘要：{output}",
            _ => output
        };
}
