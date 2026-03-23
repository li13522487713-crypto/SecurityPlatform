using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class MultiAgentOrchestrationService : IMultiAgentOrchestrationService
{
    private readonly MultiAgentOrchestrationRepository _orchestrationRepository;
    private readonly MultiAgentExecutionRepository _executionRepository;
    private readonly AgentRepository _agentRepository;
    private readonly IAgentChatService _agentChatService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly MultiAgentExecutionTracker _executionTracker;
    private readonly ILogger<MultiAgentOrchestrationService> _logger;

    public MultiAgentOrchestrationService(
        MultiAgentOrchestrationRepository orchestrationRepository,
        MultiAgentExecutionRepository executionRepository,
        AgentRepository agentRepository,
        IAgentChatService agentChatService,
        IIdGeneratorAccessor idGeneratorAccessor,
        MultiAgentExecutionTracker executionTracker,
        ILogger<MultiAgentOrchestrationService> logger)
    {
        _orchestrationRepository = orchestrationRepository;
        _executionRepository = executionRepository;
        _agentRepository = agentRepository;
        _agentChatService = agentChatService;
        _idGeneratorAccessor = idGeneratorAccessor;
        _executionTracker = executionTracker;
        _logger = logger;
    }

    public async Task<PagedResult<MultiAgentOrchestrationListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _orchestrationRepository.GetPagedAsync(
            tenantId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        var list = items.Select(item =>
        {
            var members = DeserializeMembers(item.MembersJson);
            return new MultiAgentOrchestrationListItem(
                item.Id,
                item.Name,
                item.Description,
                item.Mode,
                item.Status,
                members.Count,
                item.CreatorUserId,
                item.CreatedAt,
                item.UpdatedAt);
        }).ToList();
        return new PagedResult<MultiAgentOrchestrationListItem>(list, total, pageIndex, pageSize);
    }

    public async Task<MultiAgentOrchestrationDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _orchestrationRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var members = DeserializeMembers(entity.MembersJson);
        return new MultiAgentOrchestrationDetail(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Mode,
            entity.Status,
            entity.CreatorUserId,
            entity.CreatedAt,
            entity.UpdatedAt,
            members);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long creatorUserId,
        MultiAgentOrchestrationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var members = NormalizeMembers(request.Members);
        await EnsureAgentsExistAsync(tenantId, members, cancellationToken);

        var entity = new MultiAgentOrchestration(
            tenantId,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.Mode,
            SerializeMembers(members),
            creatorUserId,
            _idGeneratorAccessor.NextId());
        await _orchestrationRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long id,
        MultiAgentOrchestrationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _orchestrationRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("编排不存在。", ErrorCodes.NotFound);

        var members = NormalizeMembers(request.Members);
        await EnsureAgentsExistAsync(tenantId, members, cancellationToken);

        entity.Update(
            request.Name.Trim(),
            request.Description?.Trim(),
            request.Mode,
            SerializeMembers(members));

        if (request.Status == MultiAgentOrchestrationStatus.Active)
        {
            entity.Activate();
        }
        else if (request.Status == MultiAgentOrchestrationStatus.Disabled)
        {
            entity.Disable();
        }

        await _orchestrationRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        if (await _orchestrationRepository.FindByIdAsync(tenantId, id, cancellationToken) is null)
        {
            return;
        }

        await _orchestrationRepository.DeleteAsync(tenantId, id, cancellationToken);
    }

    public async Task<MultiAgentExecutionResult> RunAsync(
        TenantId tenantId,
        long userId,
        long orchestrationId,
        MultiAgentRunRequest request,
        CancellationToken cancellationToken)
    {
        var orchestration = await LoadOrchestrationOrThrowAsync(tenantId, orchestrationId, cancellationToken);
        var execution = await CreateExecutionAsync(tenantId, orchestrationId, userId, request.Message, cancellationToken);
        var result = await ExecuteCoreAsync(
            tenantId,
            userId,
            orchestration,
            execution,
            request,
            null,
            cancellationToken);
        return result;
    }

    public async IAsyncEnumerable<MultiAgentStreamEvent> StreamRunAsync(
        TenantId tenantId,
        long userId,
        long orchestrationId,
        MultiAgentRunRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var orchestration = await LoadOrchestrationOrThrowAsync(tenantId, orchestrationId, cancellationToken);
        var execution = await CreateExecutionAsync(tenantId, orchestrationId, userId, request.Message, cancellationToken);
        var reader = _executionTracker.Create(execution.Id);

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteCoreAsync(
                    tenantId,
                    userId,
                    orchestration,
                    execution,
                    request,
                    async evt => await _executionTracker.PublishAsync(execution.Id, evt, CancellationToken.None),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "multi agent stream execute failed, executionId={ExecutionId}", execution.Id);
            }
            finally
            {
                _executionTracker.Complete(execution.Id);
            }
        }, CancellationToken.None);

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out var evt))
            {
                yield return evt;
            }
        }
    }

    public async Task<MultiAgentExecutionResult?> GetExecutionAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var entity = await _executionRepository.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return ToExecutionResult(entity, DeserializeSteps(entity.TraceJson));
    }

    private async Task<MultiAgentOrchestration> LoadOrchestrationOrThrowAsync(
        TenantId tenantId,
        long orchestrationId,
        CancellationToken cancellationToken)
    {
        return await _orchestrationRepository.FindByIdAsync(tenantId, orchestrationId, cancellationToken)
            ?? throw new BusinessException("编排不存在。", ErrorCodes.NotFound);
    }

    private async Task<MultiAgentExecution> CreateExecutionAsync(
        TenantId tenantId,
        long orchestrationId,
        long userId,
        string inputMessage,
        CancellationToken cancellationToken)
    {
        var execution = new MultiAgentExecution(
            tenantId,
            orchestrationId,
            userId,
            inputMessage,
            _idGeneratorAccessor.NextId());
        await _executionRepository.AddAsync(execution, cancellationToken);
        return execution;
    }

    private async Task<MultiAgentExecutionResult> ExecuteCoreAsync(
        TenantId tenantId,
        long userId,
        MultiAgentOrchestration orchestration,
        MultiAgentExecution execution,
        MultiAgentRunRequest request,
        Func<MultiAgentStreamEvent, Task>? emitAsync,
        CancellationToken cancellationToken)
    {
        execution.MarkRunning();
        await _executionRepository.UpdateAsync(execution, cancellationToken);

        if (emitAsync is not null)
        {
            await emitAsync(new MultiAgentStreamEvent("execution_start", JsonSerializer.Serialize(new
            {
                executionId = execution.Id,
                orchestrationId = orchestration.Id
            })));
        }

        var members = DeserializeMembers(orchestration.MembersJson)
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.SortOrder)
            .ToList();
        if (members.Count == 0)
        {
            throw new BusinessException("编排内至少需要一个启用成员。", ErrorCodes.ValidationError);
        }

        await EnsureAgentsExistAsync(tenantId, members, cancellationToken);
        var agentIds = members.Select(x => x.AgentId).Distinct().ToList();
        var agents = await _agentRepository.QueryByIdsAsync(tenantId, agentIds, cancellationToken);
        var agentNameMap = agents.ToDictionary(x => x.Id, x => x.Name);
        var steps = orchestration.Mode == MultiAgentOrchestrationMode.Sequential
            ? await ExecuteSequentialAsync(tenantId, userId, members, request, agentNameMap, emitAsync, cancellationToken)
            : await ExecuteParallelAsync(tenantId, userId, members, request, agentNameMap, emitAsync, cancellationToken);

        var failedStep = steps.FirstOrDefault(x => x.Status == ExecutionStatus.Failed);
        if (failedStep is null)
        {
            var finalOutput = orchestration.Mode == MultiAgentOrchestrationMode.Sequential
                ? steps.Last().OutputMessage ?? string.Empty
                : string.Join(Environment.NewLine + Environment.NewLine, steps.Select(x =>
                    $"[{x.Alias ?? x.AgentName}] {x.OutputMessage}"));
            execution.MarkCompleted(finalOutput, SerializeSteps(steps));
        }
        else
        {
            execution.MarkFailed(failedStep.ErrorMessage ?? "执行失败", SerializeSteps(steps));
        }

        await _executionRepository.UpdateAsync(execution, cancellationToken);

        var result = ToExecutionResult(execution, steps);
        if (emitAsync is not null)
        {
            await emitAsync(new MultiAgentStreamEvent("execution_finish", JsonSerializer.Serialize(new
            {
                executionId = execution.Id,
                status = result.Status,
                outputMessage = result.OutputMessage,
                errorMessage = result.ErrorMessage
            })));
        }

        return result;
    }

    private async Task<List<MultiAgentExecutionStep>> ExecuteSequentialAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<MultiAgentMemberItem> members,
        MultiAgentRunRequest request,
        IReadOnlyDictionary<long, string> agentNameMap,
        Func<MultiAgentStreamEvent, Task>? emitAsync,
        CancellationToken cancellationToken)
    {
        var steps = new List<MultiAgentExecutionStep>(members.Count);
        var currentMessage = request.Message;
        foreach (var member in members)
        {
            var step = await ExecuteMemberAsync(
                tenantId,
                userId,
                member,
                currentMessage,
                request.EnableRag,
                agentNameMap,
                emitAsync,
                cancellationToken);
            steps.Add(step);
            if (step.Status == ExecutionStatus.Failed)
            {
                break;
            }

            currentMessage = step.OutputMessage ?? currentMessage;
        }

        return steps;
    }

    private async Task<List<MultiAgentExecutionStep>> ExecuteParallelAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<MultiAgentMemberItem> members,
        MultiAgentRunRequest request,
        IReadOnlyDictionary<long, string> agentNameMap,
        Func<MultiAgentStreamEvent, Task>? emitAsync,
        CancellationToken cancellationToken)
    {
        var tasks = members.Select(member =>
            ExecuteMemberAsync(
                tenantId,
                userId,
                member,
                request.Message,
                request.EnableRag,
                agentNameMap,
                emitAsync,
                cancellationToken)).ToList();
        var steps = await Task.WhenAll(tasks);
        return steps.OrderBy(x => x.StartedAt).ToList();
    }

    private async Task<MultiAgentExecutionStep> ExecuteMemberAsync(
        TenantId tenantId,
        long userId,
        MultiAgentMemberItem member,
        string inputMessage,
        bool? enableRag,
        IReadOnlyDictionary<long, string> agentNameMap,
        Func<MultiAgentStreamEvent, Task>? emitAsync,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var agentName = agentNameMap.TryGetValue(member.AgentId, out var mapped) ? mapped : $"Agent-{member.AgentId}";
        if (emitAsync is not null)
        {
            await emitAsync(new MultiAgentStreamEvent("agent_start", JsonSerializer.Serialize(new
            {
                member.AgentId,
                member.Alias,
                startedAt
            })));
        }

        try
        {
            var prompt = BuildPrompt(member.PromptPrefix, inputMessage);
            var response = await _agentChatService.ChatAsync(
                tenantId,
                userId,
                member.AgentId,
                new AgentChatRequest(null, prompt, enableRag),
                cancellationToken);
            var completedAt = DateTime.UtcNow;
            var step = new MultiAgentExecutionStep(
                member.AgentId,
                agentName,
                member.Alias,
                inputMessage,
                response.Content,
                ExecutionStatus.Completed,
                null,
                startedAt,
                completedAt);

            if (emitAsync is not null)
            {
                await emitAsync(new MultiAgentStreamEvent("agent_finish", JsonSerializer.Serialize(step)));
            }

            return step;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "multi agent member failed, agentId={AgentId}", member.AgentId);
            var completedAt = DateTime.UtcNow;
            var step = new MultiAgentExecutionStep(
                member.AgentId,
                agentName,
                member.Alias,
                inputMessage,
                null,
                ExecutionStatus.Failed,
                ex.Message,
                startedAt,
                completedAt);

            if (emitAsync is not null)
            {
                await emitAsync(new MultiAgentStreamEvent("agent_finish", JsonSerializer.Serialize(step)));
            }

            return step;
        }
    }

    private static string BuildPrompt(string? promptPrefix, string inputMessage)
    {
        if (string.IsNullOrWhiteSpace(promptPrefix))
        {
            return inputMessage;
        }

        return $"{promptPrefix.Trim()}{Environment.NewLine}{Environment.NewLine}{inputMessage}";
    }

    private async Task EnsureAgentsExistAsync(
        TenantId tenantId,
        IReadOnlyList<MultiAgentMemberItem> members,
        CancellationToken cancellationToken)
    {
        var agentIds = members.Select(x => x.AgentId).Distinct().ToList();
        var agents = await _agentRepository.QueryByIdsAsync(tenantId, agentIds, cancellationToken);
        if (agents.Count != agentIds.Count)
        {
            throw new BusinessException("编排成员中存在无效 Agent。", ErrorCodes.ValidationError);
        }
    }

    private static List<MultiAgentMemberItem> NormalizeMembers(IReadOnlyList<MultiAgentMemberInput> members)
    {
        var dedup = new Dictionary<long, MultiAgentMemberItem>();
        foreach (var member in members.OrderBy(x => x.SortOrder))
        {
            if (dedup.ContainsKey(member.AgentId))
            {
                continue;
            }

            dedup[member.AgentId] = new MultiAgentMemberItem(
                member.AgentId,
                string.IsNullOrWhiteSpace(member.Alias) ? null : member.Alias.Trim(),
                member.SortOrder,
                member.IsEnabled,
                string.IsNullOrWhiteSpace(member.PromptPrefix) ? null : member.PromptPrefix.Trim());
        }

        return dedup.Values
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AgentId)
            .ToList();
    }

    private static string SerializeMembers(IReadOnlyList<MultiAgentMemberItem> members)
        => JsonSerializer.Serialize(members);

    private static IReadOnlyList<MultiAgentMemberItem> DeserializeMembers(string? membersJson)
    {
        if (string.IsNullOrWhiteSpace(membersJson))
        {
            return Array.Empty<MultiAgentMemberItem>();
        }

        var members = JsonSerializer.Deserialize<List<MultiAgentMemberItem>>(membersJson);
        return members is null ? Array.Empty<MultiAgentMemberItem>() : members;
    }

    private static string SerializeSteps(IReadOnlyList<MultiAgentExecutionStep> steps)
        => JsonSerializer.Serialize(steps);

    private static IReadOnlyList<MultiAgentExecutionStep> DeserializeSteps(string? traceJson)
    {
        if (string.IsNullOrWhiteSpace(traceJson))
        {
            return Array.Empty<MultiAgentExecutionStep>();
        }

        var steps = JsonSerializer.Deserialize<List<MultiAgentExecutionStep>>(traceJson);
        return steps is null ? Array.Empty<MultiAgentExecutionStep>() : steps;
    }

    private static MultiAgentExecutionResult ToExecutionResult(
        MultiAgentExecution execution,
        IReadOnlyList<MultiAgentExecutionStep> steps)
    {
        var completedAt = execution.CompletedAt == DateTime.UnixEpoch
            ? null
            : execution.CompletedAt;
        return new MultiAgentExecutionResult(
            execution.Id,
            execution.OrchestrationId,
            execution.Status,
            execution.OutputMessage,
            execution.ErrorMessage,
            steps,
            execution.StartedAt,
            completedAt);
    }
}
