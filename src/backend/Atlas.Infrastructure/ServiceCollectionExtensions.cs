using Atlas.Core.Tenancy;
using Atlas.Infrastructure.DependencyInjection;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register modular services
        services.AddCoreInfrastructure(configuration);
        services.AddAssetInfrastructure();
        services.AddDynamicTableInfrastructure();
        services.AddApprovalInfrastructure();
        services.AddWorkflowInfrastructure();
        services.AddLowCodeInfrastructure();

        // 注册多数据源相关服务
        services.AddScoped<Atlas.Infrastructure.Repositories.TenantDataSourceRepository>();
        services.AddScoped<Atlas.Application.System.Abstractions.ITenantDbConnectionFactory,
            Atlas.Infrastructure.Services.TenantDbConnectionFactory>();

        // SqlSugar client (shared across all modules)
        services.AddScoped<ISqlSugarClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            var tenantId = tenantProvider.GetTenantId();

            string connectionString = options.ConnectionString;

            var config = new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    EntityService = (property, column) =>
                    {
                        if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId))
                        {
                            column.IsIgnore = true;
                        }

                        if (property.DeclaringType == typeof(Atlas.Domain.Identity.Entities.UserAccount))
                        {
                            if (property.PropertyType == typeof(DateTimeOffset))
                            {
                                column.IsIgnore = true;
                            }
                        }

                        if (property.DeclaringType == typeof(Atlas.Domain.Identity.Entities.AuthSession) &&
                            property.Name == nameof(Atlas.Domain.Identity.Entities.AuthSession.RevokedAt))
                        {
                            column.IsNullable = true;
                        }

                        if (property.DeclaringType == typeof(Atlas.Domain.Identity.Entities.RefreshToken))
                        {
                            if (property.Name == nameof(Atlas.Domain.Identity.Entities.RefreshToken.RevokedAt) ||
                                property.Name == nameof(Atlas.Domain.Identity.Entities.RefreshToken.ReplacedById))
                            {
                                column.IsNullable = true;
                            }
                        }
                    }
                }
            };

            var db = new SqlSugarScope(config);
            if (!tenantId.IsEmpty)
            {
                db.QueryFilter.AddTableFilter<Atlas.Core.Abstractions.TenantEntity>(
                    it => it.TenantIdValue == tenantId.Value);
            }

            return db;
        });

        return services;
    }
}
