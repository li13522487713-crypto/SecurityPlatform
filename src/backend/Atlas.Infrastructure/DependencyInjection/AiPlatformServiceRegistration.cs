using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiPlatformServiceRegistration
{
    /// <summary>
    /// 聚合注册：Core + Design + Runtime。
    /// AppHost 使用此方法注册全部 AI 服务。
    /// </summary>
    public static IServiceCollection AddAiPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAiCoreInfrastructure(configuration);
        services.AddAiPlatformDesignInfrastructure(configuration);
        services.AddAiRuntimeInfrastructure();
        return services;
    }
}
