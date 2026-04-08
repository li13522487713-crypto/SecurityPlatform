using Atlas.Core.Observability;
using Atlas.Core.Plugins;
using Atlas.Core.Setup;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.DependencyInjection;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasInfrastructureShared(
        this IServiceCollection services,
        IConfiguration configuration,
        bool includeAppRuntimeServices = true)
    {
        services.Configure<PluginCatalogOptions>(configuration.GetSection("Plugins"));
        services.Configure<AtlasHybridCacheOptions>(configuration.GetSection("HybridCache"));

        var atlasHybridCacheOptions = configuration.GetSection("HybridCache").Get<AtlasHybridCacheOptions>()
            ?? new AtlasHybridCacheOptions();

        if (atlasHybridCacheOptions.Redis.Enabled
            && !string.IsNullOrWhiteSpace(atlasHybridCacheOptions.Redis.Configuration))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = atlasHybridCacheOptions.Redis.Configuration;
                options.InstanceName = atlasHybridCacheOptions.Redis.InstanceName;
            });
        }

        services.AddHybridCache();
        services.AddSingleton<IAtlasHybridCache, AtlasHybridCache>();

        services.AddCoreInfrastructure(configuration, includeAppRuntimeServices);
        services.AddAssetInfrastructure();

        services.AddSingleton<INodeMetricsCollector, Atlas.Infrastructure.Observability.InMemoryNodeMetricsCollector>();
        services.AddSingleton<ITraceCorrelator, Atlas.Infrastructure.Observability.ActivityTraceCorrelator>();
        services.AddSingleton<IPluginRegistry, Atlas.Infrastructure.Plugins.PluginRegistry>();
        services.AddScoped<IExecutionLogger, Atlas.Infrastructure.Observability.ExecutionLogger>();

        services.AddSingleton<Atlas.Core.Resilience.IErrorClassifier, Atlas.Infrastructure.Resilience.ErrorClassifier>();
        services.AddSingleton<Atlas.Core.Resilience.ICircuitBreaker, Atlas.Infrastructure.Resilience.SimpleCircuitBreaker>();
        services.AddSingleton<Atlas.Core.Resilience.IRateLimiter, Atlas.Infrastructure.Resilience.InMemoryRateLimiter>();
        services.AddScoped<Atlas.Application.Resilience.IInboxService, Atlas.Infrastructure.Resilience.InboxService>();
        services.AddScoped<Atlas.Application.Resilience.IOutboxService, Atlas.Infrastructure.Resilience.OutboxService>();
        services.AddScoped<Atlas.Application.Resilience.IReconciliationService, Atlas.Infrastructure.Resilience.ReconciliationService>();

        services.AddSingleton<Atlas.Application.DataSource.IDataSourceConnectorRegistry>(sp =>
        {
            var registry = new Atlas.Infrastructure.DataSource.DataSourceConnectorRegistry();
            registry.Register(new Atlas.Infrastructure.DataSource.SqliteDataSourceConnector());
            return registry;
        });

        services.AddScoped<Atlas.Core.Saga.ISagaOrchestrator, Atlas.Infrastructure.Saga.SagaOrchestrator>();
        services.AddScoped<Atlas.Application.Events.IEventSubscriptionService, Atlas.Infrastructure.Events.EventSubscriptionService>();
        services.AddScoped<Atlas.Application.Events.IPlatformEventService, Atlas.Infrastructure.Events.PlatformEventService>();
        services.AddScoped<Atlas.Application.Metering.IMeteringService, Atlas.Infrastructure.Services.Metering.MeteringService>();
        services.AddSingleton<Atlas.Infrastructure.Plugins.PluginMetricsStore>();

        services.AddScoped<ISqlSugarClient>(sp =>
        {
            var setupState = sp.GetRequiredService<ISetupStateProvider>();
            if (!setupState.IsReady && !setupState.IsSetupInProgress)
            {
                throw new InvalidOperationException(
                    "数据库尚未配置。请先完成平台安装向导（Setup Wizard）。" +
                    "ISqlSugarClient 仅在 setup 完成或 setup 进行中时可用。");
            }

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
                        if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
                            && property.PropertyType == typeof(Atlas.Core.Tenancy.TenantId))
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

            // SQLite：启用 WAL 日志模式（读写不互斥）+ 写锁等待 5 s（避免并发写立即报 database is locked）
            if (dbType == DbType.Sqlite)
            {
                try
                {
                    db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
                }
                catch (Exception ex) when (ex.Message.Contains("malformed database schema", StringComparison.OrdinalIgnoreCase))
                {
                    // 历史异常数据可能误写入 sqlite_master，统一走 SqliteSchemaAlignment 清理。
                    SqliteSchemaAlignment.CleanupBrokenSchemaEntries(db);
                    db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
                }
            }

            return db;
        });

        services.AddSingleton<Atlas.Application.Identity.Abstractions.IAuthCacheService,
            Atlas.Infrastructure.Security.HybridAuthCacheService>();

        return services;
    }

    public static IServiceCollection AddAtlasInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddAtlasInfrastructureShared(configuration)
            .AddAtlasInfrastructurePlatform(configuration)
            .AddAtlasInfrastructureAppRuntime(configuration);
    }

    private static DbType MapDbType(string? dbType)
    {
        try
        {
            return DataSourceDriverRegistry.ResolveDbType(dbType);
        }
        catch
        {
            return DbType.Sqlite;
        }
    }
}
