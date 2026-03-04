using Atlas.Application.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.System.Abstractions;
using Atlas.Application.TableViews.Abstractions;
using Atlas.Application.TableViews.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Infrastructure.IdGen;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ITotpService = Atlas.Application.Abstractions.ITotpService;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// Registers core infrastructure services: database, ID generation, security, identity repositories and services.
/// </summary>
public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<DatabaseBackupOptions>(configuration.GetSection("Database:Backup"));
        services.Configure<DatabaseEncryptionOptions>(configuration.GetSection("Database:Encryption"));
        services.Configure<SnowflakeOptions>(configuration.GetSection("Snowflake"));
        services.Configure<IdGeneratorMappingOptions>(configuration.GetSection("IdGenerator"));

        // Time & ID
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIdGeneratorProvider, SnowflakeIdGeneratorProvider>();
        services.AddScoped<IIdGeneratorAccessor, DefaultIdGeneratorAccessor>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, SqlSugarUnitOfWork>();

        // Background Work Queue (replaces unsafe Task.Run pattern for fire-and-forget)
        services.AddSingleton<BackgroundWorkQueue>();
        services.AddSingleton<IBackgroundWorkQueue>(sp => sp.GetRequiredService<BackgroundWorkQueue>());
        services.AddHostedService<BackgroundWorkQueueProcessor>();

        // Hosted Services (order matters: DB init first)
        services.AddHostedService<DatabaseInitializerHostedService>();
        services.AddHostedService<DatabaseBackupHostedService>();
        services.AddHostedService<AuditRetentionHostedService>();
        services.AddHostedService<SessionCleanupHostedService>();
        services.AddHostedService<IdempotencyCleanupHostedService>();

        // Security
        services.AddScoped<IAuthTokenService, JwtAuthTokenService>();
        services.AddScoped<IAuthProfileService, AuthProfileService>();
        services.AddScoped<IRbacResolver, RbacResolver>();
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

        // Index Initializers
        services.AddScoped<IdempotencyIndexInitializer>();
        services.AddScoped<TableViewIndexInitializer>();

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

        // File Storage
        services.AddScoped<FileRecordRepository>();
        services.AddSingleton<IHostEnvironmentAccessor, HostEnvironmentAccessor>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Excel Export
        services.AddScoped<IExcelExportService, ClosedXmlExcelExportService>();

        // Monitor
        services.AddSingleton<Atlas.Application.Monitor.Abstractions.IServerInfoQueryService, ServerInfoQueryService>();
        services.AddSingleton<Atlas.Application.Monitor.Abstractions.IComplianceEvidencePackageService, ComplianceEvidencePackageService>();

        // Scheduled Jobs (Hangfire)
        services.AddScoped<IScheduledJobService, HangfireScheduledJobService>();

        // Data Scope Filter (等保2.0 数据权限)
        services.AddScoped<IDataScopeFilter, DataScopeFilter>();

        return services;
    }
}
