using Atlas.AppHost.Sdk.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.AppHost.Sdk.Hosting;

public static class AppHostBuilderExtensions
{
    public static IServiceCollection AddAtlasAppHostSupport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AppSessionOptions>(configuration.GetSection(AppSessionOptions.SectionName));
        services.AddSingleton<AppInstanceConfigurationLoader>();
        return services;
    }

    public static WebApplication UseAtlasAppHostDefaults(this WebApplication app)
    {
        app.UseRouting();
        return app;
    }
}
