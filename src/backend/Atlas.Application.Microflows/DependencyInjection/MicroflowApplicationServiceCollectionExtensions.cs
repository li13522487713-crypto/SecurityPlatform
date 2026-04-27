using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Actions.Http;
using Atlas.Application.Microflows.Runtime.Calls;
using Atlas.Application.Microflows.Runtime.ErrorHandling;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Loops;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Security;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Application.Microflows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Application.Microflows.DependencyInjection;

public static class MicroflowApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasApplicationMicroflows(this IServiceCollection services)
    {
        services.TryAddSingleton<IMicroflowClock, SystemMicroflowClock>();
        services.TryAddScoped<IMicroflowSchemaReader, MicroflowSchemaReader>();
        services.TryAddScoped<IMicroflowResourceService, MicroflowResourceService>();
        services.TryAddScoped<IMicroflowMetadataService, MicroflowMetadataService>();
        services.TryAddScoped<IMicroflowResourceQueryService, InMemoryMicroflowResourceQueryService>();
        services.TryAddScoped<IMicroflowMetadataQueryService, InMemoryMicroflowMetadataQueryService>();
        services.TryAddScoped<IMicroflowValidationService, MicroflowValidationService>();
        services.TryAddSingleton<MicroflowEntityAccessOptions>();
        services.TryAddScoped<IMicroflowReferenceIndexer, MicroflowReferenceIndexer>();
        services.TryAddScoped<IMicroflowReferenceService, MicroflowReferenceService>();
        services.TryAddScoped<IMicroflowMockRuntimeRunner, MicroflowMockRuntimeRunner>();
        services.TryAddScoped<IMicroflowVariableStore, MicroflowVariableStore>();
        services.TryAddScoped<IMicroflowExpressionEvaluator, MicroflowExpressionEvaluator>();
        services.TryAddScoped<IMicroflowMetadataResolver, MicroflowMetadataResolver>();
        services.TryAddScoped<IMicroflowEntityAccessService, MicroflowEntityAccessService>();
        services.TryAddScoped<IMicroflowRuntimeObjectMetadataService, MicroflowRuntimeObjectMetadataService>();
        services.TryAddScoped<IMicroflowTransactionManager, MicroflowTransactionManager>();
        services.TryAddScoped<IMicroflowErrorHandlingService, MicroflowErrorHandlingService>();
        services.TryAddTransient<IMicroflowUnitOfWork, MicroflowUnitOfWork>();
        services.TryAddScoped<IMicroflowActionExecutorRegistry, MicroflowActionExecutorRegistry>();
        services.TryAddScoped<CallMicroflowActionExecutor>();
        services.TryAddScoped<RestCallActionExecutor>();
        services.TryAddScoped<LogMessageActionExecutor>();
        services.TryAddSingleton<MicroflowRestExecutionOptions>();
        services.TryAddSingleton<MicroflowRestSecurityPolicy>();
        services.TryAddScoped<MicroflowRestRequestBuilder>();
        services.TryAddScoped<MicroflowRestResponseHandler>();
        services.TryAddScoped<IMicroflowRuntimeHttpClient, MicroflowRuntimeHttpClient>();
        services.AddHttpClient(MicroflowRuntimeHttpClient.HttpClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        services.TryAddScoped<IMicroflowCallStackService, MicroflowCallStackService>();
        services.TryAddScoped<IMicroflowLoopExecutor, MicroflowLoopExecutor>();
        services.TryAddScoped<IMicroflowRuntimeConnectorRegistry, MicroflowRuntimeConnectorRegistry>();
        services.TryAddScoped<IMicroflowTestRunService, MicroflowTestRunService>();
        services.TryAddScoped<IMicroflowVersionDiffService, MicroflowVersionDiffService>();
        services.TryAddScoped<IMicroflowPublishImpactService, MicroflowPublishImpactService>();
        services.TryAddScoped<IMicroflowPublishService, MicroflowPublishService>();
        services.TryAddScoped<IMicroflowVersionService, MicroflowVersionService>();
        services.TryAddScoped<IMicroflowActionSupportMatrix, MicroflowActionSupportMatrix>();
        services.TryAddScoped<IMicroflowRuntimeDtoBuilder, MicroflowRuntimeDtoBuilder>();
        services.TryAddScoped<IMicroflowExecutionPlanValidator, MicroflowExecutionPlanValidator>();
        services.TryAddScoped<IMicroflowExecutionPlanBuilder, MicroflowExecutionPlanBuilder>();
        services.TryAddScoped<IMicroflowExecutionPlanLoader, MicroflowExecutionPlanLoader>();
        services.TryAddScoped<IMicroflowFlowNavigator, MicroflowFlowNavigator>();

        return services;
    }
}
