using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiAppService : IAiAppService
{
    private readonly AiAppRepository _appRepository;
    private readonly AiAppPublishRecordRepository _publishRecordRepository;
    private readonly AiAppResourceCopyTaskRepository _copyTaskRepository;
    private readonly AiAppConversationTemplateRepository _conversationTemplateRepository;
    private readonly IBackgroundWorkQueue _backgroundWorkQueue;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AiAppService> _logger;

    public AiAppService(
        AiAppRepository appRepository,
        AiAppPublishRecordRepository publishRecordRepository,
        AiAppResourceCopyTaskRepository copyTaskRepository,
        AiAppConversationTemplateRepository conversationTemplateRepository,
        IBackgroundWorkQueue backgroundWorkQueue,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        ILogger<AiAppService> logger)
    {
        _appRepository = appRepository;
        _publishRecordRepository = publishRecordRepository;
        _copyTaskRepository = copyTaskRepository;
        _conversationTemplateRepository = conversationTemplateRepository;
        _backgroundWorkQueue = backgroundWorkQueue;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<AiAppListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _appRepository.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        return new PagedResult<AiAppListItem>(
            items.Select(MapListItem).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<AiAppDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var app = await _appRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var records = await _publishRecordRepository.GetByAppIdAsync(tenantId, id, top: 10, cancellationToken);
        return MapDetail(app, records);
    }

    public async Task<AiAppBuilderConfig> GetBuilderConfigAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var app = await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        return ParseBuilderConfig(app.UiBuilderSchemaJson, app.WorkflowId);
    }

    public async Task<IReadOnlyList<AiAppPublishRecordItem>> GetPublishRecordsAsync(
        TenantId tenantId,
        long id,
        int top,
        CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        var records = await _publishRecordRepository.GetByAppIdAsync(
            tenantId,
            id,
            top <= 0 ? 20 : top,
            cancellationToken);
        return records.Select(MapPublishRecord).ToArray();
    }

    public async Task<IReadOnlyList<AiAppConversationTemplateListItem>> GetConversationTemplatesAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        var templates = await _conversationTemplateRepository.GetByAppIdAsync(tenantId, id, cancellationToken);
        return templates.Select(MapConversationTemplate).ToArray();
    }

    public async Task<long> CreateConversationTemplateAsync(
        TenantId tenantId,
        long id,
        long userId,
        AiAppConversationTemplateCreateRequest request,
        CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        _ = userId;
        var template = new AiAppConversationTemplate(
            tenantId,
            id,
            request.Name.Trim(),
            ParseCreateMethod(request.CreateMethod),
            request.SourceWorkflowId,
            request.ConnectorId,
            request.IsDefault,
            request.ConfigJson,
            _idGeneratorAccessor.NextId());
        await _conversationTemplateRepository.AddAsync(template, cancellationToken);
        return template.Id;
    }

    public async Task UpdateConversationTemplateAsync(
        TenantId tenantId,
        long id,
        long templateId,
        long userId,
        AiAppConversationTemplateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        _ = userId;
        var template = await _conversationTemplateRepository.FindByIdAsync(tenantId, templateId, cancellationToken)
            ?? throw new BusinessException("应用会话模板不存在。", ErrorCodes.NotFound);
        if (template.AppId != id)
        {
            throw new BusinessException("应用会话模板不属于当前应用。", ErrorCodes.ValidationError);
        }

        template.Update(
            request.Name.Trim(),
            request.SourceWorkflowId,
            request.ConnectorId,
            request.IsDefault,
            request.ConfigJson);
        await _conversationTemplateRepository.UpdateAsync(template, cancellationToken);
    }

    public async Task DeleteConversationTemplateAsync(
        TenantId tenantId,
        long id,
        long templateId,
        long userId,
        CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        _ = userId;
        var template = await _conversationTemplateRepository.FindByIdAsync(tenantId, templateId, cancellationToken)
            ?? throw new BusinessException("应用会话模板不存在。", ErrorCodes.NotFound);
        if (template.AppId != id)
        {
            throw new BusinessException("应用会话模板不属于当前应用。", ErrorCodes.ValidationError);
        }

        await _conversationTemplateRepository.DeleteAsync(template, cancellationToken);
    }

    public async Task<long> CreateAsync(TenantId tenantId, AiAppCreateRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (await _appRepository.ExistsByNameAsync(tenantId, normalizedName, excludeId: null, cancellationToken))
        {
            throw new BusinessException("AI 应用名称已存在。", ErrorCodes.ValidationError);
        }

        var app = new AiApp(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.Icon?.Trim(),
            request.AgentId,
            request.WorkflowId,
            request.PromptTemplateId,
            _idGeneratorAccessor.NextId(),
            request.WorkspaceId);
        await _appRepository.AddAsync(app, cancellationToken);
        return app.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, AiAppUpdateRequest request, CancellationToken cancellationToken)
    {
        var app = await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        var normalizedName = request.Name.Trim();
        if (await _appRepository.ExistsByNameAsync(tenantId, normalizedName, id, cancellationToken))
        {
            throw new BusinessException("AI 应用名称已存在。", ErrorCodes.ValidationError);
        }

        app.Update(
            normalizedName,
            request.Description?.Trim(),
            request.Icon?.Trim(),
            request.AgentId,
            request.WorkflowId,
            request.PromptTemplateId,
            workspaceId: request.WorkspaceId);
        await _appRepository.UpdateAsync(app, cancellationToken);
    }

    public async Task UpdateBuilderConfigAsync(
        TenantId tenantId,
        long id,
        AiAppBuilderConfig request,
        CancellationToken cancellationToken)
    {
        var app = await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        ValidateLayoutMode(request.LayoutMode);

        var normalizedConfig = NormalizeBuilderConfig(request, app.WorkflowId);
        var nextWorkflowId = ParseNullableLong(normalizedConfig.BoundWorkflowId);
        app.Update(
            app.Name,
            app.Description,
            app.Icon,
            app.AgentId,
            nextWorkflowId,
            app.PromptTemplateId,
            primaryWorkflowId: nextWorkflowId ?? app.PrimaryWorkflowId,
            entryConversationTemplateId: app.EntryConversationTemplateId,
            uiBuilderSchemaJson: JsonSerializer.Serialize(normalizedConfig),
            workspaceLayoutJson: app.WorkspaceLayoutJson,
            publishedConnectorConfigJson: app.PublishedConnectorConfigJson,
            lastPublishedSnapshotJson: app.LastPublishedSnapshotJson);

        await _appRepository.UpdateAsync(app, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _copyTaskRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
            await _conversationTemplateRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
            await _publishRecordRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
            await _appRepository.DeleteAsync(tenantId, id, cancellationToken);
        }, cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId,
        long id,
        long publisherUserId,
        AiAppPublishRequest request,
        CancellationToken cancellationToken)
    {
        var app = await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        app.Publish();
        var record = new AiAppPublishRecord(
            tenantId,
            id,
            $"v{app.PublishVersion}",
            request.ReleaseNote?.Trim(),
            publisherUserId,
            _idGeneratorAccessor.NextId());

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _appRepository.UpdateAsync(app, cancellationToken);
            await _publishRecordRepository.AddAsync(record, cancellationToken);
        }, cancellationToken);
    }

    public async Task<AiAppVersionCheckResult> CheckVersionAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var app = await GetAppOrThrowAsync(tenantId, id, cancellationToken);
        var latestRecord = (await _publishRecordRepository.GetByAppIdAsync(tenantId, id, top: 1, cancellationToken)).FirstOrDefault();
        return new AiAppVersionCheckResult(
            app.Id,
            app.PublishVersion,
            latestRecord?.Version,
            latestRecord?.CreatedAt);
    }

    public async Task<long> SubmitResourceCopyAsync(
        TenantId tenantId,
        long appId,
        AiAppResourceCopyRequest request,
        CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, appId, cancellationToken);
        var source = await _appRepository.FindByIdAsync(tenantId, request.SourceAppId, cancellationToken)
            ?? throw new BusinessException("源应用不存在。", ErrorCodes.NotFound);
        if (source.Id == appId)
        {
            throw new BusinessException("不能从自身复制资源。", ErrorCodes.ValidationError);
        }

        var task = new AiAppResourceCopyTask(tenantId, appId, request.SourceAppId, _idGeneratorAccessor.NextId());
        await _copyTaskRepository.AddAsync(task, cancellationToken);

        _backgroundWorkQueue.Enqueue((sp, ct) => ProcessResourceCopyAsync(sp, tenantId, appId, task.Id, ct));
        return task.Id;
    }

    public async Task<AiAppResourceCopyTaskProgress?> GetLatestResourceCopyProgressAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        await GetAppOrThrowAsync(tenantId, appId, cancellationToken);
        var task = await _copyTaskRepository.GetLatestAsync(tenantId, appId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        return MapCopyTask(task);
    }

    private async Task ProcessResourceCopyAsync(
        IServiceProvider serviceProvider,
        TenantId tenantId,
        long appId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var taskRepository = serviceProvider.GetRequiredService<AiAppResourceCopyTaskRepository>();
        var task = await taskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        try
        {
            task.MarkRunning();
            await taskRepository.UpdateAsync(task, cancellationToken);

            // 当前版本仅记录资源复制任务状态，后续阶段可替换为真实资源复制管线。
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            task.MarkCompleted(totalItems: 3, copiedItems: 3);
            await taskRepository.UpdateAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI 应用资源复制任务失败。taskId={TaskId}", taskId);
            task.MarkFailed(ex.Message);
            await taskRepository.UpdateAsync(task, cancellationToken);
        }
    }

    private static AiAppBuilderConfig ParseBuilderConfig(string? uiBuilderSchemaJson, long? fallbackWorkflowId)
    {
        if (string.IsNullOrWhiteSpace(uiBuilderSchemaJson))
        {
            return CreateDefaultBuilderConfig(fallbackWorkflowId);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AiAppBuilderConfig>(uiBuilderSchemaJson);
            if (parsed is null)
            {
                return CreateDefaultBuilderConfig(fallbackWorkflowId);
            }

            return NormalizeBuilderConfig(parsed, fallbackWorkflowId);
        }
        catch (JsonException)
        {
            return CreateDefaultBuilderConfig(fallbackWorkflowId);
        }
    }

    private static AiAppBuilderConfig NormalizeBuilderConfig(AiAppBuilderConfig source, long? fallbackWorkflowId)
    {
        var layoutMode = string.IsNullOrWhiteSpace(source.LayoutMode) ? "form" : source.LayoutMode.Trim().ToLowerInvariant();
        if (layoutMode is not ("form" or "chat" or "hybrid"))
        {
            layoutMode = "form";
        }

        var boundWorkflowId = string.IsNullOrWhiteSpace(source.BoundWorkflowId)
            ? fallbackWorkflowId?.ToString(CultureInfo.InvariantCulture)
            : source.BoundWorkflowId.Trim();

        var normalizedInputs = (source.Inputs ?? [])
            .Select((item, index) => new AiAppBuilderInputComponent(
                string.IsNullOrWhiteSpace(item.Id) ? $"input-{index + 1}" : item.Id.Trim(),
                string.IsNullOrWhiteSpace(item.Type) ? "text" : item.Type.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(item.Label) ? $"Input {index + 1}" : item.Label.Trim(),
                item.VariableKey?.Trim() ?? string.Empty,
                item.Required,
                item.DefaultValue,
                item.Options?
                    .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                    .Select(option => new AiAppBuilderConfigOption(
                        option.Label?.Trim() ?? option.Value.Trim(),
                        option.Value.Trim()))
                    .ToArray()))
            .ToArray();
        var normalizedOutputs = (source.Outputs ?? [])
            .Select((item, index) => new AiAppBuilderOutputComponent(
                string.IsNullOrWhiteSpace(item.Id) ? $"output-{index + 1}" : item.Id.Trim(),
                string.IsNullOrWhiteSpace(item.Type) ? "text" : item.Type.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(item.Label) ? $"Output {index + 1}" : item.Label.Trim(),
                item.SourceExpression?.Trim() ?? string.Empty))
            .ToArray();

        return new AiAppBuilderConfig(normalizedInputs, normalizedOutputs, boundWorkflowId, layoutMode);
    }

    private static AiAppBuilderConfig CreateDefaultBuilderConfig(long? fallbackWorkflowId)
    {
        return new AiAppBuilderConfig(
            Array.Empty<AiAppBuilderInputComponent>(),
            Array.Empty<AiAppBuilderOutputComponent>(),
            fallbackWorkflowId?.ToString(CultureInfo.InvariantCulture),
            "form");
    }

    private static long? ParseNullableLong(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return long.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0
                ? parsed
                : null;
    }

    private static void ValidateLayoutMode(string? layoutMode)
    {
        var normalized = layoutMode?.Trim().ToLowerInvariant();
        if (normalized is "form" or "chat" or "hybrid")
        {
            return;
        }

        throw new BusinessException("应用布局模式无效。", ErrorCodes.ValidationError);
    }

    private async Task<AiApp> GetAppOrThrowAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        return await _appRepository.FindByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException("AI 应用不存在。", ErrorCodes.NotFound);
    }

    private static AiAppListItem MapListItem(AiApp app)
        => new(
            app.Id,
            app.Name,
            app.Description,
            app.Icon,
            app.AgentId,
            app.WorkflowId,
            app.PromptTemplateId,
            app.Status,
            app.PublishVersion,
            app.CreatedAt,
            app.UpdatedAt,
            app.PublishedAt);

    private static AiAppDetail MapDetail(AiApp app, IReadOnlyList<AiAppPublishRecord> records)
        => new(
            app.Id,
            app.Name,
            app.Description,
            app.Icon,
            app.AgentId,
            app.WorkflowId,
            app.PromptTemplateId,
            app.Status,
            app.PublishVersion,
            app.CreatedAt,
            app.UpdatedAt,
            app.PublishedAt,
            records.Select(MapPublishRecord).ToArray());

    private static AiAppPublishRecordItem MapPublishRecord(AiAppPublishRecord record)
        => new(
            record.Id,
            record.AppId,
            record.Version,
            record.ReleaseNote,
            record.PublishedByUserId,
            record.CreatedAt);

    private static AiAppResourceCopyTaskProgress MapCopyTask(AiAppResourceCopyTask task)
        => new(
            task.Id,
            task.AppId,
            task.SourceAppId,
            task.Status,
            task.TotalItems,
            task.CopiedItems,
            task.ErrorMessage,
            task.CreatedAt,
            task.UpdatedAt);

    private static AiAppConversationTemplateCreateMethod ParseCreateMethod(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "node" => AiAppConversationTemplateCreateMethod.NodeGenerated,
            "nodegenerated" => AiAppConversationTemplateCreateMethod.NodeGenerated,
            _ => AiAppConversationTemplateCreateMethod.Manual
        };

    private static AiAppConversationTemplateListItem MapConversationTemplate(AiAppConversationTemplate template)
        => new(
            template.Id,
            template.AppId,
            template.Name,
            template.CreateMethod.ToString(),
            template.SourceWorkflowId,
            null,
            template.ConnectorId,
            template.IsDefault,
            template.Version,
            template.PublishedVersion,
            template.CreatedAt,
            template.UpdatedAt);
}
