using System.Text.Json;
using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Application.DynamicViews.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicViewCommandService : IDynamicViewCommandService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDynamicViewRepository _viewRepository;
    private readonly IDynamicViewVersionRepository _versionRepository;
    private readonly IDynamicDeleteCheckService _deleteCheckService;
    private readonly DynamicViewVersionService _versionService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;

    public DynamicViewCommandService(
        IDynamicViewRepository viewRepository,
        IDynamicViewVersionRepository versionRepository,
        IDynamicDeleteCheckService deleteCheckService,
        DynamicViewVersionService versionService,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider)
    {
        _viewRepository = viewRepository;
        _versionRepository = versionRepository;
        _deleteCheckService = deleteCheckService;
        _versionService = versionService;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
    }

    public async Task<string> CreateAsync(TenantId tenantId, long userId, DynamicViewCreateOrUpdateRequest request, CancellationToken cancellationToken)
    {
        var viewKey = request.ViewKey.Trim();
        var appId = ParseAppId(request.AppId);
        var existing = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewKeyExists");
        }

        var now = _timeProvider.GetUtcNow();
        var definitionJson = JsonSerializer.Serialize(ToDefinitionDto(request), JsonOptions);
        var entity = new DynamicViewDefinition(
            tenantId,
            _idGeneratorAccessor.NextId(),
            appId,
            viewKey,
            request.Name,
            request.Description,
            definitionJson,
            userId,
            now);

        await _viewRepository.AddAsync(entity, cancellationToken);
        return viewKey;
    }

    public async Task UpdateAsync(TenantId tenantId, long userId, string viewKey, DynamicViewCreateOrUpdateRequest request, CancellationToken cancellationToken)
    {
        var appId = ParseAppId(request.AppId);
        var entity = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicViewNotFound");
        }

        var draftJson = JsonSerializer.Serialize(ToDefinitionDto(request), JsonOptions);
        entity.UpdateDraft(request.Name, request.Description, draftJson, userId, _timeProvider.GetUtcNow());
        await _viewRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long userId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        var check = await _deleteCheckService.CheckViewDeleteAsync(tenantId, appId, viewKey, cancellationToken);
        if (!check.CanDelete)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewDeleteBlocked");
        }

        var entity = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (entity is null)
        {
            return;
        }

        await _viewRepository.DeleteAsync(tenantId, appId, entity.Id, cancellationToken);
    }

    public async Task<DynamicViewPublishResultDto> PublishAsync(TenantId tenantId, long userId, long? appId, string viewKey, string? comment, CancellationToken cancellationToken)
    {
        var entity = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicViewNotFound");
        }

        var definition = JsonSerializer.Deserialize<DynamicViewCreateOrUpdateRequest>(entity.DraftDefinitionJson, JsonOptions)
            ?? throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewDefinitionInvalid");

        var version = await _versionService.CreateVersionAsync(tenantId, appId, viewKey, definition, userId, "Published", comment, cancellationToken);
        entity.Publish(entity.DraftDefinitionJson, version.Version, userId, version.PublishedAt);
        await _viewRepository.UpdateAsync(entity, cancellationToken);
        return version;
    }

    public async Task<DynamicViewPublishResultDto> RollbackAsync(TenantId tenantId, long userId, long? appId, string viewKey, int version, string? comment, CancellationToken cancellationToken)
    {
        var entity = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicViewNotFound");
        }

        var snapshot = await _versionRepository.FindByVersionAsync(tenantId, appId, viewKey, version, cancellationToken);
        if (snapshot is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicViewVersionNotFound");
        }

        var request = JsonSerializer.Deserialize<DynamicViewCreateOrUpdateRequest>(snapshot.DefinitionJson, JsonOptions)
            ?? throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewDefinitionInvalid");

        var publish = await _versionService.CreateVersionAsync(tenantId, appId, viewKey, request, userId, "Rollback", comment, cancellationToken);
        entity.Rollback(snapshot.DefinitionJson, publish.Version, userId, publish.PublishedAt);
        await _viewRepository.UpdateAsync(entity, cancellationToken);
        return publish;
    }

    private static long? ParseAppId(string appId)
    {
        return long.TryParse(appId, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static DynamicViewDefinitionDto ToDefinitionDto(DynamicViewCreateOrUpdateRequest request)
    {
        return new DynamicViewDefinitionDto(
            null,
            request.AppId,
            request.ViewKey,
            request.Name,
            request.Description,
            request.Nodes,
            request.Edges,
            request.OutputFields,
            request.Filters,
            request.GroupBy,
            request.Sorts);
    }
}
