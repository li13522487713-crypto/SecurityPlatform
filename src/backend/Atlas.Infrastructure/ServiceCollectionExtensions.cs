using Atlas.Application.Abstractions;
using Atlas.Application.Alert.Abstractions;
using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Workflow.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.IdGen;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Workflow;
using Atlas.WorkflowCore.Abstractions.Persistence;
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
        services.Configure<DatabaseBackupOptions>(configuration.GetSection("Database:Backup"));
        services.Configure<DatabaseEncryptionOptions>(configuration.GetSection("Database:Encryption"));
        services.Configure<SnowflakeOptions>(configuration.GetSection("Snowflake"));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIdGenerator, SnowflakeIdGenerator>();

        // 注意：HostedService 启动按注册顺序执行。数据库初始化必须最先完成（建表/建索引/种子数据），
        // 否则其它后台服务（超时提醒、外部回调重试等）可能会在表尚未创建时就开始查询，导致 no such table。
        services.AddHostedService<DatabaseInitializerHostedService>();
        services.AddHostedService<DatabaseBackupHostedService>();

        services.AddScoped<IAuthTokenService, JwtAuthTokenService>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IRoleMenuRepository, RoleMenuRepository>();
        services.AddScoped<IUserDepartmentRepository, UserDepartmentRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IAssetQueryService, AssetQueryService>();
        services.AddScoped<IAssetCommandService, AssetCommandService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAlertQueryService, AlertQueryService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IUserCommandService, UserCommandService>();
        services.AddScoped<IRoleQueryService, RoleQueryService>();
        services.AddScoped<IRoleCommandService, RoleCommandService>();
        services.AddScoped<IPermissionQueryService, PermissionQueryService>();
        services.AddScoped<IPermissionCommandService, PermissionCommandService>();
        services.AddScoped<IDepartmentQueryService, DepartmentQueryService>();
        services.AddScoped<IDepartmentCommandService, DepartmentCommandService>();
        services.AddScoped<IMenuQueryService, MenuQueryService>();
        services.AddScoped<IMenuCommandService, MenuCommandService>();
        
        // Approval Workflow Services
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalFlowRepository, ApprovalFlowRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalInstanceRepository, ApprovalInstanceRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalTaskRepository, ApprovalTaskRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalHistoryRepository, ApprovalHistoryRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalDepartmentLeaderRepository, ApprovalDepartmentLeaderRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalProcessVariableRepository, ApprovalProcessVariableRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalTaskTransferRepository, ApprovalTaskTransferRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalTaskAssigneeChangeRepository, ApprovalTaskAssigneeChangeRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalNodeExecutionRepository, ApprovalNodeExecutionRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalOperationRecordRepository, ApprovalOperationRecordRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalParallelTokenRepository, ApprovalParallelTokenRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalParallelTokenRepository, ApprovalParallelTokenRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalCopyRecordRepository, ApprovalCopyRecordRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalNotificationTemplateRepository, ApprovalNotificationTemplateRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalInboxMessageRepository, ApprovalInboxMessageRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalTimeoutReminderRepository, ApprovalTimeoutReminderRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalReminderRecordRepository, ApprovalReminderRecordRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalExternalCallbackConfigRepository, ApprovalExternalCallbackConfigRepository>();
        services.AddScoped<Atlas.Application.Approval.Repositories.IApprovalExternalCallbackRecordRepository, ApprovalExternalCallbackRecordRepository>();
        
        // External Callback Handler
        services.AddScoped<Atlas.Application.Approval.Abstractions.IExternalCallbackHandler>(sp =>
        {
            var httpClient = new System.Net.Http.HttpClient();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<Atlas.Infrastructure.Services.ApprovalFlow.CallbackHandlers.HttpCallbackHandler>>();
            return new Atlas.Infrastructure.Services.ApprovalFlow.CallbackHandlers.HttpCallbackHandler(httpClient, logger);
        });
        
        // External Callback Service
        services.AddScoped<Atlas.Infrastructure.Services.ApprovalFlow.ExternalCallbackService>();
        
        // External Callback Retry Hosted Service
        services.AddHostedService<ApprovalExternalCallbackRetryHostedService>();
        
        // Approval Notification Senders
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders.EmailNotificationSender>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders.SmsNotificationSender>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders.AppPushNotificationSender>();
        
        // Approval Notification Service
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalNotificationService>();
        
        // Approval Reminder Service
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalReminderService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalReminderService>();
        
        // Approval Timeout Reminder Hosted Service
        services.AddHostedService<ApprovalTimeoutReminderHostedService>();
        
        // Approval Flow Operation Handlers
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.ProcessDrawBackOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.TransferOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.AddAssigneeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.BackToModifyOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.BackToAnyNodeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.DrawBackAgreeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.UndertakeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.ForwardOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.ChangeAssigneeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.RemoveAssigneeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.ChangeFutureAssigneeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.AddFutureAssigneeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.RemoveFutureAssigneeOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.SaveDraftOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.RecoverToHistoryOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.ProcessMoveAheadOperationHandler>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationHandler, Atlas.Infrastructure.Services.ApprovalFlow.Operations.AddApprovalOperationHandler>();
        services.AddScoped<Atlas.Infrastructure.Services.ApprovalFlow.ApprovalOperationDispatcher>();
        
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalFlowQueryService, ApprovalFlowQueryService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalFlowCommandService, ApprovalFlowCommandService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalRuntimeQueryService, ApprovalRuntimeQueryService>();
        // ApprovalRuntimeCommandService 需要手动注册以注入 ExternalCallbackService
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalRuntimeCommandService>(sp =>
        {
            var flowRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalFlowRepository>();
            var instanceRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalInstanceRepository>();
            var taskRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalTaskRepository>();
            var historyRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalHistoryRepository>();
            var deptLeaderRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalDepartmentLeaderRepository>();
            var nodeExecutionRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalNodeExecutionRepository>();
            var parallelTokenRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalParallelTokenRepository>();
            var copyRecordRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalCopyRecordRepository>();
            var processVariableRepository = sp.GetRequiredService<Atlas.Application.Approval.Repositories.IApprovalProcessVariableRepository>();
            var userQueryService = sp.GetRequiredService<Atlas.Application.Approval.Abstractions.IApprovalUserQueryService>();
            var idGenerator = sp.GetRequiredService<Atlas.Core.Abstractions.IIdGenerator>();
            var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
            var notificationService = sp.GetService<Atlas.Application.Approval.Abstractions.IApprovalNotificationService>();
            var timeoutReminderRepository = sp.GetService<Atlas.Application.Approval.Repositories.IApprovalTimeoutReminderRepository>();
            var callbackService = sp.GetService<Atlas.Infrastructure.Services.ApprovalFlow.ExternalCallbackService>();
            return new ApprovalRuntimeCommandService(
                flowRepository, instanceRepository, taskRepository, historyRepository,
                deptLeaderRepository, nodeExecutionRepository, parallelTokenRepository,
                copyRecordRepository, processVariableRepository, userQueryService,
                idGenerator, mapper, notificationService, timeoutReminderRepository, callbackService);
        });
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalDepartmentLeaderService, ApprovalDepartmentLeaderService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationService, ApprovalOperationService>();
        
        // 审批模块用户/角色/部门查询服务接口契约（可替换实现）
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalUserService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalUserService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalRoleService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalRoleService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalDepartmentService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalDepartmentService>();
        
        // 审批模块用户查询服务（组合式实现，依赖上述接口契约）
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalUserQueryService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalUserQueryService>();
        services.AddScoped<ApprovalSeedDataService>();
        services.AddScoped<ApprovalIndexInitializer>();
        
        // Workflow Persistence
        services.AddScoped<IPersistenceProvider, SqlSugarPersistenceProvider>();
        
        // Workflow Services
        services.AddScoped<IWorkflowQueryService, WorkflowQueryService>();
        services.AddScoped<IWorkflowCommandService, WorkflowCommandService>();

        services.AddScoped<ISqlSugarClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            var tenantId = tenantProvider.GetTenantId();

            var config = new ConnectionConfig
            {
                ConnectionString = options.ConnectionString,
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
