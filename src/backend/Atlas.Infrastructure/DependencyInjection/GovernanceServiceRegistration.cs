using Atlas.Application.Governance.Abstractions;
using Atlas.Infrastructure.Services.Governance;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class GovernanceServiceRegistration
{
    public static IServiceCollection AddGovernanceInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<ILicenseGrantService, LicenseGrantService>();
        services.AddScoped<IToolAuthorizationService, ToolAuthorizationService>();
        services.AddScoped<IDlpService, DlpService>();
        return services;
    }
}
