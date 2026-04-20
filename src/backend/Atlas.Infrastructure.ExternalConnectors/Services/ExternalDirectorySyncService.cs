using System.Globalization;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 通讯录全量 / 增量同步服务。
/// 全量：从根部门递归拉取所有部门 + 成员，逐条 upsert 到 Mirror，并写 Diff 行；
/// 增量：根据 ExternalDirectoryEvent 类型 + EntityId 触发详情补拉（应对 2022-08-15 之后只返 ID 的事件回调）。
/// </summary>
public sealed class ExternalDirectorySyncService : IExternalDirectorySyncService
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly IExternalDirectoryMirrorRepository _mirrorRepository;
    private readonly IExternalDirectorySyncJobRepository _jobRepository;
    private readonly IExternalDirectorySyncDiffRepository _diffRepository;
    private readonly IConnectorRuntimeOptionsAccessor _runtimeOptionsAccessor;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExternalDirectorySyncService> _logger;

    public ExternalDirectorySyncService(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalDirectoryMirrorRepository mirrorRepository,
        IExternalDirectorySyncJobRepository jobRepository,
        IExternalDirectorySyncDiffRepository diffRepository,
        IConnectorRuntimeOptionsAccessor runtimeOptionsAccessor,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGenerator,
        TimeProvider timeProvider,
        ILogger<ExternalDirectorySyncService> logger)
    {
        _registry = registry;
        _providerRepository = providerRepository;
        _mirrorRepository = mirrorRepository;
        _jobRepository = jobRepository;
        _diffRepository = diffRepository;
        _runtimeOptionsAccessor = runtimeOptionsAccessor;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ExternalDirectorySyncJobResponse> RunFullSyncAsync(long providerId, string triggerSource, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var provider = await _providerRepository.GetByIdAsync(tenantId, providerId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {providerId} not found.");
        if (!provider.Enabled)
        {
            throw new BusinessException("CONNECTOR_PROVIDER_DISABLED", $"Provider {providerId} disabled.");
        }

        var providerType = provider.ProviderType.ToProviderType();
        var directory = _registry.GetDirectory(providerType);
        var runtime = await _runtimeOptionsAccessor.ResolveAsync(tenantId.Value, provider.Id, providerType, cancellationToken).ConfigureAwait(false);
        var connectorContext = new ConnectorContext
        {
            TenantId = tenantId.Value,
            ProviderInstanceId = provider.Id,
            ProviderType = providerType,
            RuntimeOptions = runtime,
        };

        var now = _timeProvider.GetUtcNow();
        var job = new ExternalDirectorySyncJob(tenantId, _idGenerator.NextId(), provider.Id, DirectorySyncJobMode.Full, triggerSource, now);
        await _jobRepository.AddAsync(job, cancellationToken).ConfigureAwait(false);
        job.MarkRunning();
        await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

        try
        {
            // 1. 部门：从根部门递归拉取
            var rootDepartmentId = ResolveRootDepartmentId(providerType);
            var departments = await directory.ListChildDepartmentsAsync(connectorContext, rootDepartmentId, recursive: true, cancellationToken).ConfigureAwait(false);
            foreach (var d in departments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyDepartmentAsync(tenantId, provider.Id, job, d, cancellationToken).ConfigureAwait(false);
            }

            // 2. 成员：按部门取直属用户（递归）。各 provider 内部已分页。
            foreach (var d in departments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<ExternalUserProfile> members;
                try
                {
                    members = await directory.ListDepartmentMembersAsync(connectorContext, d.ExternalDepartmentId, recursive: false, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    job.IncrementFailed();
                    await WriteDiffAsync(tenantId, job.Id, provider.Id, DirectorySyncDiffType.UserUpdated, d.ExternalDepartmentId, $"List members failed: {ex.Message}", ex.Message, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                foreach (var u in members)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ApplyUserAsync(tenantId, provider.Id, job, u, cancellationToken).ConfigureAwait(false);
                    await ApplyRelationAsync(tenantId, provider.Id, job, d.ExternalDepartmentId, u, cancellationToken).ConfigureAwait(false);
                }
            }

            job.Complete(_timeProvider.GetUtcNow());
            await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
            return Map(job);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "External directory full sync failed for provider {ProviderId}.", provider.Id);
            job.Fail(ex.Message, _timeProvider.GetUtcNow());
            await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ExternalDirectorySyncJobResponse> ApplyIncrementalEventAsync(long providerId, ExternalDirectoryEvent evt, string triggerSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var tenantId = _tenantProvider.GetTenantId();
        var provider = await _providerRepository.GetByIdAsync(tenantId, providerId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {providerId} not found.");

        var providerType = provider.ProviderType.ToProviderType();
        var directory = _registry.GetDirectory(providerType);
        var runtime = await _runtimeOptionsAccessor.ResolveAsync(tenantId.Value, provider.Id, providerType, cancellationToken).ConfigureAwait(false);
        var connectorContext = new ConnectorContext
        {
            TenantId = tenantId.Value,
            ProviderInstanceId = provider.Id,
            ProviderType = providerType,
            RuntimeOptions = runtime,
        };

        var now = _timeProvider.GetUtcNow();
        var job = new ExternalDirectorySyncJob(tenantId, _idGenerator.NextId(), provider.Id, DirectorySyncJobMode.Incremental, triggerSource, now);
        await _jobRepository.AddAsync(job, cancellationToken).ConfigureAwait(false);
        job.MarkRunning();
        await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

        try
        {
            switch (evt.Kind)
            {
                case ExternalDirectoryEventKind.UserCreated:
                case ExternalDirectoryEventKind.UserUpdated:
                {
                    var detail = await directory.GetUserAsync(connectorContext, evt.EntityId, cancellationToken).ConfigureAwait(false);
                    if (detail is not null)
                    {
                        await ApplyUserAsync(tenantId, provider.Id, job, detail, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                }
                case ExternalDirectoryEventKind.UserDeleted:
                {
                    var existing = await _mirrorRepository.GetUserAsync(tenantId, provider.Id, evt.EntityId, cancellationToken).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        existing.MarkDeleted(_timeProvider.GetUtcNow());
                        await _mirrorRepository.UpsertUserAsync(existing, cancellationToken).ConfigureAwait(false);
                        await WriteDiffAsync(tenantId, job.Id, provider.Id, DirectorySyncDiffType.UserDeleted, evt.EntityId, $"User {evt.EntityId} marked deleted", null, cancellationToken).ConfigureAwait(false);
                        job.Accumulate(DirectorySyncDiffType.UserDeleted);
                    }
                    break;
                }
                case ExternalDirectoryEventKind.DepartmentCreated:
                case ExternalDirectoryEventKind.DepartmentUpdated:
                {
                    var detail = await directory.GetDepartmentAsync(connectorContext, evt.EntityId, cancellationToken).ConfigureAwait(false);
                    if (detail is not null)
                    {
                        await ApplyDepartmentAsync(tenantId, provider.Id, job, detail, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                }
                case ExternalDirectoryEventKind.DepartmentDeleted:
                {
                    var existing = await _mirrorRepository.GetDepartmentAsync(tenantId, provider.Id, evt.EntityId, cancellationToken).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        existing.MarkDeleted(_timeProvider.GetUtcNow());
                        await _mirrorRepository.UpsertDepartmentAsync(existing, cancellationToken).ConfigureAwait(false);
                        await WriteDiffAsync(tenantId, job.Id, provider.Id, DirectorySyncDiffType.DepartmentDeleted, evt.EntityId, $"Department {evt.EntityId} marked deleted", null, cancellationToken).ConfigureAwait(false);
                        job.Accumulate(DirectorySyncDiffType.DepartmentDeleted);
                    }
                    break;
                }
            }

            job.Complete(_timeProvider.GetUtcNow());
            await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
            return Map(job);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "External directory incremental event failed (kind={Kind}, entityId={EntityId}).", evt.Kind, evt.EntityId);
            job.Fail(ex.Message, _timeProvider.GetUtcNow());
            await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ExternalDirectorySyncJobResponse?> GetJobAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(_tenantProvider.GetTenantId(), jobId, cancellationToken).ConfigureAwait(false);
        return job is null ? null : Map(job);
    }

    public async Task<IReadOnlyList<ExternalDirectorySyncJobResponse>> ListRecentAsync(long providerId, int take, CancellationToken cancellationToken)
    {
        var jobs = await _jobRepository.ListRecentAsync(_tenantProvider.GetTenantId(), providerId, take, cancellationToken).ConfigureAwait(false);
        return jobs.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ExternalDirectorySyncDiffResponse>> ListJobDiffsAsync(long jobId, int skip, int take, CancellationToken cancellationToken)
    {
        var items = await _diffRepository.ListByJobAsync(_tenantProvider.GetTenantId(), jobId, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(MapDiff).ToList();
    }

    public Task<int> CountJobDiffsAsync(long jobId, CancellationToken cancellationToken)
        => _diffRepository.CountByJobAsync(_tenantProvider.GetTenantId(), jobId, cancellationToken);

    private async Task ApplyDepartmentAsync(TenantId tenantId, long providerId, ExternalDirectorySyncJob job, ExternalDepartment d, CancellationToken cancellationToken)
    {
        var existing = await _mirrorRepository.GetDepartmentAsync(tenantId, providerId, d.ExternalDepartmentId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        DirectorySyncDiffType diffType;
        if (existing is null)
        {
            existing = new ExternalDepartmentMirror(tenantId, _idGenerator.NextId(), providerId, d.ExternalDepartmentId, d.ParentExternalDepartmentId, d.Name, d.FullPath, d.Order, d.RawJson, now);
            diffType = DirectorySyncDiffType.DepartmentCreated;
        }
        else
        {
            existing.UpdateFrom(d.ParentExternalDepartmentId, d.Name, d.FullPath, d.Order, d.RawJson, now);
            diffType = DirectorySyncDiffType.DepartmentUpdated;
        }
        await _mirrorRepository.UpsertDepartmentAsync(existing, cancellationToken).ConfigureAwait(false);
        job.Accumulate(diffType);
        await WriteDiffAsync(tenantId, job.Id, providerId, diffType, d.ExternalDepartmentId, $"Department '{d.Name}'", null, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyUserAsync(TenantId tenantId, long providerId, ExternalDirectorySyncJob job, ExternalUserProfile u, CancellationToken cancellationToken)
    {
        var existing = await _mirrorRepository.GetUserAsync(tenantId, providerId, u.ExternalUserId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        DirectorySyncDiffType diffType;
        if (existing is null)
        {
            existing = new ExternalUserMirror(
                tenantId,
                _idGenerator.NextId(),
                providerId,
                u.ExternalUserId,
                u.OpenId,
                u.UnionId,
                u.Name,
                u.EnglishName,
                u.Mobile,
                u.Email,
                u.Avatar,
                u.Position,
                u.PrimaryDepartmentId,
                u.Status,
                u.RawJson,
                now);
            diffType = DirectorySyncDiffType.UserCreated;
        }
        else
        {
            existing.UpdateFrom(u.OpenId, u.UnionId, u.Name, u.EnglishName, u.Mobile, u.Email, u.Avatar, u.Position, u.PrimaryDepartmentId, u.Status, u.RawJson, now);
            diffType = DirectorySyncDiffType.UserUpdated;
        }
        await _mirrorRepository.UpsertUserAsync(existing, cancellationToken).ConfigureAwait(false);
        job.Accumulate(diffType);
        await WriteDiffAsync(tenantId, job.Id, providerId, diffType, u.ExternalUserId, $"User '{u.Name ?? u.ExternalUserId}'", null, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyRelationAsync(TenantId tenantId, long providerId, ExternalDirectorySyncJob job, string externalDepartmentId, ExternalUserProfile u, CancellationToken cancellationToken)
    {
        var isPrimary = string.Equals(u.PrimaryDepartmentId, externalDepartmentId, StringComparison.Ordinal);
        var rel = new ExternalDepartmentUserRelation(tenantId, _idGenerator.NextId(), providerId, externalDepartmentId, u.ExternalUserId, isPrimary, order: 0, _timeProvider.GetUtcNow());
        await _mirrorRepository.UpsertRelationAsync(rel, cancellationToken).ConfigureAwait(false);
        job.Accumulate(DirectorySyncDiffType.RelationCreated);
        await WriteDiffAsync(tenantId, job.Id, providerId, DirectorySyncDiffType.RelationCreated,
            string.Concat(externalDepartmentId, "::", u.ExternalUserId), $"Relation dept={externalDepartmentId} user={u.ExternalUserId}", null, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteDiffAsync(TenantId tenantId, long jobId, long providerId, DirectorySyncDiffType type, string entityId, string summary, string? error, CancellationToken cancellationToken)
    {
        var diff = new ExternalDirectorySyncDiff(tenantId, _idGenerator.NextId(), jobId, providerId, type, entityId, summary, error, _timeProvider.GetUtcNow());
        await _diffRepository.AddAsync(diff, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveRootDepartmentId(string providerType) => providerType switch
    {
        "wecom" => "1",  // 企微 root_id 默认为 1
        "feishu" => "0", // 飞书 root open_department_id 为 "0"
        _ => string.Empty,
    };

    private static ExternalDirectorySyncJobResponse Map(ExternalDirectorySyncJob job) => new()
    {
        Id = job.Id,
        ProviderId = job.ProviderId,
        Mode = job.Mode,
        Status = job.Status,
        TriggerSource = job.TriggerSource,
        DepartmentCreated = job.DepartmentCreated,
        DepartmentUpdated = job.DepartmentUpdated,
        DepartmentDeleted = job.DepartmentDeleted,
        UserCreated = job.UserCreated,
        UserUpdated = job.UserUpdated,
        UserDeleted = job.UserDeleted,
        RelationChanged = job.RelationChanged,
        FailedItems = job.FailedItems,
        ErrorMessage = job.ErrorMessage,
        StartedAt = job.StartedAt,
        FinishedAt = job.FinishedAt,
    };

    private static ExternalDirectorySyncDiffResponse MapDiff(ExternalDirectorySyncDiff diff) => new()
    {
        Id = diff.Id,
        JobId = diff.JobId,
        DiffType = diff.DiffType,
        EntityId = diff.EntityId,
        Summary = diff.Summary,
        ErrorMessage = diff.ErrorMessage,
        OccurredAt = diff.OccurredAt,
    };
}
