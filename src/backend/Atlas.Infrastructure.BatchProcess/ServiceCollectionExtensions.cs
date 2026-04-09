using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Application.BatchProcess.Validators;
using Atlas.Infrastructure.BatchProcess.Repositories;
using Atlas.Infrastructure.BatchProcess.Recovery;
using Atlas.Infrastructure.BatchProcess.Scanning;
using Atlas.Infrastructure.BatchProcess.Scheduling;
using Atlas.Infrastructure.BatchProcess.Services;
using Atlas.Infrastructure.BatchProcess.Sharding;
using Atlas.Infrastructure.BatchProcess.Splitting;
using Atlas.Infrastructure.BatchProcess.Options;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.BatchProcess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBatchProcessInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BatchProcessRuntimeOptions>(options =>
        {
            var configuredValue = configuration[$"{BatchProcessRuntimeOptions.SectionName}:{nameof(BatchProcessRuntimeOptions.EnableExecution)}"];
            options.EnableExecution = bool.TryParse(configuredValue, out var parsed) && parsed;
        });

        services.AddScoped<IBatchJobRepository, BatchJobRepository>();
        services.AddScoped<IBatchDeadLetterRepository, BatchDeadLetterRepository>();
        services.AddScoped<IBatchCheckpointRepository, BatchCheckpointRepository>();

        services.AddScoped<IBatchJobQueryService, BatchJobQueryService>();
        services.AddScoped<IBatchJobCommandService, BatchJobCommandService>();
        services.AddScoped<IBatchDeadLetterQueryService, BatchDeadLetterQueryService>();
        services.AddScoped<IBatchDeadLetterCommandService, BatchDeadLetterCommandService>();
        services.AddScoped<ICheckpointService, CheckpointService>();
        services.AddScoped<IValidator<BatchJobCreateRequest>, BatchJobCreateRequestValidator>();
        services.AddScoped<IValidator<BatchJobUpdateRequest>, BatchJobUpdateRequestValidator>();

        services.AddScoped<IKeysetScanner, KeysetScanner>();
        services.AddScoped<IPrimaryKeyRangeSharder, PrimaryKeyRangeSharder>();
        services.AddSingleton<ITimeWindowSharder, TimeWindowSharder>();
        services.AddSingleton<IBatchSplitter, BatchSplitter>();
        services.AddSingleton<IWorkerPool>(_ => new WorkerPool(Math.Max(1, Environment.ProcessorCount)));
        services.AddSingleton<IBackpressurePolicy, BackpressurePolicy>();

        services.AddScoped<IShardRecoveryService, ShardRecoveryService>();
        services.AddScoped<IBatchRetryService, BatchRetryService>();

        return services;
    }
}
