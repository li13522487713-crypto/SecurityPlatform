using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime.Connectors;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Infrastructure.Repositories.LowCode;
using Atlas.Infrastructure.Repositories.Microflows;
using Atlas.Infrastructure.Services.Microflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class MicroflowInfrastructureServiceRegistration
{
    public static IServiceCollection AddMicroflowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IMicroflowResourceRepository, MicroflowResourceRepository>();
        services.AddScoped<IMicroflowFolderRepository, MicroflowFolderRepository>();
        services.AddScoped<IMicroflowSchemaSnapshotRepository, MicroflowSchemaSnapshotRepository>();
        services.AddScoped<IMicroflowVersionRepository, MicroflowVersionRepository>();
        services.AddScoped<IMicroflowPublishSnapshotRepository, MicroflowPublishSnapshotRepository>();
        services.AddScoped<IMicroflowReferenceRepository, MicroflowReferenceRepository>();
        services.AddScoped<IMicroflowRunRepository, MicroflowRunRepository>();
        services.AddScoped<IMicroflowMetadataCacheRepository, MicroflowMetadataCacheRepository>();
        services.AddScoped<IMendixDomainModelDocumentRepository, MendixDomainModelDocumentRepository>();
        services.AddScoped<IMicroflowStorageTransaction, MicroflowStorageTransaction>();
        services.AddScoped<IMicroflowRuntimeDbSessionFactory, SqlSugarMicroflowRuntimeDbSessionFactory>();
        services.AddScoped<IDatabaseBackedMicroflowRuntimeObjectStore, SqlSugarMicroflowRuntimeObjectStore>();
        services.AddScoped<IMicroflowRuntimeObjectStore, DomainModelRuntimeObjectStore>();
        services.AddScoped<IWorkflowRuntimeClient, WorkflowRuntimeClientService>();
        services.AddSingleton<ISoapWebServiceConnector, DefaultSoapWebServiceConnector>();
        services.AddSingleton<IXmlMappingConnector, DefaultXmlMappingConnector>();
        services.AddSingleton<IDocumentGenerationRuntime, DefaultDocumentGenerationRuntime>();
        services.AddSingleton<IExternalObjectConnector, DefaultExternalObjectConnector>();
        services.AddScoped<IMicroflowDatabaseUnitOfWork>(sp =>
        {
            var sessionFactory = sp.GetRequiredService<IMicroflowRuntimeDbSessionFactory>();
            var requestContextAccessor = sp.GetRequiredService<IMicroflowRequestContextAccessor>();
            var session = sessionFactory.Create(
                requestContextAccessor.Current,
                parentSession: null,
                transactionBoundary: Atlas.Application.Microflows.Runtime.Calls.MicroflowCallTransactionBoundary.ChildTransaction)
                ?? throw new InvalidOperationException("Runtime DB session could not be created.");
            return new SqlSugarMicroflowDatabaseUnitOfWork(session);
        });

        services.AddScoped<IMicroflowResourceQueryService, MicroflowDbResourceQueryService>();
        services.AddScoped<IMicroflowMetadataQueryService, MicroflowDbMetadataQueryService>();
        services.AddScoped<IMicroflowStorageDiagnosticsService, MicroflowStorageDiagnosticsService>();
        services.AddScoped<IMicroflowAppAssetService, MicroflowAppAssetService>();
        services.AddScoped<IMendixDomainModelService, MendixDomainModelService>();
        services.AddHostedService<MicroflowSeedDataHostedService>();
        services.AddHostedService<MicroflowMetadataSeedHostedService>();

        return services;
    }
}
