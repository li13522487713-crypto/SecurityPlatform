using System.IO;
using System.Text.Json;
using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Application.DynamicViews.Repositories;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicViewP2Service : IDynamicViewP2Service
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ISqlSugarClient _db;
    private readonly ISqlQueryService _sqlQueryService;
    private readonly IDynamicViewRepository _viewRepository;
    private readonly IDynamicViewVersionRepository _versionRepository;
    private readonly DynamicViewCompiler _compiler;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;

    public DynamicViewP2Service(
        ISqlSugarClient db,
        ISqlQueryService sqlQueryService,
        IDynamicViewRepository viewRepository,
        IDynamicViewVersionRepository versionRepository,
        DynamicViewCompiler compiler,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider)
    {
        _db = db;
        _sqlQueryService = sqlQueryService;
        _viewRepository = viewRepository;
        _versionRepository = versionRepository;
        _compiler = compiler;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<DynamicExternalExtractDataSourceDto>> ListExternalExtractDataSourcesAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        var dataSourceIds = await _db.Queryable<TenantAppDataSourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TenantAppInstanceId == appId && x.IsActive)
            .Select(x => x.DataSourceId)
            .ToListAsync(cancellationToken);

        if (dataSourceIds.Count == 0)
        {
            return Array.Empty<DynamicExternalExtractDataSourceDto>();
        }

        var list = await _db.Queryable<TenantDataSource>()
            .Where(x => x.TenantIdValue == tenantId.Value.ToString() && x.IsActive && SqlFunc.ContainsArray(dataSourceIds, x.Id))
            .OrderBy(x => x.Name, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        return list.Select(x => new DynamicExternalExtractDataSourceDto(
            x.Id.ToString(),
            x.Name,
            x.DbType)).ToArray();
    }

    public async Task<DynamicExternalExtractSchemaResult> GetExternalExtractSchemaAsync(
        TenantId tenantId,
        long appId,
        long dataSourceId,
        CancellationToken cancellationToken)
    {
        await EnsureDataSourceBoundToAppAsync(tenantId, appId, dataSourceId, cancellationToken);
        var schema = await _sqlQueryService.GetSchemaAsync(tenantId.Value.ToString(), dataSourceId, cancellationToken);
        if (!schema.Success)
        {
            throw new BusinessException(ErrorCodes.ValidationError, schema.ErrorMessage ?? "DynamicExternalExtractSchemaFailed");
        }

        return new DynamicExternalExtractSchemaResult(
            dataSourceId.ToString(),
            schema.Tables.Select(table => new DynamicExternalExtractSchemaTableDto(
                table.Name,
                table.Columns.Select(column => new DynamicExternalExtractColumnDto(column.Name, column.DataType)).ToArray())).ToArray());
    }

    public async Task<DynamicExternalExtractPreviewResult> PreviewExternalExtractAsync(
        TenantId tenantId,
        long appId,
        long userId,
        DynamicExternalExtractPreviewRequest request,
        CancellationToken cancellationToken)
    {
        _ = userId;
        await EnsureDataSourceBoundToAppAsync(tenantId, appId, request.DataSourceId, cancellationToken);

        var sql = request.Sql?.Trim() ?? string.Empty;
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !sql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicExternalExtractOnlySelectAllowed");
        }

        var result = await _sqlQueryService.ExecutePreviewQueryAsync(
            tenantId.Value.ToString(),
            request.DataSourceId,
            new SqlQueryRequest(sql),
            cancellationToken);

        if (!result.Success)
        {
            return new DynamicExternalExtractPreviewResult(
                false,
                result.ErrorMessage,
                Array.Empty<DynamicExternalExtractColumnDto>(),
                Array.Empty<Dictionary<string, object?>>());
        }

        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        var rows = result.Data
            .Take(limit)
            .Select(row => row.ToDictionary(kv => kv.Key, kv => kv.Value))
            .ToArray();
        return new DynamicExternalExtractPreviewResult(
            true,
            null,
            result.Columns.Select(column => new DynamicExternalExtractColumnDto(column.Field, column.Type)).ToArray(),
            rows);
    }

    public async Task<DynamicPhysicalViewPublishResult> PublishPhysicalViewAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        DynamicPhysicalViewPublishRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _viewRepository.FindByKeyAsync(tenantId, appId, viewKey, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicViewNotFound");
        }

        var definition = JsonSerializer.Deserialize<DynamicViewCreateOrUpdateRequest>(entity.DefinitionJson, JsonOptions);
        if (definition is null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewDefinitionInvalid");
        }

        var sqlPreview = _compiler.BuildSqlPreview(definition);
        if (!sqlPreview.FullyPushdown)
        {
            var warning = string.Join(" | ", sqlPreview.Warnings);
            throw new BusinessException(ErrorCodes.ValidationError, $"DynamicPhysicalViewRequiresFullyPushdown:{warning}");
        }

        var physicalViewName = ResolvePhysicalViewName(viewKey, request.PhysicalViewName);
        if (request.ReplaceIfExists)
        {
            await _db.Ado.ExecuteCommandAsync($"DROP VIEW IF EXISTS \"{physicalViewName}\";");
        }

        await _db.Ado.ExecuteCommandAsync($"CREATE VIEW IF NOT EXISTS \"{physicalViewName}\" AS {sqlPreview.Sql};");

        var now = _timeProvider.GetUtcNow();
        var publication = new DynamicPhysicalViewPublication(
            tenantId,
            _idGeneratorAccessor.NextId(),
            appId,
            viewKey,
            entity.PublishedVersion,
            physicalViewName,
            request.DataSourceId,
            "Published",
            request.Comment,
            userId,
            now);
        await _db.Insertable(publication).ExecuteCommandAsync(cancellationToken);

        return new DynamicPhysicalViewPublishResult(
            viewKey,
            publication.Id.ToString(),
            publication.Version,
            physicalViewName,
            request.DataSourceId,
            publication.Status,
            now,
            true,
            "Physical view published.");
    }

    public async Task<IReadOnlyList<DynamicPhysicalViewPublicationDto>> ListPhysicalPublicationsAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<DynamicPhysicalViewPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ViewKey == viewKey);
        query = appId.HasValue ? query.Where(x => x.AppId == appId.Value) : query.Where(x => x.AppId == null);

        var rows = await query.OrderBy(x => x.PublishedAt, OrderByType.Desc).ToListAsync(cancellationToken);
        return rows.Select(x => new DynamicPhysicalViewPublicationDto(
            x.Id.ToString(),
            x.ViewKey,
            x.Version,
            x.PhysicalViewName,
            x.Status,
            x.Comment,
            x.DataSourceId,
            x.PublishedBy,
            x.PublishedAt)).ToArray();
    }

    public async Task<DynamicPhysicalViewPublishResult> RollbackPhysicalPublicationAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        int version,
        CancellationToken cancellationToken)
    {
        var snapshot = await _versionRepository.FindByVersionAsync(tenantId, appId, viewKey, version, cancellationToken);
        if (snapshot is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicViewVersionNotFound");
        }

        var request = JsonSerializer.Deserialize<DynamicViewCreateOrUpdateRequest>(snapshot.DefinitionJson, JsonOptions);
        if (request is null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewDefinitionInvalid");
        }

        var preview = _compiler.BuildSqlPreview(request);
        if (!preview.FullyPushdown)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicPhysicalViewRequiresFullyPushdown");
        }

        var publication = await _db.Queryable<DynamicPhysicalViewPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ViewKey == viewKey)
            .OrderBy(x => x.PublishedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        if (publication is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicPhysicalViewPublicationNotFound");
        }

        await _db.Ado.ExecuteCommandAsync($"DROP VIEW IF EXISTS \"{publication.PhysicalViewName}\";");
        await _db.Ado.ExecuteCommandAsync($"CREATE VIEW IF NOT EXISTS \"{publication.PhysicalViewName}\" AS {preview.Sql};");

        var now = _timeProvider.GetUtcNow();
        var history = new DynamicPhysicalViewPublication(
            tenantId,
            _idGeneratorAccessor.NextId(),
            appId,
            viewKey,
            version,
            publication.PhysicalViewName,
            publication.DataSourceId,
            "RolledBack",
            $"Rollback to version {version}",
            userId,
            now);
        await _db.Insertable(history).ExecuteCommandAsync(cancellationToken);

        return new DynamicPhysicalViewPublishResult(
            viewKey,
            history.Id.ToString(),
            version,
            publication.PhysicalViewName,
            publication.DataSourceId,
            history.Status,
            now,
            true,
            "Physical view rollback completed.");
    }

    public async Task DeletePhysicalPublicationAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        string publicationId,
        CancellationToken cancellationToken)
    {
        _ = userId;
        if (!long.TryParse(publicationId, out var parsedId))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicPhysicalViewPublicationIdInvalid");
        }

        var query = _db.Queryable<DynamicPhysicalViewPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ViewKey == viewKey && x.Id == parsedId);
        query = appId.HasValue ? query.Where(x => x.AppId == appId.Value) : query.Where(x => x.AppId == null);
        var entity = await query.FirstAsync(cancellationToken);
        if (entity is null)
        {
            return;
        }

        await _db.Ado.ExecuteCommandAsync($"DROP VIEW IF EXISTS \"{entity.PhysicalViewName}\";");
        await _db.Deleteable<DynamicPhysicalViewPublication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == entity.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsureDataSourceBoundToAppAsync(TenantId tenantId, long appId, long dataSourceId, CancellationToken cancellationToken)
    {
        var exists = await _db.Queryable<TenantAppDataSourceBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.TenantAppInstanceId == appId
                && x.DataSourceId == dataSourceId
                && x.IsActive)
            .AnyAsync();
        if (!exists)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicExternalExtractDataSourceNotBoundToApp");
        }
    }

    private static string ResolvePhysicalViewName(string viewKey, string? input)
    {
        var name = string.IsNullOrWhiteSpace(input) ? $"vw_{viewKey}" : input.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }
}
