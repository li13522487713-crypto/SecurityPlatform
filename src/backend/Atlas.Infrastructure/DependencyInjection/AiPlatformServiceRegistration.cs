using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiPlatformServiceRegistration
{
    /// <summary>
    /// 聚合注册：Core + Design + Runtime。
    /// PlatformHost 使用此方法注册全部 AI 服务。
    /// AppHost 应使用 AddAiCoreInfrastructure + AddAiRuntimeInfrastructure（不含 Design）。
    /// </summary>
    public static IServiceCollection AddAiPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAiCoreInfrastructure(configuration);
        services.AddAiPlatformDesignInfrastructure(configuration);
        services.AddAiRuntimeInfrastructure();
        return services;
    }
}
