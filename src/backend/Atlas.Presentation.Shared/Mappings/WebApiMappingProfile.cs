using AutoMapper;
using Atlas.Application.Models;
using Atlas.Presentation.Shared.Models;

namespace Atlas.Presentation.Shared.Mappings;

public sealed class WebApiMappingProfile : Profile
{
    public WebApiMappingProfile()
    {
        CreateMap<AuthTokenViewModel, AuthTokenRequest>();
        CreateMap<AuthRefreshViewModel, AuthRefreshRequest>();
        CreateMap<ChangePasswordViewModel, ChangePasswordRequest>();
        CreateMap<RegisterViewModel, RegisterRequest>();
    }
}
