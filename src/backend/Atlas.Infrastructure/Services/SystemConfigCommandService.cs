using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Events;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Events;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class SystemConfigCommandService : ISystemConfigCommandService
{
    private readonly SystemConfigRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IEventBus _eventBus;

    public SystemConfigCommandService(
        SystemConfigRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IEventBus eventBus)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _eventBus = eventBus;
    }

    public async Task<long> CreateSystemConfigAsync(
        TenantId tenantId, SystemConfigCreateRequest request, CancellationToken cancellationToken)
    {
        var exists = await _repository.ExistsByKeyAsync(tenantId, request.ConfigKey, request.AppId, cancellationToken);
        if (exists)
        {
            throw new BusinessException("SystemConfigKeyExists", ErrorCodes.ValidationError);
        }

        var entity = new SystemConfig(
            tenantId,
            request.ConfigKey,
            request.ConfigValue,
            request.ConfigName,
            false,
            _idGeneratorAccessor.NextId(),
            request.ConfigType ?? "Text",
            request.AppId,
            request.GroupName,
            request.IsEncrypted,
            0);

        if (string.Equals(entity.ConfigType, "FeatureFlag", StringComparison.OrdinalIgnoreCase))
        {
            entity.UpdateFeatureFlag(request.ConfigValue, request.ConfigName, request.TargetJson, request.Remark);
        }
        else
        {
            entity.Update(request.ConfigValue, request.ConfigName, request.Remark, request.GroupName, request.IsEncrypted);
        }
        await _repository.AddAsync(entity, cancellationToken);
        await _eventBus.PublishAsync(
            new SystemConfigChangedEvent(tenantId, entity.ConfigKey, entity.AppId, null, entity.ConfigValue),
            cancellationToken);
        return entity.Id;
    }

    public async Task UpdateSystemConfigAsync(
        TenantId tenantId, long id, SystemConfigUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("SystemConfigNotFound", ErrorCodes.NotFound);

        if (request.Version.HasValue && request.Version.Value != entity.Version)
        {
            throw new BusinessException("SystemConfigVersionConflict", ErrorCodes.ValidationError);
        }

        var oldValue = entity.ConfigValue;
        if (string.Equals(entity.ConfigType, "FeatureFlag", StringComparison.OrdinalIgnoreCase))
        {
            entity.UpdateFeatureFlag(request.ConfigValue, request.ConfigName, request.TargetJson, request.Remark);
        }
        else
        {
            entity.Update(request.ConfigValue, request.ConfigName, request.Remark, request.GroupName, request.IsEncrypted);
        }

        await _repository.UpdateAsync(entity, cancellationToken);
        await _eventBus.PublishAsync(
            new SystemConfigChangedEvent(tenantId, entity.ConfigKey, entity.AppId, oldValue, entity.ConfigValue),
            cancellationToken);
    }

    public async Task<IReadOnlyList<long>> BatchUpsertSystemConfigsAsync(
        TenantId tenantId,
        SystemConfigBatchUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return [];
        }

        var normalizedItems = request.Items
            .Select(item =>
            {
                var effectiveAppId = NormalizeAppId(item.AppId) ?? NormalizeAppId(request.AppId);
                var effectiveGroupName = string.IsNullOrWhiteSpace(item.GroupName)
                    ? (string.IsNullOrWhiteSpace(request.GroupName) ? null : request.GroupName.Trim())
                    : item.GroupName.Trim();
                return new BatchUpsertItem(item, effectiveAppId, effectiveGroupName);
            })
            .ToList();

        var duplicate = normalizedItems
            .GroupBy(static x => $"{x.AppId ?? string.Empty}::{x.Item.ConfigKey}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new BusinessException("SystemConfigDuplicateKeyInBatch", ErrorCodes.ValidationError);
        }

        var keys = normalizedItems.Select(x => x.Item.ConfigKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var appIds = normalizedItems.Select(x => x.AppId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await _repository.GetByKeysAndAppIdsAsync(tenantId, keys, appIds, cancellationToken);
        var existingMap = existing.ToDictionary(
            static x => $"{x.AppId ?? string.Empty}::{x.ConfigKey}",
            StringComparer.OrdinalIgnoreCase);

        var inserts = new List<SystemConfig>();
        var updates = new List<SystemConfig>();
        var changedEvents = new List<SystemConfigChangedEvent>();
        var ids = new List<long>(normalizedItems.Count);

        foreach (var normalized in normalizedItems)
        {
            var item = normalized.Item;
            var mapKey = $"{normalized.AppId ?? string.Empty}::{item.ConfigKey}";
            if (existingMap.TryGetValue(mapKey, out var existingEntity))
            {
                if (item.Version.HasValue && item.Version.Value != existingEntity.Version)
                {
                    throw new BusinessException("SystemConfigVersionConflict", ErrorCodes.ValidationError);
                }

                var oldValue = existingEntity.ConfigValue;
                if (string.Equals(existingEntity.ConfigType, "FeatureFlag", StringComparison.OrdinalIgnoreCase))
                {
                    existingEntity.UpdateFeatureFlag(item.ConfigValue, item.ConfigName, item.TargetJson, item.Remark);
                }
                else
                {
                    existingEntity.Update(
                        item.ConfigValue,
                        item.ConfigName,
                        item.Remark,
                        normalized.GroupName,
                        item.IsEncrypted);
                }

                updates.Add(existingEntity);
                ids.Add(existingEntity.Id);
                changedEvents.Add(new SystemConfigChangedEvent(tenantId, existingEntity.ConfigKey, existingEntity.AppId, oldValue, existingEntity.ConfigValue));
            }
            else
            {
                var entity = new SystemConfig(
                    tenantId,
                    item.ConfigKey,
                    item.ConfigValue,
                    item.ConfigName,
                    false,
                    _idGeneratorAccessor.NextId(),
                    item.ConfigType,
                    normalized.AppId,
                    normalized.GroupName,
                    item.IsEncrypted ?? false,
                    0);

                if (string.Equals(entity.ConfigType, "FeatureFlag", StringComparison.OrdinalIgnoreCase))
                {
                    entity.UpdateFeatureFlag(item.ConfigValue, item.ConfigName, item.TargetJson, item.Remark);
                }
                else
                {
                    entity.Update(
                        item.ConfigValue,
                        item.ConfigName,
                        item.Remark,
                        normalized.GroupName,
                        item.IsEncrypted);
                }

                inserts.Add(entity);
                ids.Add(entity.Id);
                changedEvents.Add(new SystemConfigChangedEvent(tenantId, entity.ConfigKey, entity.AppId, null, entity.ConfigValue));
            }
        }

        await _repository.AddRangeAsync(inserts, cancellationToken);
        await _repository.UpdateRangeAsync(updates, cancellationToken);

        foreach (var changedEvent in changedEvents)
        {
            await _eventBus.PublishAsync(changedEvent, cancellationToken);
        }

        return ids;
    }

    public async Task DeleteSystemConfigAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("SystemConfigNotFound", ErrorCodes.NotFound);

        if (entity.IsBuiltIn)
        {
            throw new BusinessException("SystemConfigBuiltinCannotDelete", ErrorCodes.Forbidden);
        }

        var oldValue = entity.ConfigValue;
        await _repository.DeleteAsync(tenantId, id, cancellationToken);
        await _eventBus.PublishAsync(
            new SystemConfigChangedEvent(tenantId, entity.ConfigKey, entity.AppId, oldValue, null),
            cancellationToken);
    }

    public async Task DeleteSystemConfigByKeyAsync(
        TenantId tenantId,
        string configKey,
        string? appId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindExactByKeyAsync(tenantId, configKey, appId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        if (entity.IsBuiltIn)
        {
            throw new BusinessException("SystemConfigBuiltinCannotDelete", ErrorCodes.Forbidden);
        }

        var oldValue = entity.ConfigValue;
        await _repository.DeleteAsync(tenantId, entity.Id, cancellationToken);
        await _eventBus.PublishAsync(
            new SystemConfigChangedEvent(tenantId, entity.ConfigKey, entity.AppId, oldValue, null),
            cancellationToken);
    }

    private static string? NormalizeAppId(string? appId)
    {
        return string.IsNullOrWhiteSpace(appId) ? null : appId.Trim();
    }

    private sealed record BatchUpsertItem(
        SystemConfigBatchUpsertItem Item,
        string? AppId,
        string? GroupName);
}
