using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class MicroflowAppAssetService : IMicroflowAppAssetService
{
    private readonly ISqlSugarClient _db;
    private readonly IMicroflowMetadataService _metadataService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMendixDomainModelService? _domainModelService;

    public MicroflowAppAssetService(
        ISqlSugarClient db,
        IMicroflowMetadataService metadataService,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMendixDomainModelService? domainModelService = null)
    {
        _db = db;
        _metadataService = metadataService;
        _requestContextAccessor = requestContextAccessor;
        _domainModelService = domainModelService;
    }

    public async Task<MicroflowAppAssetDto> GetAppAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        var app = await LoadAppAssetAsync(appId, workspaceId, cancellationToken);
        var modules = await ListModulesAsync(appId, workspaceId, cancellationToken);
        return new MicroflowAppAssetDto
        {
            AppId = app.AppId,
            WorkspaceId = workspaceId,
            Name = app.Name,
            Description = app.Description,
            Status = app.Status,
            Modules = modules
        };
    }

    public async Task<IReadOnlyList<MicroflowModuleAssetDto>> ListModulesAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        _ = await LoadAppAssetAsync(appId, workspaceId, cancellationToken);
        var catalog = await _metadataService.GetCatalogAsync(
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = workspaceId,
                IncludeSystem = true,
                IncludeArchived = false
            },
            cancellationToken);

        var domainModelSummaries = _domainModelService is null
            ? Array.Empty<MendixDomainModelModuleSummaryDto>()
            : await _domainModelService.ListModuleSummariesAsync(appId, workspaceId, cancellationToken);

        var modules = catalog.Modules
            .Select(module => CreateModuleAsset(module, catalog))
            .Where(module => !string.IsNullOrWhiteSpace(module.ModuleId))
            .GroupBy(module => module.ModuleId, StringComparer.OrdinalIgnoreCase)
            .Select(group => MergeDomainModelEntities(group.First(), domainModelSummaries.FirstOrDefault(item => string.Equals(item.ModuleId, group.Key, StringComparison.OrdinalIgnoreCase))))
            .ToArray();

        return modules.Length > 0
            ? modules
            : new[]
            {
                CreateFallbackModuleAsset(catalog)
            };
    }

    private static MicroflowModuleAssetDto CreateModuleAsset(MetadataModuleDto module, MicroflowMetadataCatalogDto catalog)
    {
        var moduleId = string.IsNullOrWhiteSpace(module.Id) ? module.Name : module.Id;
        var moduleName = string.IsNullOrWhiteSpace(module.Name) ? module.Id : module.Name;
        var qualifiedName = string.IsNullOrWhiteSpace(module.QualifiedName) ? moduleName : module.QualifiedName;

        var pages = catalog.Pages
            .Where(page => BelongsToModule(page.ModuleName, moduleId, moduleName, qualifiedName))
            .Select(page => new MicroflowPageAssetSummaryDto
            {
                Id = page.Id,
                Name = page.Name,
                QualifiedName = page.QualifiedName,
                ModuleName = page.ModuleName,
                Description = page.Description,
                ParameterCount = page.Parameters.Count
            })
            .OrderBy(page => page.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var workflows = catalog.Workflows
            .Where(workflow => BelongsToModule(workflow.ModuleName, moduleId, moduleName, qualifiedName))
            .Select(workflow => new MicroflowWorkflowAssetSummaryDto
            {
                Id = workflow.Id,
                Name = workflow.Name,
                QualifiedName = workflow.QualifiedName,
                ModuleName = workflow.ModuleName,
                ContextEntityQualifiedName = workflow.ContextEntityQualifiedName,
                Description = workflow.Description
            })
            .OrderBy(workflow => workflow.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entities = catalog.Entities
            .Where(entity => BelongsToModule(entity.ModuleId, moduleId, moduleName, qualifiedName) || BelongsToModule(entity.ModuleName, moduleId, moduleName, qualifiedName))
            .Select(entity => new MicroflowDomainEntitySummaryDto
            {
                Id = entity.Id,
                Name = entity.Name,
                QualifiedName = entity.QualifiedName,
                ModuleName = entity.ModuleName,
                AttributeCount = entity.Attributes.Count,
                AssociationCount = entity.Associations.Count,
                IsPersistable = entity.IsPersistable
            })
            .OrderBy(entity => entity.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MicroflowModuleAssetDto
        {
            ModuleId = moduleId,
            Name = moduleName,
            QualifiedName = qualifiedName,
            Description = module.Description,
            Pages = pages,
            Workflows = workflows,
            Entities = entities,
            Security = new MicroflowSecurityAssetSummaryDto
            {
                ModuleId = moduleId,
                ModuleName = moduleName,
                EntityAccessCount = entities.Count(entity => entity.IsPersistable),
                Readonly = true
            }
        };
    }

    private static MicroflowModuleAssetDto CreateFallbackModuleAsset(MicroflowMetadataCatalogDto catalog)
    {
        var module = new MetadataModuleDto
        {
            Id = "main",
            Name = "Main",
            QualifiedName = "Main",
            Description = "Default module"
        };
        return CreateModuleAsset(module, catalog);
    }

    private static bool BelongsToModule(string? candidate, params string[] moduleAliases)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return moduleAliases.Any(alias =>
            !string.IsNullOrWhiteSpace(alias) &&
            string.Equals(candidate, alias, StringComparison.OrdinalIgnoreCase));
    }

    private static MicroflowModuleAssetDto MergeDomainModelEntities(MicroflowModuleAssetDto module, MendixDomainModelModuleSummaryDto? summary)
    {
        if (summary is null || summary.Entities.Count == 0)
        {
            return module;
        }

        var entities = summary.Entities
            .Select(entity => new MicroflowDomainEntitySummaryDto
            {
                Id = entity.EntityId,
                Name = entity.Name,
                QualifiedName = entity.QualifiedName,
                ModuleName = module.Name,
                AttributeCount = entity.Attributes.Count,
                AssociationCount = 0,
                IsPersistable = entity.Persistable
            })
            .OrderBy(entity => entity.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return module with
        {
            Entities = entities,
            Security = (module.Security ?? new MicroflowSecurityAssetSummaryDto()) with
            {
                EntityAccessCount = entities.Count(entity => entity.IsPersistable)
            }
        };
    }

    private async Task<MicroflowAppAssetDescriptor> LoadAppAssetAsync(string appId, string workspaceId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(appId, out var id))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流应用不存在或 appId 无效。", 404);
        }

        var tenantId = _requestContextAccessor.Current.TenantId;
        var aiAppQuery = _db.Queryable<AiApp>().Where(app => app.Id == id);
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsedTenantId))
        {
            aiAppQuery = aiAppQuery.Where(app => app.TenantIdValue == parsedTenantId);
        }

        var aiApp = await aiAppQuery.FirstAsync(cancellationToken);
        if (aiApp is not null)
        {
            if (!long.TryParse(workspaceId, out var workspaceIdValue) || aiApp.WorkspaceId != workspaceIdValue)
            {
                throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "该应用不属于当前工作区。", 403);
            }

            return new MicroflowAppAssetDescriptor(
                aiApp.Id.ToString(),
                aiApp.Name,
                aiApp.Description,
                aiApp.Status == AiAppStatus.Published ? "published" : "draft");
        }

        var lowCodeAppQuery = _db.Queryable<AppDefinition>().Where(app => app.Id == id);
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsedLowCodeTenantId))
        {
            lowCodeAppQuery = lowCodeAppQuery.Where(app => app.TenantIdValue == parsedLowCodeTenantId);
        }

        var lowCodeApp = await lowCodeAppQuery.FirstAsync(cancellationToken);
        if (lowCodeApp is null)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流应用不存在。", 404);
        }

        if (!string.Equals(lowCodeApp.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "该应用不属于当前工作区。", 403);
        }

        return new MicroflowAppAssetDescriptor(
            lowCodeApp.Id.ToString(),
            lowCodeApp.DisplayName,
            lowCodeApp.Description,
            lowCodeApp.Status);
    }

    private sealed record MicroflowAppAssetDescriptor(
        string AppId,
        string Name,
        string? Description,
        string Status);
}
