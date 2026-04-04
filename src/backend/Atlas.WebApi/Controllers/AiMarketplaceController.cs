using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/ai-marketplace")]
[Authorize]
[PlatformOnly]
public sealed class AiMarketplaceController : ControllerBase
{
    private readonly IAiMarketplaceService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AiProductCategoryCreateRequest> _createCategoryValidator;
    private readonly IValidator<AiProductCategoryUpdateRequest> _updateCategoryValidator;
    private readonly IValidator<AiMarketplaceProductCreateRequest> _createProductValidator;
    private readonly IValidator<AiMarketplaceProductUpdateRequest> _updateProductValidator;
    private readonly IValidator<AiMarketplaceProductPublishRequest> _publishProductValidator;

    public AiMarketplaceController(
        IAiMarketplaceService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AiProductCategoryCreateRequest> createCategoryValidator,
        IValidator<AiProductCategoryUpdateRequest> updateCategoryValidator,
        IValidator<AiMarketplaceProductCreateRequest> createProductValidator,
        IValidator<AiMarketplaceProductUpdateRequest> updateProductValidator,
        IValidator<AiMarketplaceProductPublishRequest> publishProductValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createCategoryValidator = createCategoryValidator;
        _updateCategoryValidator = updateCategoryValidator;
        _createProductValidator = createProductValidator;
        _updateProductValidator = updateProductValidator;
        _publishProductValidator = publishProductValidator;
    }

    [HttpGet("categories")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiProductCategoryItem>>>> GetCategories(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetCategoriesAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiProductCategoryItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("categories")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateCategory(
        [FromBody] AiProductCategoryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        _createCategoryValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreateCategoryAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("categories/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateCategory(
        long id,
        [FromBody] AiProductCategoryUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        _updateCategoryValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateCategoryAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("categories/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCategory(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteCategoryAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("products")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiMarketplaceProductListItem>>>> GetProducts(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] long? categoryId = null,
        [FromQuery] AiMarketplaceProductType? productType = null,
        [FromQuery] AiMarketplaceProductStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetProductsPagedAsync(
            tenantId,
            currentUser.UserId,
            keyword,
            categoryId,
            productType,
            status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AiMarketplaceProductListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("products/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceView)]
    public async Task<ActionResult<ApiResponse<AiMarketplaceProductDetail>>> GetProductById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetProductByIdAsync(tenantId, currentUser.UserId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiMarketplaceProductDetail>.Fail(
                ErrorCodes.NotFound,
                "市场商品不存在",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiMarketplaceProductDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("products")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateProduct(
        [FromBody] AiMarketplaceProductCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        _createProductValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var id = await _service.CreateProductAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("products/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProduct(
        long id,
        [FromBody] AiMarketplaceProductUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        _updateProductValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateProductAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("products/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProduct(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteProductAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("products/{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AiMarketplacePublish)]
    public async Task<ActionResult<ApiResponse<object>>> PublishProduct(
        long id,
        [FromBody] AiMarketplaceProductPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        _publishProductValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.PublishProductAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("products/{id:long}/favorite")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceView)]
    public async Task<ActionResult<ApiResponse<object>>> FavoriteProduct(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.FavoriteProductAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), Favorited = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("products/{id:long}/favorite")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceView)]
    public async Task<ActionResult<ApiResponse<object>>> UnfavoriteProduct(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.UnfavoriteProductAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), Favorited = false }, HttpContext.TraceIdentifier));
    }

    [HttpPost("products/{id:long}/download")]
    [Authorize(Policy = PermissionPolicies.AiMarketplaceView)]
    public async Task<ActionResult<ApiResponse<object>>> MarkProductDownloaded(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.MarkDownloadedAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
