using System.Text.Json;
using Atlas.Application.TableViews.Abstractions;
using Atlas.Application.TableViews.Models;
using Atlas.Application.TableViews.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class TableViewCommandService : ITableViewCommandService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ITableViewRepository _tableViewRepository;
    private readonly ITableViewDefaultRepository _defaultRepository;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;

    public TableViewCommandService(
        ITableViewRepository tableViewRepository,
        ITableViewDefaultRepository defaultRepository,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider)
    {
        _tableViewRepository = tableViewRepository;
        _defaultRepository = defaultRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        TableViewCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var existing = await _tableViewRepository.FindByNameAsync(
            tenantId,
            userId,
            request.TableKey,
            request.Name,
            cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "视图名称已存在");
        }

        var configVersion = request.ConfigVersion ?? 1;
        var configJson = SerializeConfig(request.Config);
        var now = _timeProvider.GetUtcNow();
        var entity = new TableView(
            tenantId,
            userId,
            request.TableKey,
            request.Name,
            configJson,
            configVersion,
            id,
            now);

        await _tableViewRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        TableViewUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var view = await _tableViewRepository.FindByIdAsync(tenantId, userId, id, cancellationToken);
        if (view is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "视图不存在");
        }

        var conflict = await _tableViewRepository.FindByNameAsync(
            tenantId,
            userId,
            view.TableKey,
            request.Name,
            cancellationToken);
        if (conflict is not null && conflict.Id != view.Id)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "视图名称已存在");
        }

        var configVersion = request.ConfigVersion ?? view.ConfigVersion;
        var configJson = SerializeConfig(request.Config);
        view.Update(request.Name, configJson, configVersion, _timeProvider.GetUtcNow());
        await _tableViewRepository.UpdateAsync(view, cancellationToken);
    }

    public async Task UpdateConfigAsync(
        TenantId tenantId,
        long userId,
        long id,
        TableViewConfigUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var view = await _tableViewRepository.FindByIdAsync(tenantId, userId, id, cancellationToken);
        if (view is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "视图不存在");
        }

        var configVersion = request.ConfigVersion ?? view.ConfigVersion;
        var configJson = SerializeConfig(request.Config);
        view.UpdateConfig(configJson, configVersion, _timeProvider.GetUtcNow());
        await _tableViewRepository.UpdateAsync(view, cancellationToken);
    }

    public async Task<long> DuplicateAsync(
        TenantId tenantId,
        long userId,
        long id,
        TableViewDuplicateRequest request,
        long newId,
        CancellationToken cancellationToken)
    {
        var view = await _tableViewRepository.FindByIdAsync(tenantId, userId, id, cancellationToken);
        if (view is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "视图不存在");
        }

        var existing = await _tableViewRepository.FindByNameAsync(
            tenantId,
            userId,
            view.TableKey,
            request.Name,
            cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "视图名称已存在");
        }

        var now = _timeProvider.GetUtcNow();
        var copy = new TableView(
            tenantId,
            userId,
            view.TableKey,
            request.Name,
            view.ConfigJson,
            view.ConfigVersion,
            newId,
            now);

        await _tableViewRepository.AddAsync(copy, cancellationToken);
        return copy.Id;
    }

    public async Task SetDefaultAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken)
    {
        var view = await _tableViewRepository.FindByIdAsync(tenantId, userId, id, cancellationToken);
        if (view is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "视图不存在");
        }

        var existing = await _defaultRepository.FindByTableKeyAsync(
            tenantId,
            userId,
            view.TableKey,
            cancellationToken);
        var now = _timeProvider.GetUtcNow();

        if (existing is null)
        {
            var entry = new UserTableViewDefault(
                tenantId,
                userId,
                view.TableKey,
                view.Id,
                _idGeneratorAccessor.NextId(),
                now: now);
            await _defaultRepository.AddAsync(entry, cancellationToken);
            return;
        }

        existing.UpdateView(view.Id, now);
        await _defaultRepository.UpdateAsync(existing, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken)
    {
        await _tableViewRepository.DeleteAsync(tenantId, userId, id, cancellationToken);
        await _defaultRepository.DeleteByViewIdAsync(tenantId, userId, id, cancellationToken);
    }

    private static string SerializeConfig(TableViewConfig config)
    {
        return JsonSerializer.Serialize(config, JsonOptions);
    }
}
