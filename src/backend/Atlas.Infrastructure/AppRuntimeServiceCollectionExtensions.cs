using Atlas.Infrastructure.DependencyInjection;
using Atlas.Infrastructure.LogicFlow;
using Atlas.Infrastructure.BatchProcess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure;

public static class AppRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasInfrastructureAppRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDynamicTableInfrastructure();
        services.AddApprovalInfrastructure();
        services.AddWorkflowInfrastructure();
        services.AddLowCodeInfrastructure();
        services.AddAiCoreInfrastructure(configuration);
        services.AddAiRuntimeInfrastructure();
        services.AddLogicFlowInfrastructure();
        services.AddBatchProcessInfrastructure();

        services.AddScoped<Atlas.Application.Integration.IWebhookService, Atlas.Infrastructure.Services.WebhookService>();
        services.AddHttpClient("Webhook").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<Atlas.Application.Integration.IApiConnectorService, Atlas.Infrastructure.Services.ApiConnectorService>();
        services.AddScoped<Atlas.Application.Integration.IIntegrationApiKeyRepository, Atlas.Infrastructure.Repositories.IntegrationApiKeyRepository>();
        services.AddScoped<Atlas.Application.Integration.IApiKeyValidationService, Atlas.Infrastructure.Services.ApiKeyValidationService>();
        services.AddScoped<Atlas.Application.Resilience.ICompensationService, Atlas.Infrastructure.Resilience.CompensationService>();
        services.AddScoped<Atlas.Infrastructure.Services.EvidenceChainService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.ITenantAppInstanceQueryService, Atlas.Infrastructure.Services.Platform.TenantAppInstanceQueryService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.ITenantAppInstanceCommandService, Atlas.Infrastructure.Services.Platform.TenantAppInstanceCommandService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.ITenantAppMemberQueryService, Atlas.Infrastructure.Services.Platform.TenantAppMemberQueryService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.ITenantAppMemberCommandService, Atlas.Infrastructure.Services.Platform.TenantAppMemberCommandService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.ITenantAppRoleQueryService, Atlas.Infrastructure.Services.Platform.TenantAppRoleQueryService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.ITenantAppRoleCommandService, Atlas.Infrastructure.Services.Platform.TenantAppRoleCommandService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppOrgQueryService, Atlas.Infrastructure.Services.Platform.AppOrgQueryService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppOrgCommandService, Atlas.Infrastructure.Services.Platform.AppOrgCommandService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppOrganizationQueryService, Atlas.Infrastructure.Services.Platform.AppOrganizationQueryService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppOrganizationCommandService, Atlas.Infrastructure.Services.Platform.AppOrganizationCommandService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppRoleAssignmentQueryService, Atlas.Infrastructure.Services.Platform.AppRoleAssignmentQueryService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IAppRoleAssignmentCommandService, Atlas.Infrastructure.Services.Platform.AppRoleAssignmentCommandService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IRuntimeExecutionCommandService, Atlas.Infrastructure.Services.Platform.RuntimeExecutionCommandService>();
        services.AddScoped<Atlas.Application.Platform.Abstractions.IRuntimeRouteQueryService, Atlas.Infrastructure.Services.Platform.RuntimeRouteQueryService>();

        services.AddScoped<Atlas.Core.Messaging.IMessageQueue, Atlas.Infrastructure.Messaging.SqliteMessageQueue>();
        services.AddHostedService<Atlas.Infrastructure.Messaging.MessageQueueProcessorHostedService>();
        services.AddScoped<Atlas.Infrastructure.Messaging.IQueueMessageHandler, Atlas.Infrastructure.Services.ApprovalFlow.ApprovalEventConsumer>();

        return services;
    }
}
