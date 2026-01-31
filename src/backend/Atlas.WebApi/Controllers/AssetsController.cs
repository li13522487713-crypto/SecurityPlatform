using AutoMapper;
using FluentValidation;
using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Assets.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Authorization;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/assets")]
public sealed class AssetsController : ControllerBase
{
    private readonly IAssetQueryService _assetQueryService;
    private readonly IAssetCommandService _assetCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;
    private readonly IValidator<Asset> _entityValidator;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;

    public AssetsController(
        IAssetQueryService assetQueryService,
        IAssetCommandService assetCommandService,
        ITenantProvider tenantProvider,
        IMapper mapper,
        IValidator<Asset> entityValidator,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor)
    {
        _assetQueryService = assetQueryService;
        _assetCommandService = assetCommandService;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
        _entityValidator = entityValidator;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _assetQueryService.QueryAssetsAsync(request, tenantId, cancellationToken);
        var payload = ApiResponse<PagedResult<AssetListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AssetsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AssetCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var asset = _mapper.Map<Asset>(request, opt =>
        {
            opt.Items["TenantId"] = tenantId;
            opt.Items["Id"] = _idGeneratorAccessor.NextId();
        });

        _entityValidator.ValidateAndThrow(asset);

        var id = await _assetCommandService.CreateAsync(asset, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}




