using AutoMapper;
using Atlas.Application.Identity.Models;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Mappings;

public sealed class IdentityMappingProfile : Profile
{
    public IdentityMappingProfile()
    {
        CreateMap<UserAccount, UserListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<Role, RoleListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<Permission, PermissionListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<Permission, PermissionDetail>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<Department, DepartmentListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<Position, PositionListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<Menu, MenuListItem>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()));
    }
}
