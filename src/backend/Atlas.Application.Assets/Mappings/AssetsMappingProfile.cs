using AutoMapper;
using Atlas.Application.Assets.Models;
using Atlas.Domain.Assets.Entities;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Assets.Mappings;

public sealed class AssetsMappingProfile : Profile
{
    public AssetsMappingProfile()
    {
        CreateMap<Asset, AssetListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<AssetCreateRequest, Asset>()
            .ConstructUsing((src, ctx) =>
            {
                var tenantId = (TenantId)ctx.Items["TenantId"];
                var id = (long)ctx.Items["Id"];
                return new Asset(tenantId, src.Name, id);
            });
    }
}