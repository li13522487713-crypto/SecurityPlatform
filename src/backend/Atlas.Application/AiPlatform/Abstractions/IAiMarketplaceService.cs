using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiMarketplaceService
{
    Task<IReadOnlyList<AiProductCategoryItem>> GetCategoriesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<long> CreateCategoryAsync(
        TenantId tenantId,
        AiProductCategoryCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateCategoryAsync(
        TenantId tenantId,
        long categoryId,
        AiProductCategoryUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteCategoryAsync(
        TenantId tenantId,
        long categoryId,
        CancellationToken cancellationToken);

    Task<PagedResult<AiMarketplaceProductListItem>> GetProductsPagedAsync(
        TenantId tenantId,
        long currentUserId,
        string? keyword,
        long? categoryId,
        AiMarketplaceProductType? productType,
        AiMarketplaceProductStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiMarketplaceProductDetail?> GetProductByIdAsync(
        TenantId tenantId,
        long currentUserId,
        long productId,
        CancellationToken cancellationToken);

    Task<long> CreateProductAsync(
        TenantId tenantId,
        long currentUserId,
        AiMarketplaceProductCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateProductAsync(
        TenantId tenantId,
        long productId,
        AiMarketplaceProductUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteProductAsync(
        TenantId tenantId,
        long productId,
        CancellationToken cancellationToken);

    Task PublishProductAsync(
        TenantId tenantId,
        long productId,
        AiMarketplaceProductPublishRequest request,
        CancellationToken cancellationToken);

    Task FavoriteProductAsync(
        TenantId tenantId,
        long currentUserId,
        long productId,
        CancellationToken cancellationToken);

    Task UnfavoriteProductAsync(
        TenantId tenantId,
        long currentUserId,
        long productId,
        CancellationToken cancellationToken);

    Task MarkDownloadedAsync(
        TenantId tenantId,
        long productId,
        CancellationToken cancellationToken);
}
