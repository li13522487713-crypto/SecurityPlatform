using Atlas.Application.Abstractions;
using Atlas.Application.Alert.Abstractions;
using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.IdGen;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Security;
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
        services.Configure<DatabaseBackupOptions>(configuration.GetSection("Database:Backup"));
        services.Configure<DatabaseEncryptionOptions>(configuration.GetSection("Database:Encryption"));
        services.Configure<SnowflakeOptions>(configuration.GetSection("Snowflake"));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIdGenerator, SnowflakeIdGenerator>();
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
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalRuntimeCommandService, ApprovalRuntimeCommandService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalDepartmentLeaderService, ApprovalDepartmentLeaderService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationService, ApprovalOperationService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalUserQueryService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalUserQueryService>();
        services.AddScoped<ApprovalSeedDataService>();
        services.AddScoped<ApprovalIndexInitializer>();
        services.AddHostedService<DatabaseInitializerHostedService>();
        services.AddHostedService<DatabaseBackupHostedService>();

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
