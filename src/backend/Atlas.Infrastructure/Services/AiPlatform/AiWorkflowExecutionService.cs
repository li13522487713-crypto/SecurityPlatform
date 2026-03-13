using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.DSL.Interface;
using Atlas.WorkflowCore.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiWorkflowExecutionService : IAiWorkflowExecutionService
{
    private readonly AiWorkflowDefinitionRepository _repository;
    private readonly IWorkflowHost _workflowHost;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly IDefinitionLoader _definitionLoader;
    private readonly IPersistenceProvider _persistenceProvider;

    public AiWorkflowExecutionService(
        AiWorkflowDefinitionRepository repository,
        IWorkflowHost workflowHost,
        IWorkflowRegistry workflowRegistry,
        IDefinitionLoader definitionLoader,
        IPersistenceProvider persistenceProvider)
    {
        _repository = repository;
        _workflowHost = workflowHost;
        _workflowRegistry = workflowRegistry;
        _definitionLoader = definitionLoader;
        _persistenceProvider = persistenceProvider;
    }

    public async Task<AiWorkflowExecutionRunResult> RunAsync(
        TenantId tenantId,
        long workflowDefinitionId,
        AiWorkflowExecutionRunRequest request,
        CancellationToken cancellationToken)
    {
        var definitionEntity = await _repository.FindByIdAsync(tenantId, workflowDefinitionId, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);

        var definition = _definitionLoader.LoadDefinitionFromJson(definitionEntity.DefinitionJson);
        definition.Id = $"aiwf-{definitionEntity.Id}";
        definition.Version = Math.Max(1, definitionEntity.PublishVersion);
        _workflowRegistry.RegisterWorkflow(definition);

        var inputs = request.Inputs ?? new Dictionary<string, object?>();
        inputs["tenantId"] = tenantId.Value.ToString();
        inputs["workflowDefinitionId"] = workflowDefinitionId;

        var executionId = await _workflowHost.StartWorkflowAsync(
            definition.Id,
            definition.Version,
            inputs,
            reference: $"tenant:{tenantId.Value}:aiwf:{workflowDefinitionId}",
            cancellationToken);

        return new AiWorkflowExecutionRunResult(executionId);
    }

    public async Task CancelAsync(TenantId tenantId, string executionId, CancellationToken cancellationToken)
    {
        _ = tenantId;
        await _workflowHost.TerminateWorkflowAsync(executionId, cancellationToken);
    }

    public async Task<AiWorkflowExecutionProgressDto?> GetProgressAsync(
        TenantId tenantId,
        string executionId,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        var workflow = await _persistenceProvider.GetWorkflowAsync(executionId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        return new AiWorkflowExecutionProgressDto(
            workflow.Id,
            workflow.WorkflowDefinitionId,
            workflow.Version,
            workflow.Status.ToString(),
            workflow.CreateTime,
            workflow.CompleteTime);
    }

    public async Task<IReadOnlyList<AiWorkflowNodeHistoryItem>> GetNodeHistoryAsync(
        TenantId tenantId,
        string executionId,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        var workflow = await _persistenceProvider.GetWorkflowAsync(executionId, cancellationToken);
        if (workflow is null)
        {
            return [];
        }

        return workflow.ExecutionPointers
            .OrderBy(x => x.StartTime ?? DateTime.MaxValue)
            .ThenBy(x => x.StepId)
            .Select(pointer => new AiWorkflowNodeHistoryItem(
                pointer.Id,
                pointer.StepId,
                pointer.StepName,
                pointer.Status.ToString(),
                pointer.StartTime,
                pointer.EndTime,
                pointer.Outcome))
            .ToList();
    }
}
