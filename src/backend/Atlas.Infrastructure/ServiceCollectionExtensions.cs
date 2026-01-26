using Atlas.Application.Abstractions;
using Atlas.Application.Assets.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthTokenService, JwtAuthTokenService>();
        services.AddScoped<IAssetQueryService, AssetQueryService>();

        services.AddScoped<ISqlSugarClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var config = new ConnectionConfig
            {
                ConnectionString = options.ConnectionString,
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            };

            return new SqlSugarScope(config);
        });

        return services;
    }
}