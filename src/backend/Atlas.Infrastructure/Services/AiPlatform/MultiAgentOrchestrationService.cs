using System.Text.Json;
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

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class MultiAgentOrchestrationService : IMultiAgentOrchestrationService
{
    private readonly MultiAgentOrchestrationRepository _orchestrationRepository;
    private readonly MultiAgentExecutionRepository _executionRepository;
    private readonly AgentRepository _agentRepository;
    private readonly ITeamAgentOrchestrationRuntime _teamRuntime;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly MultiAgentExecutionTracker _executionTracker;
    private readonly ILogger<MultiAgentOrchestrationService> _logger;

    public MultiAgentOrchestrationService(
        MultiAgentOrchestrationRepository orchestrationRepository,
        MultiAgentExecutionRepository executionRepository,
        AgentRepository agentRepository,
        ITeamAgentOrchestrationRuntime teamRuntime,
        IIdGeneratorAccessor idGeneratorAccessor,
        MultiAgentExecutionTracker executionTracker,
        ILogger<MultiAgentOrchestrationService> logger)
    {
        _orchestrationRepository = orchestrationRepository;
        _executionRepository = executionRepository;
        _agentRepository = agentRepository;
        _teamRuntime = teamRuntime;
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
        return await ExecuteCoreAsync(tenantId, userId, orchestration, execution, request, null, cancellationToken);
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
        => await _orchestrationRepository.FindByIdAsync(tenantId, orchestrationId, cancellationToken)
            ?? throw new BusinessException("编排不存在。", ErrorCodes.NotFound);

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

        var legacyMembers = DeserializeMembers(orchestration.MembersJson)
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.SortOrder)
            .ToList();
        if (legacyMembers.Count == 0)
        {
            throw new BusinessException("编排内至少需要一个启用成员。", ErrorCodes.ValidationError);
        }

        await EnsureAgentsExistAsync(tenantId, legacyMembers, cancellationToken);
        var agents = await _agentRepository.QueryByIdsAsync(
            tenantId,
            legacyMembers.Select(item => item.AgentId).Distinct().ToList(),
            cancellationToken);
        var agentMap = agents.ToDictionary(item => item.Id);

        var teamMembers = legacyMembers
            .Select((member, index) =>
            {
                var agent = agentMap[member.AgentId];
                var roleName = string.IsNullOrWhiteSpace(member.Alias) ? agent.Name : member.Alias!;
                return new TeamAgentMemberItem(
                    member.AgentId,
                    roleName,
                    orchestration.Mode == MultiAgentOrchestrationMode.Sequential ? "顺序旧编排成员" : "兼容旧编排成员",
                    member.Alias,
                    index,
                    member.IsEnabled,
                    member.PromptPrefix,
                    [],
                    "bound");
            })
            .ToList();

        var runtimeTeam = new TeamAgent(
            tenantId,
            orchestration.Name,
            orchestration.Description,
            orchestration.Mode == MultiAgentOrchestrationMode.Sequential ? TeamAgentMode.Workflow : TeamAgentMode.GroupChat,
            "[]",
            "chat",
            "[]",
            "[]",
            "{}",
            userId,
            orchestration.Id);
        var runtimePattern = orchestration.Mode == MultiAgentOrchestrationMode.Parallel
            ? TeamAgentRuntimePattern.Concurrent
            : TeamAgentRuntimePattern.Default;

        TeamAgentOrchestrationRuntimeResult runtimeResult;
        try
        {
            runtimeResult = await _teamRuntime.ExecuteAsync(
                new TeamAgentOrchestrationRuntimeRequest(
                    tenantId,
                    userId,
                    runtimeTeam,
                    teamMembers,
                    request.Message,
                    request.EnableRag,
                    null,
                    runtimePattern),
                evt => emitAsync is null
                    ? Task.CompletedTask
                    : emitAsync(new MultiAgentStreamEvent(evt.EventType, evt.Data)),
                _ => Task.CompletedTask,
                cancellationToken);
        }
        catch (TeamAgentOrchestrationExecutionException ex)
        {
            var failedSteps = ex.Steps.Select(MapStep).ToList();
            execution.MarkFailed(ex.Message, SerializeSteps(failedSteps));
            await _executionRepository.UpdateAsync(execution, cancellationToken);
            return ToExecutionResult(execution, failedSteps);
        }

        var steps = runtimeResult.Steps.Select(MapStep).ToList();
        execution.MarkCompleted(runtimeResult.FinalMessage, SerializeSteps(steps));
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

    private static MultiAgentExecutionStep MapStep(TeamAgentExecutionStep step)
        => new(
            step.AgentId ?? 0,
            step.AgentName,
            step.Alias,
            step.InputMessage,
            step.OutputMessage,
            string.Equals(step.Status, "completed", StringComparison.OrdinalIgnoreCase)
                ? ExecutionStatus.Completed
                : ExecutionStatus.Failed,
            step.ErrorMessage,
            step.StartedAt,
            step.CompletedAt);

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
