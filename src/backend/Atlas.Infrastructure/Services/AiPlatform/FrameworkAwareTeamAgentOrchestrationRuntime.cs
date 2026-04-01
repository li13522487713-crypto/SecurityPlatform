#pragma warning disable SKEXP0110
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Orchestration.Handoff;
using Microsoft.SemanticKernel.Agents.Orchestration.Sequential;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using DomainAgent = Atlas.Domain.AiPlatform.Entities.Agent;
using TeamAgentMode = Atlas.Domain.AiPlatform.Entities.TeamAgentMode;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class FrameworkAwareTeamAgentOrchestrationRuntime : ITeamAgentOrchestrationRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentRepository _agentRepository;
    private readonly IKernelFactory _kernelFactory;
    private readonly IAgentRuntimeFactory _agentRuntimeFactory;
    private readonly IOptionsMonitor<AgentFrameworkOptions> _optionsMonitor;
    private readonly ILogger<FrameworkAwareTeamAgentOrchestrationRuntime> _logger;

    public FrameworkAwareTeamAgentOrchestrationRuntime(
        AgentRepository agentRepository,
        IKernelFactory kernelFactory,
        IAgentRuntimeFactory agentRuntimeFactory,
        IOptionsMonitor<AgentFrameworkOptions> optionsMonitor,
        ILogger<FrameworkAwareTeamAgentOrchestrationRuntime> logger)
    {
        _agentRepository = agentRepository;
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
        var builtAgents = await BuildAgentsAsync(request, members, agentMap, cancellationToken);
        var observer = CreateResponseObserver(
            request.TeamAgent.TeamMode,
            runtime,
            builtAgents,
            onEventAsync,
            onContributionAsync,
            cancellationToken);
        observer.InitializeInput(request.Message.Trim());

        var orchestration = new GroupChatOrchestration(
            new RoundRobinGroupChatManager
            {
                MaximumInvocationCount = Math.Max(members.Count, _optionsMonitor.CurrentValue.GroupChatMaximumRounds)
            },
            builtAgents.Select(item => item.RuntimeAgent).ToArray())
        {
            ResponseCallback = observer.HandleResponseAsync
        };

        var runtimeHost = new InProcessRuntime();
        await runtimeHost.StartAsync();
        var result = await orchestration.InvokeAsync(request.Message.Trim(), runtimeHost);
        var finalMessage = NormalizeText(await result.GetValueAsync());

        return observer.BuildResult(finalMessage);
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
        var builtAgents = await BuildAgentsAsync(request, members, agentMap, cancellationToken);
        var observer = CreateResponseObserver(
            request.TeamAgent.TeamMode,
            runtime,
            builtAgents,
            onEventAsync,
            onContributionAsync,
            cancellationToken);
        observer.InitializeInput(request.Message.Trim());

        var orchestration = new SequentialOrchestration(builtAgents.Select(item => item.RuntimeAgent).ToArray())
        {
            ResponseCallback = observer.HandleResponseAsync
        };

        var runtimeHost = new InProcessRuntime();
        await runtimeHost.StartAsync();
        var result = await orchestration.InvokeAsync(request.Message.Trim(), runtimeHost);
        var finalMessage = NormalizeText(await result.GetValueAsync());

        return observer.BuildResult(finalMessage);
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
        var builtAgents = await BuildAgentsAsync(request, members, agentMap, cancellationToken);
        if (builtAgents.Count == 0)
        {
            throw new BusinessException("Team Agent 至少需要一个启用成员。", ErrorCodes.ValidationError);
        }

        var handoffs = OrchestrationHandoffs.StartWith(builtAgents[0].RuntimeAgent);
        for (var index = 0; index < builtAgents.Count - 1; index++)
        {
            handoffs = handoffs.Add(
                builtAgents[index].RuntimeAgent,
                builtAgents[index + 1].RuntimeAgent,
                $"Transfer to {builtAgents[index + 1].Member.RoleName} when the next specialized step is required.");
        }

        var observer = CreateResponseObserver(
            request.TeamAgent.TeamMode,
            runtime,
            builtAgents,
            onEventAsync,
            onContributionAsync,
            cancellationToken);
        observer.InitializeInput(request.Message.Trim());

        var orchestration = new HandoffOrchestration(
            handoffs,
            builtAgents.Select(item => item.RuntimeAgent).ToArray())
        {
            ResponseCallback = observer.HandleResponseAsync
        };

        var runtimeHost = new InProcessRuntime();
        await runtimeHost.StartAsync();
        var result = await orchestration.InvokeAsync(request.Message.Trim(), runtimeHost);
        var finalMessage = NormalizeText(await result.GetValueAsync());

        return observer.BuildResult(finalMessage);
    }

    private async Task<List<BuiltAgent>> BuildAgentsAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        IReadOnlyList<TeamAgentMemberItem> members,
        IReadOnlyDictionary<long, DomainAgent> agentMap,
        CancellationToken cancellationToken)
    {
        var built = new List<BuiltAgent>(members.Count);
        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var agent = agentMap[member.AgentId!.Value];
            var kernel = await _kernelFactory.CreateAsync(request.TenantId, agent.ModelConfigId, agent.ModelName, cancellationToken);
            built.Add(new BuiltAgent(
                member,
                agent,
                new ChatCompletionAgent
                {
                    Name = member.RoleName,
                    Description = string.IsNullOrWhiteSpace(member.Responsibility) ? member.RoleName : member.Responsibility,
                    Instructions = BuildAgentInstructions(member, request.TeamAgent.TeamMode),
                    Kernel = kernel
                }));
        }

        return built;
    }

    private ResponseObserver CreateResponseObserver(
        TeamAgentMode mode,
        TeamAgentRuntimeDescriptor runtime,
        IReadOnlyList<BuiltAgent> builtAgents,
        Func<TeamAgentRunEvent, Task> onEventAsync,
        Func<TeamAgentMemberContribution, Task> onContributionAsync,
        CancellationToken cancellationToken)
        => new(
            mode,
            runtime,
            builtAgents.ToDictionary(item => item.Member.RoleName, StringComparer.OrdinalIgnoreCase),
            onEventAsync,
            onContributionAsync,
            _logger,
            cancellationToken);

    private static string BuildAgentInstructions(
        TeamAgentMemberItem member,
        TeamAgentMode mode)
    {
        var prefix = string.IsNullOrWhiteSpace(member.PromptPrefix) ? string.Empty : $"{member.PromptPrefix.Trim()}\n";
        var modeInstructions = mode switch
        {
            TeamAgentMode.GroupChat => "你是团队群聊中的固定成员。请基于当前线程上下文继续推进任务，不要重复前面成员已经确认的结论。",
            TeamAgentMode.Workflow => "你是顺序编排中的执行节点。请产出可直接被下一节点消费的明确结论。",
            TeamAgentMode.Handoff => "你处于接力编排中。请完成当前子任务，并在需要时把控制权交给下一位更合适的成员。",
            _ => "请根据输入完成当前任务。"
        };

        var capabilityText = member.CapabilityTags.Count == 0
            ? "无显式能力标签"
            : string.Join("、", member.CapabilityTags);

        return
            $"{prefix}角色：{member.RoleName}\n职责：{member.Responsibility ?? "未提供"}\n能力标签：{capabilityText}\n{modeInstructions}";
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

    private sealed class ResponseObserver
    {
        private readonly TeamAgentMode _mode;
        private readonly TeamAgentRuntimeDescriptor _runtime;
        private readonly IReadOnlyDictionary<string, BuiltAgent> _participantMap;
        private readonly Func<TeamAgentRunEvent, Task> _onEventAsync;
        private readonly Func<TeamAgentMemberContribution, Task> _onContributionAsync;
        private readonly ILogger _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly List<TeamAgentExecutionStep> _steps = [];
        private readonly List<TeamAgentMemberContribution> _contributions = [];
        private int _roundCounter;
        private string _currentInput = string.Empty;

        public ResponseObserver(
            TeamAgentMode mode,
            TeamAgentRuntimeDescriptor runtime,
            IReadOnlyDictionary<string, BuiltAgent> participantMap,
            Func<TeamAgentRunEvent, Task> onEventAsync,
            Func<TeamAgentMemberContribution, Task> onContributionAsync,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            _mode = mode;
            _runtime = runtime;
            _participantMap = participantMap;
            _onEventAsync = onEventAsync;
            _onContributionAsync = onContributionAsync;
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        public async ValueTask HandleResponseAsync(ChatMessageContent message)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(_currentInput))
            {
                _currentInput = string.Empty;
            }

            var authorName = message.AuthorName ?? string.Empty;
            if (!_participantMap.TryGetValue(authorName, out var participant))
            {
                _logger.LogDebug("编排返回了未映射成员: {AuthorName}", authorName);
                return;
            }

            _roundCounter++;
            var output = ExtractText(message);
            var startedAt = DateTime.UtcNow;
            var completedAt = DateTime.UtcNow;

            await _onEventAsync(new TeamAgentRunEvent("round.started", JsonSerializer.Serialize(new
            {
                round = _roundCounter,
                member = participant.Member.RoleName,
                runtime = _runtime.RuntimeKey
            }, JsonOptions)));

            var step = new TeamAgentExecutionStep(
                0,
                participant.Member.AgentId,
                participant.Agent.Name,
                participant.Member.RoleName,
                participant.Member.Alias,
                _currentInput,
                output,
                "completed",
                null,
                startedAt,
                completedAt);
            _steps.Add(step);

            var contribution = new TeamAgentMemberContribution(
                participant.Member.AgentId!.Value,
                participant.Agent.Name,
                participant.Member.RoleName,
                participant.Member.Alias,
                _currentInput,
                output,
                _roundCounter,
                startedAt,
                completedAt);
            _contributions.Add(contribution);

            await _onContributionAsync(contribution);
            await _onEventAsync(new TeamAgentRunEvent("member.message", JsonSerializer.Serialize(new
            {
                round = _roundCounter,
                member = participant.Member.RoleName,
                agentId = participant.Member.AgentId,
                agentName = participant.Agent.Name,
                content = output
            }, JsonOptions)));
            await _onEventAsync(new TeamAgentRunEvent("execution.step", JsonSerializer.Serialize(step, JsonOptions)));

            _currentInput = _mode == TeamAgentMode.GroupChat
                ? $"{_currentInput}\n\n[{participant.Member.RoleName}] {output}".Trim()
                : output;
        }

        public TeamAgentOrchestrationRuntimeResult BuildResult(string finalMessage)
            => new(finalMessage, _steps, _contributions, _runtime);

        public void InitializeInput(string input)
        {
            _currentInput = input;
        }
    }

    private sealed record BuiltAgent(
        TeamAgentMemberItem Member,
        DomainAgent Agent,
        ChatCompletionAgent RuntimeAgent);
}
