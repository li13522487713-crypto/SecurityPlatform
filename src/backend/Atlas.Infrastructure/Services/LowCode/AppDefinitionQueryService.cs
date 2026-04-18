using AutoMapper;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>应用定义查询服务（M01）。</summary>
public sealed class AppDefinitionQueryService : IAppDefinitionQueryService
{
    private readonly IAppDefinitionRepository _appRepo;
    private readonly IPageDefinitionRepository _pageRepo;
    private readonly IAppVariableRepository _varRepo;
    private readonly IAppContentParamRepository _cpRepo;
    private readonly IAppVersionArchiveRepository _versionRepo;
    private readonly IMapper _mapper;

    public AppDefinitionQueryService(
        IAppDefinitionRepository appRepo,
        IPageDefinitionRepository pageRepo,
        IAppVariableRepository varRepo,
        IAppContentParamRepository cpRepo,
        IAppVersionArchiveRepository versionRepo,
        IMapper mapper)
    {
        _appRepo = appRepo;
        _pageRepo = pageRepo;
        _varRepo = varRepo;
        _cpRepo = cpRepo;
        _versionRepo = versionRepo;
        _mapper = mapper;
    }

    public async Task<PagedResult<AppDefinitionListItem>> QueryAsync(PagedRequest request, TenantId tenantId, string? status, CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);
        var (items, total) = await _appRepo.QueryPagedAsync(tenantId, pageIndex, pageSize, request.Keyword, status, cancellationToken);
        var dtoItems = _mapper.Map<IReadOnlyList<AppDefinitionListItem>>(items);
        return new PagedResult<AppDefinitionListItem>(
            Items: dtoItems,
            Total: total,
            PageIndex: pageIndex,
            PageSize: pageSize);
    }

    public async Task<AppDefinitionDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken);
        return app is null ? null : _mapper.Map<AppDefinitionDetail>(app);
    }

    public async Task<AppDraftResponse?> GetDraftAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken);
        if (app is null) return null;
        return new AppDraftResponse(
            AppId: app.Id.ToString(),
            SchemaVersion: app.SchemaVersion,
            SchemaJson: app.DraftSchemaJson,
            UpdatedAt: app.UpdatedAt,
            UpdatedBy: app.UpdatedByUserId?.ToString());
    }

    public async Task<AppSchemaSnapshotDto?> GetSchemaSnapshotAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken);
        if (app is null) return null;
        var pages = await _pageRepo.ListByAppAsync(tenantId, id, cancellationToken);
        var variables = await _varRepo.ListByAppAsync(tenantId, id, scope: null, cancellationToken);
        var contentParams = await _cpRepo.ListByAppAsync(tenantId, id, cancellationToken);

        return new AppSchemaSnapshotDto(
            AppId: app.Id.ToString(),
            Code: app.Code,
            SchemaVersion: app.SchemaVersion,
            App: _mapper.Map<AppDefinitionDetail>(app),
            Pages: _mapper.Map<IReadOnlyList<PageDefinitionDetail>>(pages),
            Variables: _mapper.Map<IReadOnlyList<AppVariableDto>>(variables),
            ContentParams: _mapper.Map<IReadOnlyList<AppContentParamDto>>(contentParams));
    }

    public async Task<IReadOnlyList<AppVersionArchiveListItem>> ListVersionsAsync(TenantId tenantId, long id, bool includeSystemSnapshot, CancellationToken cancellationToken)
    {
        var list = await _versionRepo.ListByAppAsync(tenantId, id, includeSystemSnapshot, cancellationToken);
        return _mapper.Map<IReadOnlyList<AppVersionArchiveListItem>>(list);
    }

    public async Task<AppVersionedSchemaSnapshotDto?> GetVersionSchemaSnapshotAsync(TenantId tenantId, long appId, long versionId, CancellationToken cancellationToken)
    {
        var archive = await _versionRepo.FindByIdAsync(tenantId, versionId, cancellationToken);
        if (archive is null || archive.AppId != appId) return null;
        return new AppVersionedSchemaSnapshotDto(
            AppId: appId.ToString(),
            VersionId: archive.Id.ToString(),
            VersionLabel: archive.VersionLabel,
            SchemaJson: archive.SchemaSnapshotJson,
            ResourceSnapshotJson: archive.ResourceSnapshotJson,
            CreatedAt: archive.CreatedAt);
    }
}
