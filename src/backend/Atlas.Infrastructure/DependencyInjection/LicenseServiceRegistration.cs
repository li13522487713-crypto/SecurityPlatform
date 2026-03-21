using Atlas.Application.License.Abstractions;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.License;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class LicenseServiceRegistration
{
    public static IServiceCollection AddLicenseInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Repository（DevMode 下 License 控制器仍可用，Repository 保持注册）
        services.AddScoped<ILicenseRepository, LicenseRepository>();

        // Signature & fingerprint (singleton: 公钥内嵌，指纹缓存进程级)
        services.AddSingleton<ILicenseSignatureService, LicenseSignatureService>();
        services.AddSingleton<IMachineFingerprintService, MachineFingerprintService>();
        services.AddSingleton<ILicenseStateSealService, LicenseStateSealService>();

        // Validation service (singleton: 仅依赖无状态单例服务)
        services.AddSingleton<LicenseValidationService>();

        // Activation service (scoped: 需要 ILicenseRepository)
        services.AddScoped<LicenseTenantAdminProvisionService>();
        services.AddScoped<ILicenseActivationService, LicenseActivationService>();

        var devModeEnabled = configuration.GetValue<bool>("License:DevMode:Enabled");
        if (devModeEnabled)
        {
            // 调试模式：注册固定 Active 状态服务，跳过证书校验
            services.Configure<LicenseDevModeOptions>(configuration.GetSection("License:DevMode"));
            services.AddSingleton<ILicenseService, DevLicenseService>();
        }
        else
        {
            // 生产模式：内存缓存真实授权状态
            services.AddSingleton<LicenseGuardService>();
            services.AddSingleton<ILicenseService>(sp => sp.GetRequiredService<LicenseGuardService>());
        }

        return services;
    }
}
