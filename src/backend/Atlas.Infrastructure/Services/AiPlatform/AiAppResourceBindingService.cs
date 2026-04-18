using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiAppResourceBindingService : IAiAppResourceBindingService
{
    /// <summary>已支持的 ResourceType（与 AppResourceCatalogService 的 AllSupportedTypes 对齐）。</summary>
    private static readonly HashSet<string> SupportedResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "knowledge",
        "database",
        "workflow",
        "chatflow",
        "plugin",
        "prompt-template",
        "variable",
        "trigger"
    };

    private readonly AiAppResourceBindingRepository _bindingRepository;
    private readonly AiAppRepository _appRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AiAppResourceBindingService(
        AiAppResourceBindingRepository bindingRepository,
        AiAppRepository appRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        AiDatabaseRepository databaseRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _bindingRepository = bindingRepository;
        _appRepository = appRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _databaseRepository = databaseRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<IReadOnlyList<AiAppResourceBindingDto>> ListByAppAsync(
        TenantId tenantId,
        long appId,
        string? resourceType,
        CancellationToken cancellationToken)
    {
        await EnsureAppExistsAsync(tenantId, appId, cancellationToken);
        var entities = await _bindingRepository.ListByAppAsync(tenantId, appId, resourceType, cancellationToken);
        return entities.Select(Map).ToList();
    }

    public async Task<long> BindAsync(
        TenantId tenantId,
        long appId,
        AiAppResourceBindingCreateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeResourceType(request.ResourceType);
        if (request.ResourceId <= 0)
        {
            throw new BusinessException("ResourceId 无效。", ErrorCodes.ValidationError);
        }

        await EnsureAppExistsAsync(tenantId, appId, cancellationToken);
        await EnsureResourceExistsAsync(tenantId, normalizedType, request.ResourceId, cancellationToken);

        var existing = await _bindingRepository.FindAsync(tenantId, appId, normalizedType, request.ResourceId, cancellationToken);
        if (existing is not null)
        {
            existing.Update(request.Role, request.DisplayOrder, request.ConfigJson);
            await _bindingRepository.UpdateAsync(existing, cancellationToken);
            return existing.Id;
        }

        var entity = new AiAppResourceBinding(
            tenantId,
            appId,
            normalizedType,
            request.ResourceId,
            request.Role,
            request.DisplayOrder,
            request.ConfigJson,
            _idGeneratorAccessor.NextId());
        await _bindingRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long appId,
        long bindingId,
        AiAppResourceBindingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAppExistsAsync(tenantId, appId, cancellationToken);
        var entity = await _bindingRepository.FindByIdAsync(tenantId, bindingId, cancellationToken)
            ?? throw new BusinessException("绑定不存在。", ErrorCodes.NotFound);
        if (entity.AppId != appId)
        {
            throw new BusinessException("绑定不属于此应用。", ErrorCodes.ValidationError);
        }
        entity.Update(request.Role, request.DisplayOrder, request.ConfigJson);
        await _bindingRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UnbindAsync(
        TenantId tenantId,
        long appId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeResourceType(resourceType);
        await EnsureAppExistsAsync(tenantId, appId, cancellationToken);
        await _bindingRepository.DeleteByAppAndResourceAsync(tenantId, appId, normalizedType, resourceId, cancellationToken);
    }

    private static AiAppResourceBindingDto Map(AiAppResourceBinding entity)
        => new(
            entity.Id,
            entity.AppId,
            entity.ResourceType,
            entity.ResourceId,
            entity.Role,
            entity.DisplayOrder,
            entity.ConfigJson,
            entity.CreatedAt,
            entity.UpdatedAt);

    private async Task EnsureAppExistsAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var app = await _appRepository.FindByIdAsync(tenantId, appId, cancellationToken);
        if (app is null)
        {
            throw new BusinessException("应用不存在。", ErrorCodes.NotFound);
        }
    }

    private async Task EnsureResourceExistsAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        switch (resourceType)
        {
            case "knowledge":
            {
                var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, resourceId, cancellationToken);
                if (kb is null)
                {
                    throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
                }
                break;
            }
            case "database":
            {
                var db = await _databaseRepository.FindByIdAsync(tenantId, resourceId, cancellationToken);
                if (db is null)
                {
                    throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);
                }
                break;
            }
            default:
                // 其余资源类型暂不强校验存在（后续 X1/X3 引入统一资源探针后再补）。
                break;
        }
    }

    private static string NormalizeResourceType(string? resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new BusinessException("ResourceType 不能为空。", ErrorCodes.ValidationError);
        }
        var normalized = resourceType.Trim().ToLowerInvariant();
        if (!SupportedResourceTypes.Contains(normalized))
        {
            throw new BusinessException($"不支持的资源类型：{resourceType}。", ErrorCodes.ValidationError);
        }
        return normalized;
    }
}
