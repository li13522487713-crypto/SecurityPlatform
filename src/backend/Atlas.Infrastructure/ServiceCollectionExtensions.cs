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
        services.AddAiPlatformInfrastructure(configuration);
        services.AddLicenseInfrastructure();
        services.AddPlatformInfrastructure();
        services.AddGovernanceInfrastructure();

        // 注册多数据源相关服务
        services.AddScoped<Atlas.Infrastructure.Repositories.TenantDataSourceRepository>();
        services.AddScoped<Atlas.Application.System.Abstractions.ITenantDataSourceService,
            Atlas.Infrastructure.Services.TenantDataSourceService>();
        services.AddScoped<Atlas.Application.System.Abstractions.ITenantDbConnectionFactory,
            Atlas.Infrastructure.Services.TenantDbConnectionFactory>();
        services.AddSingleton<IPluginCatalogService, Atlas.Infrastructure.Services.PluginCatalogService>();

        // Plugin Configuration
        services.AddScoped<Atlas.Application.Plugins.Repositories.IPluginConfigRepository, Atlas.Infrastructure.Repositories.PluginConfigRepository>();
        services.AddScoped<Atlas.Application.Plugins.Abstractions.IPluginConfigService, Atlas.Infrastructure.Services.PluginConfigService>();

        // Plugin Package (install from .atpkg upload)
        services.AddScoped<Atlas.Infrastructure.Plugins.PluginPackageService>();

        // Plugin Market
        services.AddScoped<Atlas.Application.Plugins.Abstractions.IPluginMarketQueryService, Atlas.Infrastructure.Services.PluginMarketQueryService>();
        services.AddScoped<Atlas.Application.Plugins.Abstractions.IPluginMarketCommandService, Atlas.Infrastructure.Services.PluginMarketCommandService>();

        // Component Templates
        services.AddScoped<Atlas.Application.Templates.IComponentTemplateQueryService, Atlas.Infrastructure.Services.ComponentTemplateQueryService>();
        services.AddScoped<Atlas.Application.Templates.IComponentTemplateCommandService, Atlas.Infrastructure.Services.ComponentTemplateCommandService>();
        services.AddScoped<Atlas.Infrastructure.Services.TemplateSeedDataService>();

        // Webhooks
        services.AddScoped<Atlas.Application.Integration.IWebhookService, Atlas.Infrastructure.Services.WebhookService>();
        services.AddHttpClient("Webhook").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

        // API Connectors
        services.AddScoped<Atlas.Application.Integration.IApiConnectorService, Atlas.Infrastructure.Services.ApiConnectorService>();

        // Integration API Key validation
        services.AddScoped<Atlas.Application.Integration.IIntegrationApiKeyRepository, Atlas.Infrastructure.Repositories.IntegrationApiKeyRepository>();
        services.AddScoped<Atlas.Application.Integration.IApiKeyValidationService, Atlas.Infrastructure.Services.ApiKeyValidationService>();

        // Data Source Connector Registry (singleton for lifetime of app)
        services.AddSingleton<Atlas.Application.DataSource.IDataSourceConnectorRegistry>(sp =>
        {
            var registry = new Atlas.Infrastructure.DataSource.DataSourceConnectorRegistry();
            registry.Register(new Atlas.Infrastructure.DataSource.SqliteDataSourceConnector());
            return registry;
        });

        // Message Queue (SQLite-backed)
        services.AddScoped<Atlas.Core.Messaging.IMessageQueue, Atlas.Infrastructure.Messaging.SqliteMessageQueue>();
        services.AddHostedService<Atlas.Infrastructure.Messaging.MessageQueueProcessorHostedService>();

        // Approval Event Consumer (async mode, activated when Messaging:ApprovalEvents:AsyncEnabled = true)
        services.AddScoped<Atlas.Infrastructure.Messaging.IQueueMessageHandler, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalEventConsumer>();

        // Saga Orchestrator
        services.AddScoped<Atlas.Core.Saga.ISagaOrchestrator, Atlas.Infrastructure.Saga.SagaOrchestrator>();

        // Event Subscriptions
        services.AddScoped<Atlas.Application.Events.IEventSubscriptionService, Atlas.Infrastructure.Events.EventSubscriptionService>();
        services.AddScoped<Atlas.Application.Events.IPlatformEventService, Atlas.Infrastructure.Events.PlatformEventService>();

        // Metering
        services.AddScoped<Atlas.Application.Metering.IMeteringService, Atlas.Infrastructure.Services.Metering.MeteringService>();

        // Subscription & Plans
        services.AddScoped<Atlas.Application.Subscription.IPlanQueryService, Atlas.Infrastructure.Services.Subscription.PlanService>();
        services.AddScoped<Atlas.Application.Subscription.IPlanCommandService, Atlas.Infrastructure.Services.Subscription.PlanService>();
        services.AddScoped<Atlas.Application.Subscription.ISubscriptionService, Atlas.Infrastructure.Services.Subscription.SubscriptionService>();

        // Observability - Alert Rules
        services.AddScoped<Atlas.Application.Observability.IAlertRuleService, Atlas.Infrastructure.Observability.AlertRuleService>();

        // Plugin Metrics
        services.AddSingleton<Atlas.Infrastructure.Plugins.PluginMetricsStore>();

        // Evidence Chain
        services.AddScoped<Atlas.Infrastructure.Services.EvidenceChainService>();

        // SqlSugar client (shared across all modules)
        services.AddScoped<ISqlSugarClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var configuration = sp.GetRequiredService<IConfiguration>();
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            var tenantId = tenantProvider.GetTenantId();

            string connectionString = options.ConnectionString;
            var dbType = MapDbType(options.DbType);

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

                        // 平台级权限 AppId 必须为 NULL；SqlSugar 对部分可空 long 可能生成 NOT NULL，导致种子数据插入失败
                        if (property.DeclaringType == typeof(Atlas.Domain.Identity.Entities.Permission)
                            && property.Name == nameof(Atlas.Domain.Identity.Entities.Permission.AppId))
                        {
                            column.IsNullable = true;
                        }

                        if (property.DeclaringType == typeof(Atlas.Domain.Approval.Entities.ApprovalTask)
                            && property.Name == nameof(Atlas.Domain.Approval.Entities.ApprovalTask.RowVersion))
                        {
                            column.IsEnableUpdateVersionValidation = true;
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

        // JWT 认证缓存（减少热路径 DB 查询，TTL 60 秒）
        services.AddSingleton<Atlas.Application.Identity.Abstractions.IAuthCacheService,
            Atlas.Infrastructure.Security.MemoryAuthCacheService>();

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
