using AutoMapper;
using FluentValidation;
using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Assets.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("assets")]
public sealed class AssetsController : ControllerBase
{
    private readonly IAssetQueryService _assetQueryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;
    private readonly IValidator<Asset> _entityValidator;
    private readonly Atlas.Core.Abstractions.IIdGenerator _idGenerator;

    public AssetsController(
        IAssetQueryService assetQueryService,
        ITenantProvider tenantProvider,
        IMapper mapper,
        IValidator<Asset> entityValidator,
        Atlas.Core.Abstractions.IIdGenerator idGenerator)
    {
        _assetQueryService = assetQueryService;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
        _entityValidator = entityValidator;
        _idGenerator = idGenerator;
    }

    [HttpGet]
    [Authorize]
    public ActionResult<ApiResponse<PagedResult<AssetListItem>>> Get([FromQuery] PagedRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = _assetQueryService.QueryAssets(request, tenantId);
        var payload = ApiResponse<PagedResult<AssetListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPost]
    [Authorize]
    public ActionResult<ApiResponse<object>> Create([FromBody] AssetCreateRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var asset = _mapper.Map<Asset>(request, opt =>
        {
            opt.Items["TenantId"] = tenantId;
            opt.Items["Id"] = _idGenerator.NextId();
        });

        _entityValidator.ValidateAndThrow(asset);

        var payload = ApiResponse<object>.Ok(new { Id = asset.Id.ToString() }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}