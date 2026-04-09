using System.Text.Json;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Application.DynamicViews.Repositories;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicViewQueryService : IDynamicViewQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDynamicViewRepository _viewRepository;
    private readonly IDynamicViewVersionRepository _versionRepository;
    private readonly IDynamicDeleteCheckService _deleteCheckService;
    private readonly DynamicViewCompiler _compiler;
    private readonly DynamicViewRuntime _runtime;

    public DynamicViewQueryService(
        IDynamicViewRepository viewRepository,
        IDynamicViewVersionRepository versionRepository,
        IDynamicDeleteCheckService deleteCheckService,
        DynamicViewCompiler compiler,
        DynamicViewRuntime runtime)
    {
        _viewRepository = viewRepository;
        _versionRepository = versionRepository;
        _deleteCheckService = deleteCheckService;
        _compiler = compiler;
        _runtime = runtime;
    }

    public async Task<PagedResult<DynamicViewListItem>> QueryAsync(TenantId tenantId, long? appId, PagedRequest request, CancellationToken cancellationToken)
    {
        var (items, total) = await _viewRepository.QueryPageAsync(tenantId, appId, request, cancellationToken);
        var list = items.Select(x => new DynamicViewListItem(
            x.Id.ToString(),
            x.AppId?.ToString(),
            x.ViewKey,
            x.Name,
            x.Description,
            x.IsPublished,
            x.PublishedVersion,
            x.CreatedAt,
            x.UpdatedAt,
            x.CreatedBy,
            x.UpdatedBy)).ToArray();

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
        return new PagedResult<DynamicViewListItem>(list, total, pageIndex, pageSize);
    }

    public async Task<DynamicViewDefinitionDto?> GetByKeyAsync(TenantId tenantId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        var entity = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return DeserializeDefinition(entity.DraftDefinitionJson);
    }

    public async Task<IReadOnlyList<DynamicViewHistoryItemDto>> GetHistoryAsync(TenantId tenantId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        var versions = await _versionRepository.ListByViewKeyAsync(tenantId, appId, viewKey, cancellationToken);
        return versions
            .Select(v => new DynamicViewHistoryItemDto(v.Version, v.Status, v.CreatedBy, v.CreatedAt, v.Comment, v.Checksum))
            .ToArray();
    }

    public Task<DeleteCheckResultDto> GetDeleteCheckAsync(TenantId tenantId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        return _deleteCheckService.CheckViewDeleteAsync(tenantId, appId, viewKey, cancellationToken);
    }

    public async Task<DynamicRecordListResult> QueryRecordsAsync(TenantId tenantId, long? appId, string viewKey, DynamicViewRecordsQueryRequest request, CancellationToken cancellationToken)
    {
        var entity = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicViewNotFound");
        }

        var definition = DeserializeRequest(entity.DefinitionJson);
        var plan = _compiler.Compile(definition);
        var sqlPreview = _compiler.BuildSqlPreview(definition);
        return await _runtime.ExecutePreferPushdownAsync(tenantId, appId, sqlPreview, plan, request, cancellationToken);
    }

    public async Task<DynamicRecordListResult> PreviewAsync(TenantId tenantId, long? appId, DynamicViewPreviewRequest request, CancellationToken cancellationToken)
    {
        var plan = _compiler.Compile(request.Definition);
        var sqlPreview = _compiler.BuildSqlPreview(request.Definition);
        var query = new DynamicViewRecordsQueryRequest(
            PageIndex: 1,
            PageSize: request.Limit is > 0 and <= 1000 ? request.Limit.Value : 100,
            Keyword: null,
            SortBy: null,
            SortDesc: false,
            Filters: null)
        {
            AdvancedQuery = null
        };

        return await _runtime.ExecutePreferPushdownAsync(tenantId, appId, sqlPreview, plan, query, cancellationToken);
    }

    public Task<DynamicViewSqlPreviewDto> PreviewSqlAsync(TenantId tenantId, long? appId, DynamicViewSqlPreviewRequest request, CancellationToken cancellationToken)
    {
        var result = _compiler.BuildSqlPreview(request.Definition);
        return Task.FromResult(result);
    }

    public async Task<IReadOnlyList<DeleteCheckBlockerDto>> GetReferencesAsync(TenantId tenantId, long? appId, string viewKey, CancellationToken cancellationToken)
    {
        var check = await _deleteCheckService.CheckViewDeleteAsync(tenantId, appId, viewKey, cancellationToken);
        return check.Blockers;
    }

    private static DynamicViewDefinitionDto DeserializeDefinition(string json)
    {
        var dto = JsonSerializer.Deserialize<DynamicViewDefinitionDto>(json, JsonOptions);
        if (dto is null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewDefinitionInvalid");
        }

        return dto;
    }

    private static DynamicViewCreateOrUpdateRequest DeserializeRequest(string json)
    {
        var dto = JsonSerializer.Deserialize<DynamicViewCreateOrUpdateRequest>(json, JsonOptions);
        if (dto is null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewDefinitionInvalid");
        }

        return dto;
    }
}

