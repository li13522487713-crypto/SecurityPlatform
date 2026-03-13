using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiProductCategoryItem(
    long Id,
    string Name,
    string Code,
    string? Description,
    int SortOrder,
    bool IsEnabled);

public sealed record AiProductCategoryCreateRequest(
    string Name,
    string Code,
    string? Description,
    int SortOrder);

public sealed record AiProductCategoryUpdateRequest(
    string Name,
    string Code,
    string? Description,
    int SortOrder);

public sealed record AiMarketplaceProductListItem(
    long Id,
    long CategoryId,
    string CategoryName,
    string Name,
    string? Summary,
    string? Icon,
    AiMarketplaceProductType ProductType,
    AiMarketplaceProductStatus Status,
    string Version,
    int DownloadCount,
    int FavoriteCount,
    bool IsFavorited,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiMarketplaceProductDetail(
    long Id,
    long CategoryId,
    string CategoryName,
    string Name,
    string? Summary,
    string? Description,
    string? Icon,
    string[] Tags,
    AiMarketplaceProductType ProductType,
    long? SourceResourceId,
    AiMarketplaceProductStatus Status,
    string Version,
    int DownloadCount,
    int FavoriteCount,
    bool IsFavorited,
    long PublisherUserId,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiMarketplaceProductCreateRequest(
    long CategoryId,
    string Name,
    string? Summary,
    string? Description,
    string? Icon,
    string[] Tags,
    AiMarketplaceProductType ProductType,
    long? SourceResourceId);

public sealed record AiMarketplaceProductUpdateRequest(
    long CategoryId,
    string Name,
    string? Summary,
    string? Description,
    string? Icon,
    string[] Tags,
    AiMarketplaceProductType ProductType,
    long? SourceResourceId);

public sealed record AiMarketplaceProductPublishRequest(string Version);
