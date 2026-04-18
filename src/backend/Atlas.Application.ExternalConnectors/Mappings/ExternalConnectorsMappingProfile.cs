using Atlas.Application.ExternalConnectors.Models;
using Atlas.Domain.ExternalConnectors.Entities;
using AutoMapper;

namespace Atlas.Application.ExternalConnectors.Mappings;

public sealed class ExternalConnectorsMappingProfile : Profile
{
    public ExternalConnectorsMappingProfile()
    {
        CreateMap<ExternalIdentityProvider, ExternalIdentityProviderResponse>()
            .ForMember(dest => dest.SecretMasked, opt => opt.MapFrom(src => SecretMasker.Mask(src.SecretEncrypted)));

        CreateMap<ExternalIdentityProvider, ExternalIdentityProviderListItem>();

        CreateMap<ExternalIdentityBinding, ExternalIdentityBindingResponse>();

        CreateMap<ExternalIdentityBinding, ExternalIdentityBindingListItem>();
    }
}

internal static class SecretMasker
{
    public static string Mask(string? secretEncrypted)
    {
        if (string.IsNullOrEmpty(secretEncrypted))
        {
            return string.Empty;
        }
        if (secretEncrypted.Length <= 4)
        {
            return new string('*', secretEncrypted.Length);
        }
        return string.Concat(secretEncrypted[..4], new string('*', Math.Min(8, secretEncrypted.Length - 4)));
    }
}
