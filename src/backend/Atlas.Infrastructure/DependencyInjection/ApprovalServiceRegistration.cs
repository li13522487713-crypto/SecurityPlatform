using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// Registers approval workflow services, repositories, operation handlers, and notification senders.
/// </summary>
public static class ApprovalServiceRegistration
{
    public static IServiceCollection AddApprovalInfrastructure(this IServiceCollection services)
    {
        // Approval Repositories
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

        // External Callback Service & Retry
        services.AddScoped<Atlas.Infrastructure.Services.ApprovalFlow.ExternalCallbackService>();
        services.AddHostedService<ApprovalExternalCallbackRetryHostedService>();

        // Notification Senders
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders.EmailNotificationSender>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders.SmsNotificationSender>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationSender, Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders.AppPushNotificationSender>();

        // Notification & Reminder Services
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalNotificationService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalNotificationService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalReminderService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalReminderService>();

        // Timeout Reminder Hosted Service
        services.AddHostedService<ApprovalTimeoutReminderHostedService>();
        // Timeout Auto-Processing Hosted Service (auto-approve/reject/skip timed-out tasks)
        services.AddHostedService<ApprovalTimeoutAutoProcessHostedService>();

        // Operation Handlers
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

        // Query & Command Services
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalFlowQueryService, ApprovalFlowQueryService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalFlowCommandService, ApprovalFlowCommandService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalRuntimeQueryService, ApprovalRuntimeQueryService>();

        // Approval Status Sync Handler (for dynamic table status writeback)
        services.AddScoped<Atlas.Infrastructure.Services.ApprovalFlow.ApprovalStatusSyncHandler>();

        // Unified Assignee Resolver (eliminates duplicated logic in FlowEngine, BackToAnyNode, CopyRecord)
        services.AddScoped<Atlas.Infrastructure.Services.ApprovalFlow.AssigneeResolver>();

        // Domain Event Publisher & Handlers (decouples approval from external business modules)
        services.AddScoped<Atlas.Infrastructure.Services.ApprovalFlow.ApprovalEventPublisher>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalEventHandler,
            Atlas.Infrastructure.Services.ApprovalFlow.DynamicTableApprovalEventHandler>();

        // ApprovalRuntimeCommandService (manual resolution for ExternalCallbackService)
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
            var idGeneratorAccessor = sp.GetRequiredService<Atlas.Core.Abstractions.IIdGeneratorAccessor>();
            var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
            var unitOfWork = sp.GetService<Atlas.Core.Abstractions.IUnitOfWork>();
            var notificationService = sp.GetService<Atlas.Application.Approval.Abstractions.IApprovalNotificationService>();
            var timeoutReminderRepository = sp.GetService<Atlas.Application.Approval.Repositories.IApprovalTimeoutReminderRepository>();
            var callbackService = sp.GetService<Atlas.Infrastructure.Services.ApprovalFlow.ExternalCallbackService>();
            var statusSyncHandler = sp.GetService<Atlas.Infrastructure.Services.ApprovalFlow.ApprovalStatusSyncHandler>();
            var backgroundWorkQueue = sp.GetService<Atlas.Core.Abstractions.IBackgroundWorkQueue>();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<ApprovalRuntimeCommandService>>();
            return new ApprovalRuntimeCommandService(
                flowRepository, instanceRepository, taskRepository, historyRepository,
                deptLeaderRepository, nodeExecutionRepository, parallelTokenRepository,
                copyRecordRepository, processVariableRepository, userQueryService,
                idGeneratorAccessor, mapper, unitOfWork, notificationService, timeoutReminderRepository,
                callbackService, statusSyncHandler, backgroundWorkQueue, logger);
        });

        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalDepartmentLeaderService, ApprovalDepartmentLeaderService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalOperationService, ApprovalOperationService>();

        // Approval user/role/department query services
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalUserService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalUserService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalRoleService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalRoleService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalDepartmentService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalDepartmentService>();
        services.AddScoped<Atlas.Application.Approval.Abstractions.IApprovalUserQueryService, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalUserQueryService>();

        // Seed Data & Index
        services.AddScoped<ApprovalSeedDataService>();
        services.AddScoped<ApprovalIndexInitializer>();

        return services;
    }
}
