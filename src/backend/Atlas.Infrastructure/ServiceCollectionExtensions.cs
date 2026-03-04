using Atlas.Core.Tenancy;
using Atlas.Application.Plugins.Abstractions;
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
        services.Configure<PluginCatalogOptions>(configuration.GetSection("Plugins"));

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
        services.AddSingleton<IPluginCatalogService, Atlas.Infrastructure.Services.PluginCatalogService>();

        // SqlSugar client (shared across all modules)
        services.AddScoped<ISqlSugarClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            var tenantId = tenantProvider.GetTenantId();

            // 尝试获取租户自定义数据源
            string connectionString = options.ConnectionString;
            var dbType = DbType.Sqlite;
            if (!tenantId.IsEmpty)
            {
                try
                {
                    var factory = sp.GetRequiredService<Atlas.Application.System.Abstractions.ITenantDbConnectionFactory>();
                    var customConnectionInfo = factory.GetConnectionInfoAsync(tenantId.Value.ToString()).GetAwaiter().GetResult();
                    if (customConnectionInfo is not null && !string.IsNullOrWhiteSpace(customConnectionInfo.ConnectionString))
                    {
                        connectionString = customConnectionInfo.ConnectionString;
                        dbType = MapDbType(customConnectionInfo.DbType);
                    }
                }
                catch
                {
                    // 数据源查询失败时回退到默认连接
                }
            }

            var config = new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = dbType,
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

    private static DbType MapDbType(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType))
        {
            return DbType.Sqlite;
        }

        return dbType.Trim().ToLowerInvariant() switch
        {
            "sqlite" => DbType.Sqlite,
            "sqlserver" => DbType.SqlServer,
            "mysql" => DbType.MySql,
            "postgresql" => DbType.PostgreSQL,
            _ => DbType.Sqlite
        };
    }
}
