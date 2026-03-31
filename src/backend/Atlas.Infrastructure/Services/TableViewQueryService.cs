using System.Text.Json;
using Atlas.Application.TableViews.Abstractions;
using Atlas.Application.TableViews.Models;
using Atlas.Application.TableViews.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class TableViewQueryService : ITableViewQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ITableViewRepository _tableViewRepository;
    private readonly ITableViewDefaultRepository _defaultRepository;
    private readonly ITableViewDefaultConfigProvider _defaultConfigProvider;

    public TableViewQueryService(
        ITableViewRepository tableViewRepository,
        ITableViewDefaultRepository defaultRepository,
        ITableViewDefaultConfigProvider defaultConfigProvider)
    {
        _tableViewRepository = tableViewRepository;
        _defaultRepository = defaultRepository;
        _defaultConfigProvider = defaultConfigProvider;
    }

    public async Task<PagedResult<TableViewListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var pageTask = _tableViewRepository.QueryPageAsync(
            tenantId,
            userId,
            tableKey,
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);
        var defaultTask = _defaultRepository.FindByTableKeyAsync(
            tenantId,
            userId,
            tableKey,
            cancellationToken);
        await Task.WhenAll(pageTask, defaultTask);

        var (items, total) = pageTask.Result;
        var defaultView = defaultTask.Result;
        var defaultViewId = defaultView?.ViewId ?? 0;

        var resultItems = items.Select(item => new TableViewListItem(
            item.Id.ToString(),
            item.Name,
            item.TableKey,
            item.ConfigVersion,
            item.Id == defaultViewId,
            item.UpdatedAt,
            item.LastUsedAt)).ToArray();

        return new PagedResult<TableViewListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<TableViewDetail?> GetByIdAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken)
    {
        var view = await _tableViewRepository.FindByIdAsync(tenantId, userId, id, cancellationToken);
        if (view is null)
        {
            return null;
        }

        var defaultView = await _defaultRepository.FindByTableKeyAsync(
            tenantId,
            userId,
            view.TableKey,
            cancellationToken);
        var isDefault = defaultView?.ViewId == view.Id;

        var config = DeserializeConfig(view.ConfigJson);

        return new TableViewDetail(
            view.Id.ToString(),
            view.Name,
            view.TableKey,
            view.ConfigVersion,
            isDefault,
            config,
            view.UpdatedAt,
            view.LastUsedAt);
    }

    public async Task<TableViewDetail?> GetDefaultAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var defaultView = await _defaultRepository.FindByTableKeyAsync(
            tenantId,
            userId,
            tableKey,
            cancellationToken);
        if (defaultView is null)
        {
            return null;
        }

        var view = await _tableViewRepository.FindByIdAsync(tenantId, userId, defaultView.ViewId, cancellationToken);
        if (view is null)
        {
            return null;
        }

        var config = DeserializeConfig(view.ConfigJson);

        return new TableViewDetail(
            view.Id.ToString(),
            view.Name,
            view.TableKey,
            view.ConfigVersion,
            true,
            config,
            view.UpdatedAt,
            view.LastUsedAt);
    }

    public Task<TableViewConfig> GetDefaultConfigAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        return _defaultConfigProvider.GetDefaultConfigAsync(tenantId, tableKey, cancellationToken);
    }

    private static TableViewConfig DeserializeConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TableViewConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<TableViewConfig>(json, JsonOptions) ?? new TableViewConfig();
        }
        catch (JsonException)
        {
            return new TableViewConfig();
        }
    }
}
