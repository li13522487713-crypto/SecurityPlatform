using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class EvaluationService : IEvaluationService
{
    private readonly EvaluationDatasetRepository _datasetRepository;
    private readonly EvaluationCaseRepository _caseRepository;
    private readonly EvaluationTaskRepository _taskRepository;
    private readonly EvaluationResultRepository _resultRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly bool _runInlineWhenNoHangfireServer;

    public EvaluationService(
        EvaluationDatasetRepository datasetRepository,
        EvaluationCaseRepository caseRepository,
        EvaluationTaskRepository taskRepository,
        EvaluationResultRepository resultRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IBackgroundJobClient backgroundJobClient,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _datasetRepository = datasetRepository;
        _caseRepository = caseRepository;
        _taskRepository = taskRepository;
        _resultRepository = resultRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _backgroundJobClient = backgroundJobClient;
        _serviceScopeFactory = serviceScopeFactory;
        var runHangfireServer = configuration.GetValue("Hangfire:RunServer", !hostEnvironment.IsDevelopment());
        _runInlineWhenNoHangfireServer = !runHangfireServer;
    }

    public async Task<long> CreateDatasetAsync(
        TenantId tenantId,
        long userId,
        EvaluationDatasetCreateRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = new EvaluationDataset(
            tenantId,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.Scene?.Trim(),
            userId,
            _idGeneratorAccessor.NextId());
        await _datasetRepository.AddAsync(dataset, cancellationToken);
        return dataset.Id;
    }

    public async Task<PagedResult<EvaluationDatasetDto>> GetDatasetsAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _datasetRepository.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        var counts = await _caseRepository.CountByDatasetIdsAsync(
            tenantId,
            items.Select(x => x.Id).ToList(),
            cancellationToken);
        var list = items.Select(item => new EvaluationDatasetDto(
            item.Id,
            item.Name,
            item.Description,
            item.Scene,
            counts.TryGetValue(item.Id, out var count) ? count : 0,
            item.CreatedByUserId,
            item.CreatedAt,
            item.UpdatedAt)).ToList();
        return new PagedResult<EvaluationDatasetDto>(list, total, pageIndex, pageSize);
    }

    public async Task<long> CreateCaseAsync(
        TenantId tenantId,
        long datasetId,
        EvaluationCaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _datasetRepository.FindByIdAsync(tenantId, datasetId, cancellationToken)
            ?? throw new BusinessException("评测数据集不存在。", ErrorCodes.NotFound);
        _ = dataset;
        var tagsJson = JsonSerializer.Serialize(request.Tags ?? []);
        var evaluationCase = new EvaluationCase(
            tenantId,
            datasetId,
            request.Input.Trim(),
            request.ExpectedOutput?.Trim(),
            request.ReferenceOutput?.Trim(),
            tagsJson,
            _idGeneratorAccessor.NextId());
        await _caseRepository.AddAsync(evaluationCase, cancellationToken);
        return evaluationCase.Id;
    }

    public async Task<IReadOnlyList<EvaluationCaseDto>> GetCasesAsync(
        TenantId tenantId,
        long datasetId,
        CancellationToken cancellationToken)
    {
        var items = await _caseRepository.GetByDatasetAsync(tenantId, datasetId, cancellationToken);
        return items.Select(MapCase).ToList();
    }

    public async Task<long> CreateTaskAsync(
        TenantId tenantId,
        long userId,
        EvaluationTaskCreateRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _datasetRepository.FindByIdAsync(tenantId, request.DatasetId, cancellationToken)
            ?? throw new BusinessException("评测数据集不存在。", ErrorCodes.NotFound);
        _ = dataset;

        var cases = await _caseRepository.GetByDatasetAsync(tenantId, request.DatasetId, cancellationToken);
        if (cases.Count == 0)
        {
            throw new BusinessException("评测数据集用例为空。", ErrorCodes.ValidationError);
        }

        var task = new EvaluationTask(
            tenantId,
            request.Name.Trim(),
            request.DatasetId,
            request.AgentId,
            userId,
            _idGeneratorAccessor.NextId());
        await _taskRepository.AddAsync(task, cancellationToken);

        _backgroundJobClient.Enqueue<IEvaluationJobService>(job => job.ExecuteTaskAsync(task.Id));
        if (_runInlineWhenNoHangfireServer)
        {
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<IEvaluationJobService>();
                await job.ExecuteTaskAsync(task.Id);
            });
        }
        return task.Id;
    }

    public async Task<PagedResult<EvaluationTaskDto>> GetTasksAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _taskRepository.GetPagedAsync(tenantId, pageIndex, pageSize, cancellationToken);
        var list = items.Select(MapTask).ToList();
        return new PagedResult<EvaluationTaskDto>(list, total, pageIndex, pageSize);
    }

    public async Task<EvaluationTaskDto?> GetTaskAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        return task is null ? null : MapTask(task);
    }

    public async Task<IReadOnlyList<EvaluationResultDto>> GetTaskResultsAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var items = await _resultRepository.GetByTaskAsync(tenantId, taskId, cancellationToken);
        return items.Select(item => new EvaluationResultDto(
            item.Id,
            item.TaskId,
            item.CaseId,
            item.ActualOutput,
            item.Score,
            item.JudgeReason,
            item.Status,
            item.CreatedAt)).ToList();
    }

    public async Task<EvaluationComparisonResult> CompareTasksAsync(
        TenantId tenantId,
        long leftTaskId,
        long rightTaskId,
        CancellationToken cancellationToken)
    {
        var left = await _taskRepository.FindByIdAsync(tenantId, leftTaskId, cancellationToken)
            ?? throw new BusinessException("左侧评测任务不存在。", ErrorCodes.NotFound);
        var right = await _taskRepository.FindByIdAsync(tenantId, rightTaskId, cancellationToken)
            ?? throw new BusinessException("右侧评测任务不存在。", ErrorCodes.NotFound);
        var delta = Math.Round(left.Score - right.Score, 4);
        var winner = delta == 0 ? "draw" : delta > 0 ? "left" : "right";
        return new EvaluationComparisonResult(
            left.Id,
            left.Score,
            right.Id,
            right.Score,
            delta,
            winner);
    }

    private static EvaluationTaskDto MapTask(EvaluationTask task)
    {
        return new EvaluationTaskDto(
            task.Id,
            task.Name,
            task.DatasetId,
            task.AgentId,
            task.Status,
            task.TotalCases,
            task.CompletedCases,
            task.Score,
            task.ErrorMessage,
            task.CreatedAt,
            task.UpdatedAt,
            task.StartedAt == DateTime.UnixEpoch ? null : task.StartedAt,
            task.CompletedAt == DateTime.UnixEpoch ? null : task.CompletedAt);
    }

    private static EvaluationCaseDto MapCase(EvaluationCase item)
    {
        var tags = JsonSerializer.Deserialize<List<string>>(item.TagsJson) ?? [];
        return new EvaluationCaseDto(
            item.Id,
            item.DatasetId,
            item.Input,
            item.ExpectedOutput,
            item.ReferenceOutput,
            tags,
            item.CreatedAt,
            item.UpdatedAt);
    }
}
