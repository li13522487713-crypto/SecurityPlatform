using Atlas.Application.Abstractions;
using Atlas.Application.Options;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.System.Events;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.System.Abstractions;
using Atlas.Application.TableViews.Abstractions;
using Atlas.Application.TableViews.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Events;
using Atlas.Core.Setup;
using Atlas.Infrastructure.Events;
using Atlas.Infrastructure.EventHandlers;
using Atlas.Infrastructure.IdGen;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.FileStorage;
using Atlas.Infrastructure.Services.Platform;
using Atlas.Infrastructure.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ITotpService = Atlas.Application.Abstractions.ITotpService;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// Registers core infrastructure services: database, ID generation, security, identity repositories and services.
/// </summary>
public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Setup State (must be registered before anything that depends on DB)
        services.AddSingleton<ISetupStateProvider, FileBasedSetupStateProvider>();
        services.AddSingleton<IAppSetupStateProvider, FileBasedAppSetupStateProvider>();
        services.AddSingleton<ISetupDbClientFactory, SetupDbClientFactory>();
        services.AddScoped<Atlas.Application.Setup.IDatabaseMaintenanceService, DatabaseMaintenanceService>();

        // Options
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<DatabaseBackupOptions>(configuration.GetSection("Database:Backup"));
        services.Configure<DatabaseEncryptionOptions>(configuration.GetSection("Database:Encryption"));
        services.Configure<SqliteDisasterRecoveryOptions>(configuration.GetSection("Database:SqliteDisasterRecovery"));
        services.Configure<SnowflakeOptions>(configuration.GetSection("Snowflake"));
        services.Configure<IdGeneratorMappingOptions>(configuration.GetSection("IdGenerator"));

        // Time & ID
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIdGeneratorProvider, SnowflakeIdGeneratorProvider>();
        services.AddScoped<IIdGeneratorAccessor, DefaultIdGeneratorAccessor>();

        // Event Bus (in-process, resolves all IDomainEventHandler<T> registrations)
        services.AddScoped<IEventBus, InProcessEventBus>();
        services.AddScoped<IDomainEventHandler<SystemConfigChangedEvent>, SystemConfigChangedEventHandler>();

        // Outbox (at-least-once integration event delivery)
        services.AddScoped<Atlas.Application.Events.IOutboxRepository, Atlas.Infrastructure.Repositories.OutboxRepository>();
        services.AddScoped<Atlas.Application.Events.IOutboxManagementService, Atlas.Infrastructure.Events.OutboxManagementService>();
        services.AddScoped<Atlas.Infrastructure.Events.OutboxPublisher>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, SqlSugarUnitOfWork>();

        // Background Work Queue (replaces unsafe Task.Run pattern for fire-and-forget)
        services.AddSingleton<BackgroundWorkQueue>();
        services.AddSingleton<IBackgroundWorkQueue>(sp => sp.GetRequiredService<BackgroundWorkQueue>());

        // Hosted Services (order: DB init → backup → outbox → work queue → cleanup)
        // All gated by ISetupStateProvider.WaitForReadyAsync — safe to register early.
        // DatabaseInitializerHostedService registered as both singleton (for SetupController injection) and hosted service.
        services.AddSingleton<DatabaseInitializerHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<DatabaseInitializerHostedService>());
        services.AddHostedService<DatabaseBackupHostedService>();
        services.AddHostedService<Atlas.Infrastructure.Events.OutboxProcessorHostedService>();
        services.AddHostedService<BackgroundWorkQueueProcessor>();
        services.AddHostedService<AuditRetentionHostedService>();
        services.AddHostedService<TenantExpirationHostedService>();
        services.AddHostedService<SessionCleanupHostedService>();
        services.AddHostedService<IdempotencyCleanupHostedService>();
        services.AddHostedService<FileUploadSessionCleanupHostedService>();

        // Security
        services.AddScoped<IAuthTokenService, JwtAuthTokenService>();
        services.AddScoped<IAuthProfileService, AuthProfileService>();
        services.AddScoped<IRbacResolver, RbacResolver>();
        services.AddScoped<IPermissionDecisionService, PermissionDecisionService>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();

        // Auth Repositories
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IIdempotencyRecordRepository, IdempotencyRecordRepository>();

        // Identity Repositories
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IAppConfigRepository, AppConfigRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectUserRepository, ProjectUserRepository>();
        services.AddScoped<IProjectDepartmentRepository, ProjectDepartmentRepository>();
        services.AddScoped<IProjectPositionRepository, ProjectPositionRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IRoleMenuRepository, RoleMenuRepository>();
        services.AddScoped<IRoleDeptRepository, RoleDeptRepository>();
        services.AddScoped<IUserDepartmentRepository, UserDepartmentRepository>();
        services.AddScoped<IUserPositionRepository, UserPositionRepository>();
        services.AddScoped<IUserHierarchyQueryRepository, UserHierarchyQueryRepository>();
        services.AddScoped<IPasswordHistoryRepository, PasswordHistoryRepository>();
        services.AddScoped<IOidcLinkRepository, OidcLinkRepository>();
        services.AddScoped<OidcLinkRepository>();

        // Table Views
        services.AddScoped<ITableViewRepository, TableViewRepository>();
        services.AddScoped<ITableViewDefaultRepository, TableViewDefaultRepository>();
        services.AddScoped<ITableViewDefaultConfigProvider, TableViewDefaultConfigProvider>();

        // Audit
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAuditRecorder, AuditRecorder>();

        // Alert
        services.AddScoped<Atlas.Application.Alert.Abstractions.IAlertQueryService, AlertQueryService>();

        // Identity Services
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IUserCommandService, UserCommandService>();
        services.AddScoped<IRoleQueryService, RoleQueryService>();
        services.AddScoped<IRoleCommandService, RoleCommandService>();
        services.AddScoped<IPermissionQueryService, PermissionQueryService>();
        services.AddScoped<IPermissionCommandService, PermissionCommandService>();
        services.AddScoped<IDepartmentQueryService, DepartmentQueryService>();
        services.AddScoped<IDepartmentCommandService, DepartmentCommandService>();
        services.AddScoped<IPositionQueryService, PositionQueryService>();
        services.AddScoped<IPositionCommandService, PositionCommandService>();
        services.AddScoped<IMenuQueryService, MenuQueryService>();
        services.AddScoped<IMenuCommandService, MenuCommandService>();
        services.AddScoped<IAppConfigQueryService, AppConfigQueryService>();
        services.AddScoped<IAppConfigCommandService, AppConfigCommandService>();
        services.AddScoped<IProjectQueryService, ProjectQueryService>();
        services.AddScoped<IProjectCommandService, ProjectCommandService>();
        services.AddScoped<ITableViewQueryService, TableViewQueryService>();
        services.AddScoped<ITableViewCommandService, TableViewCommandService>();

        // Amis
        services.AddSingleton<Atlas.Application.Amis.Abstractions.IAmisSchemaProvider, Atlas.Infrastructure.Services.Amis.FileSystemAmisSchemaProvider>();

        // Dict & SystemConfig Repositories
        services.AddScoped<DictTypeRepository>();
        services.AddScoped<DictDataRepository>();
        services.AddScoped<SystemConfigRepository>();

        // Dict & SystemConfig Services
        services.AddScoped<IDictQueryService, DictQueryService>();
        services.AddScoped<IDictCommandService, DictCommandService>();
        services.AddScoped<ISystemConfigQueryService, SystemConfigQueryService>();
        services.AddScoped<ISystemConfigCommandService, SystemConfigCommandService>();

        // Login log
        services.AddScoped<LoginLogRepository>();
        services.AddScoped<ILoginLogWriteService, LoginLogWriteService>();
        services.AddScoped<ILoginLogQueryService, LoginLogQueryService>();

        // Captcha (requires IMemoryCache registered in Program.cs via AddMemoryCache)
        services.AddSingleton<ICaptchaService, CaptchaService>();

        // Notification
        services.AddScoped<NotificationRepository>();
        services.AddScoped<UserNotificationRepository>();
        services.AddScoped<INotificationQueryService, NotificationService>();
        services.AddScoped<INotificationCommandService, NotificationService>();

        services.AddScoped<FileRecordRepository>();
        services.AddScoped<FileUploadSessionRepository>();
        services.AddScoped<FileTusUploadSessionRepository>();
        services.AddScoped<AttachmentBindingRepository>();
        services.AddSingleton<IHostEnvironmentAccessor, HostEnvironmentAccessor>();
        services.AddScoped<IFileStorageSettingsResolver, FileStorageSettingsResolver>();
        services.AddScoped<LocalObjectStore>();
        services.AddScoped<MinioObjectStore>();
        services.AddScoped<AliyunOssObjectStore>();
        services.AddScoped<IFileObjectStore, DynamicFileObjectStore>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddHostedService<ObjectStoreConnectivityService>();

        // Excel Export
        services.AddScoped<IExcelExportService, ClosedXmlExcelExportService>();

        // Monitor
        services.AddSingleton<Atlas.Application.Monitor.Abstractions.IServerInfoQueryService, ServerInfoQueryService>();
        services.AddSingleton<Atlas.Application.Monitor.Abstractions.IComplianceEvidencePackageService, ComplianceEvidencePackageService>();

        // Scheduled Jobs (Hangfire)
        services.AddScoped<IScheduledJobService, HangfireScheduledJobService>();

        // Data Scope Filter (等保2.0 数据权限)
        services.AddScoped<ITenantDataScopeFilter, TenantDataScopeFilter>();
        services.AddScoped<IAppDataScopeFilter, AppDataScopeFilter>();

        return services;
    }
}
