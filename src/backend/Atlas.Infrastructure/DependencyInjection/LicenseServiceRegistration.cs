using Atlas.Application.License.Abstractions;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.License;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class LicenseServiceRegistration
{
    public static IServiceCollection AddLicenseInfrastructure(this IServiceCollection services)
    {
        // Repository
        services.AddScoped<ILicenseRepository, LicenseRepository>();

        // Signature & fingerprint (singleton: 公钥内嵌，指纹缓存进程级)
        services.AddSingleton<ILicenseSignatureService, LicenseSignatureService>();
        services.AddSingleton<IMachineFingerprintService, MachineFingerprintService>();
        services.AddSingleton<ILicenseStateSealService, LicenseStateSealService>();

        // Guard service (singleton: 内存缓存授权状态)
        services.AddSingleton<LicenseGuardService>();
        services.AddSingleton<ILicenseService>(sp => sp.GetRequiredService<LicenseGuardService>());

        // Validation service (singleton: 仅依赖无状态单例服务)
        services.AddSingleton<LicenseValidationService>();

        // Activation service (scoped: 需要 ILicenseRepository)
        services.AddScoped<ILicenseActivationService, LicenseActivationService>();

        return services;
    }
}
