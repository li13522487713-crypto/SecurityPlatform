using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class MicroflowAppAssetService : IMicroflowAppAssetService
{
    private readonly ISqlSugarClient _db;
    private readonly IMicroflowMetadataService _metadataService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowAppAssetService(
        ISqlSugarClient db,
        IMicroflowMetadataService metadataService,
        IMicroflowRequestContextAccessor requestContextAccessor)
    {
        _db = db;
        _metadataService = metadataService;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<MicroflowAppAssetDto> GetAppAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        var app = await LoadAppAsync(appId, workspaceId, cancellationToken);
        var modules = await ListModulesAsync(appId, workspaceId, cancellationToken);
        return new MicroflowAppAssetDto
        {
            AppId = app.Id.ToString(),
            WorkspaceId = workspaceId,
            Name = app.Name,
            Description = app.Description,
            Status = app.Status == AiAppStatus.Published ? "published" : "draft",
            Modules = modules
        };
    }

    public async Task<IReadOnlyList<MicroflowModuleAssetDto>> ListModulesAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        _ = await LoadAppAsync(appId, workspaceId, cancellationToken);
        var catalog = await _metadataService.GetCatalogAsync(
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = workspaceId,
                IncludeSystem = true,
                IncludeArchived = false
            },
            cancellationToken);

        var modules = catalog.Modules
            .Select(module => new MicroflowModuleAssetDto
            {
                ModuleId = string.IsNullOrWhiteSpace(module.Id) ? module.Name : module.Id,
                Name = string.IsNullOrWhiteSpace(module.Name) ? module.Id : module.Name,
                QualifiedName = string.IsNullOrWhiteSpace(module.QualifiedName) ? module.Name : module.QualifiedName,
                Description = module.Description
            })
            .Where(module => !string.IsNullOrWhiteSpace(module.ModuleId))
            .GroupBy(module => module.ModuleId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        return modules.Length > 0
            ? modules
            : new[]
            {
                new MicroflowModuleAssetDto
                {
                    ModuleId = "main",
                    Name = "Main",
                    QualifiedName = "Main",
                    Description = "Default module"
                }
            };
    }

    private async Task<AiApp> LoadAppAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(appId, out var id))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流应用不存在或 appId 无效。", 404);
        }

        var tenantId = _requestContextAccessor.Current.TenantId;
        var query = _db.Queryable<AiApp>().Where(app => app.Id == id);
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsedTenantId))
        {
            query = query.Where(app => app.TenantIdValue == parsedTenantId);
        }

        var app = await query.FirstAsync(cancellationToken);
        if (app is null)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流应用不存在。", 404);
        }

        if (!long.TryParse(workspaceId, out var workspaceIdValue) || app.WorkspaceId != workspaceIdValue)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "该应用不属于当前工作区。", 403);
        }

        return app;
    }
}
