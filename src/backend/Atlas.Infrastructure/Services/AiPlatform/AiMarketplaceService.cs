using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiMarketplaceService : IAiMarketplaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiProductCategoryRepository _categoryRepository;
    private readonly AiMarketplaceProductRepository _productRepository;
    private readonly AiMarketplaceFavoriteRepository _favoriteRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public AiMarketplaceService(
        AiProductCategoryRepository categoryRepository,
        AiMarketplaceProductRepository productRepository,
        AiMarketplaceFavoriteRepository favoriteRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _favoriteRepository = favoriteRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<AiProductCategoryItem>> GetCategoriesAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetEnabledAsync(tenantId, cancellationToken);
        return categories
            .Select(x => new AiProductCategoryItem(x.Id, x.Name, x.Code, x.Description, x.SortOrder, x.IsEnabled))
            .ToArray();
    }

    public async Task<long> CreateCategoryAsync(
        TenantId tenantId,
        AiProductCategoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim();
        if (await _categoryRepository.ExistsByCodeAsync(tenantId, normalizedCode, null, cancellationToken))
        {
            throw new BusinessException("分类编码已存在。", ErrorCodes.ValidationError);
        }

        var entity = new AiProductCategory(
            tenantId,
            request.Name.Trim(),
            normalizedCode,
            request.Description?.Trim(),
            request.SortOrder,
            _idGeneratorAccessor.NextId());
        await _categoryRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateCategoryAsync(
        TenantId tenantId,
        long categoryId,
        AiProductCategoryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetCategoryOrThrowAsync(tenantId, categoryId, cancellationToken);
        var normalizedCode = request.Code.Trim();
        if (await _categoryRepository.ExistsByCodeAsync(tenantId, normalizedCode, categoryId, cancellationToken))
        {
            throw new BusinessException("分类编码已存在。", ErrorCodes.ValidationError);
        }

        category.Update(
            request.Name.Trim(),
            normalizedCode,
            request.Description?.Trim(),
            request.SortOrder);
        await _categoryRepository.UpdateAsync(category, cancellationToken);
    }

    public async Task DeleteCategoryAsync(TenantId tenantId, long categoryId, CancellationToken cancellationToken)
    {
        var category = await GetCategoryOrThrowAsync(tenantId, categoryId, cancellationToken);
        if (await _categoryRepository.IsCategoryUsedByProductsAsync(tenantId, categoryId, cancellationToken))
        {
            throw new BusinessException("分类下仍有商品，无法删除。", ErrorCodes.ValidationError);
        }

        category.Disable();
        await _categoryRepository.UpdateAsync(category, cancellationToken);
    }

    public async Task<PagedResult<AiMarketplaceProductListItem>> GetProductsPagedAsync(
        TenantId tenantId,
        long currentUserId,
        string? keyword,
        long? categoryId,
        AiMarketplaceProductType? productType,
        AiMarketplaceProductStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _productRepository.GetPagedAsync(
            tenantId,
            keyword,
            categoryId,
            productType,
            status,
            pageIndex,
            pageSize,
            cancellationToken);

        var categoryIds = items.Select(x => x.CategoryId).Distinct().ToArray();
        var categories = await _categoryRepository.GetByIdsAsync(tenantId, categoryIds, cancellationToken);
        var categoryMap = categories.ToDictionary(x => x.Id, x => x.Name);

        var productIds = items.Select(x => x.Id).ToArray();
        var favoriteProductIds = await _favoriteRepository.GetProductIdsByUserAsync(
            tenantId,
            currentUserId,
            productIds,
            cancellationToken);
        var favoriteSet = favoriteProductIds.ToHashSet();

        var data = items.Select(x => new AiMarketplaceProductListItem(
                x.Id,
                x.CategoryId,
                categoryMap.TryGetValue(x.CategoryId, out var categoryName) ? categoryName : "-",
                x.Name,
                x.Summary,
                x.Icon,
                x.ProductType,
                x.Status,
                x.Version,
                x.DownloadCount,
                x.FavoriteCount,
                favoriteSet.Contains(x.Id),
                x.PublishedAt,
                x.CreatedAt,
                x.UpdatedAt))
            .ToArray();
        return new PagedResult<AiMarketplaceProductListItem>(data, total, pageIndex, pageSize);
    }

    public async Task<AiMarketplaceProductDetail?> GetProductByIdAsync(
        TenantId tenantId,
        long currentUserId,
        long productId,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.FindByIdAsync(tenantId, productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var category = await _categoryRepository.FindByIdAsync(tenantId, product.CategoryId, cancellationToken);
        var isFavorited = await _favoriteRepository.ExistsAsync(tenantId, currentUserId, productId, cancellationToken);
        return new AiMarketplaceProductDetail(
            product.Id,
            product.CategoryId,
            category?.Name ?? "-",
            product.Name,
            product.Summary,
            product.Description,
            product.Icon,
            ParseTags(product.TagsJson),
            product.ProductType,
            product.SourceResourceId,
            product.Status,
            product.Version,
            product.DownloadCount,
            product.FavoriteCount,
            isFavorited,
            product.PublisherUserId,
            product.PublishedAt,
            product.CreatedAt,
            product.UpdatedAt);
    }

    public async Task<long> CreateProductAsync(
        TenantId tenantId,
        long currentUserId,
        AiMarketplaceProductCreateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCategoryExistsAsync(tenantId, request.CategoryId, cancellationToken);
        var normalizedName = request.Name.Trim();
        if (await _productRepository.ExistsByNameAsync(tenantId, normalizedName, null, cancellationToken))
        {
            throw new BusinessException("商品名称已存在。", ErrorCodes.ValidationError);
        }

        var entity = new AiMarketplaceProduct(
            tenantId,
            request.CategoryId,
            normalizedName,
            request.Summary?.Trim(),
            request.Description?.Trim(),
            request.Icon?.Trim(),
            SerializeTags(request.Tags),
            request.ProductType,
            request.SourceResourceId,
            currentUserId,
            _idGeneratorAccessor.NextId());
        await _productRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateProductAsync(
        TenantId tenantId,
        long productId,
        AiMarketplaceProductUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrowAsync(tenantId, productId, cancellationToken);
        await EnsureCategoryExistsAsync(tenantId, request.CategoryId, cancellationToken);

        var normalizedName = request.Name.Trim();
        if (await _productRepository.ExistsByNameAsync(tenantId, normalizedName, productId, cancellationToken))
        {
            throw new BusinessException("商品名称已存在。", ErrorCodes.ValidationError);
        }

        product.Update(
            request.CategoryId,
            normalizedName,
            request.Summary?.Trim(),
            request.Description?.Trim(),
            request.Icon?.Trim(),
            SerializeTags(request.Tags),
            request.ProductType,
            request.SourceResourceId);
        await _productRepository.UpdateAsync(product, cancellationToken);
    }

    public async Task DeleteProductAsync(TenantId tenantId, long productId, CancellationToken cancellationToken)
    {
        await GetProductOrThrowAsync(tenantId, productId, cancellationToken);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _favoriteRepository.DeleteByProductIdAsync(tenantId, productId, cancellationToken);
            await _productRepository.DeleteAsync(tenantId, productId, cancellationToken);
        }, cancellationToken);
    }

    public async Task PublishProductAsync(
        TenantId tenantId,
        long productId,
        AiMarketplaceProductPublishRequest request,
        CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrowAsync(tenantId, productId, cancellationToken);
        product.Publish(request.Version.Trim());
        await _productRepository.UpdateAsync(product, cancellationToken);
    }

    public async Task FavoriteProductAsync(TenantId tenantId, long currentUserId, long productId, CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrowAsync(tenantId, productId, cancellationToken);
        if (await _favoriteRepository.ExistsAsync(tenantId, currentUserId, productId, cancellationToken))
        {
            return;
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var favorite = new AiMarketplaceFavorite(
                tenantId,
                productId,
                currentUserId,
                _idGeneratorAccessor.NextId());
            await _favoriteRepository.AddAsync(favorite, cancellationToken);
            product.IncreaseFavorite();
            await _productRepository.UpdateAsync(product, cancellationToken);
        }, cancellationToken);
    }

    public async Task UnfavoriteProductAsync(TenantId tenantId, long currentUserId, long productId, CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrowAsync(tenantId, productId, cancellationToken);
        if (!await _favoriteRepository.ExistsAsync(tenantId, currentUserId, productId, cancellationToken))
        {
            return;
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _favoriteRepository.DeleteByUserAndProductAsync(tenantId, currentUserId, productId, cancellationToken);
            product.DecreaseFavorite();
            await _productRepository.UpdateAsync(product, cancellationToken);
        }, cancellationToken);
    }

    public async Task MarkDownloadedAsync(TenantId tenantId, long productId, CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrowAsync(tenantId, productId, cancellationToken);
        product.IncreaseDownload();
        await _productRepository.UpdateAsync(product, cancellationToken);
    }

    private async Task<AiProductCategory> GetCategoryOrThrowAsync(TenantId tenantId, long categoryId, CancellationToken cancellationToken)
    {
        return await _categoryRepository.FindByIdAsync(tenantId, categoryId, cancellationToken)
            ?? throw new BusinessException("分类不存在。", ErrorCodes.NotFound);
    }

    private async Task EnsureCategoryExistsAsync(TenantId tenantId, long categoryId, CancellationToken cancellationToken)
    {
        var category = await GetCategoryOrThrowAsync(tenantId, categoryId, cancellationToken);
        if (!category.IsEnabled)
        {
            throw new BusinessException("分类已停用。", ErrorCodes.ValidationError);
        }
    }

    private async Task<AiMarketplaceProduct> GetProductOrThrowAsync(TenantId tenantId, long productId, CancellationToken cancellationToken)
    {
        return await _productRepository.FindByIdAsync(tenantId, productId, cancellationToken)
            ?? throw new BusinessException("市场商品不存在。", ErrorCodes.NotFound);
    }

    private static string SerializeTags(string[] tags)
    {
        var normalized = tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static string[] ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(tagsJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
