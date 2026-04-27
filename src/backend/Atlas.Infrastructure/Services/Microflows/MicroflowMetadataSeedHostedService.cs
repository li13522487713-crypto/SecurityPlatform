using System.Text.Json;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using Atlas.Domain.Microflows.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class MicroflowMetadataSeedHostedService : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MicroflowMetadataSeedHostedService> _logger;

    public MicroflowMetadataSeedHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<MicroflowMetadataSeedHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue("Microflows:Metadata:SeedEnabled", _environment.IsDevelopment());
        if (!enabled)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMicroflowMetadataCacheRepository>();
        var workspaceId = _configuration.GetValue<string?>("Microflows:Metadata:SeedWorkspaceId") ?? "demo-workspace";
        var tenantId = _configuration.GetValue<string?>("Microflows:Metadata:SeedTenantId") ?? "demo-tenant";
        var forceSeed = _configuration.GetValue("Microflows:Metadata:ForceSeed", false);
        var count = await repository.CountAsync(workspaceId, tenantId, cancellationToken);
        if (count > 0 && !forceSeed)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var catalog = MicroflowSeedMetadataCatalog.Create(now);
        await repository.UpsertLatestAsync(
            new MicroflowMetadataCacheEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                WorkspaceId = workspaceId,
                TenantId = tenantId,
                CatalogVersion = MicroflowSeedMetadataCatalog.Version,
                CatalogJson = JsonSerializer.Serialize(catalog, JsonOptions),
                UpdatedAt = now,
                UpdatedBy = "metadata-seed",
                Source = "seed"
            },
            cancellationToken);

        _logger.LogInformation(
            "[MicroflowMetadataSeed] Seeded metadata catalog {CatalogVersion} for workspace {WorkspaceId}.",
            MicroflowSeedMetadataCatalog.Version,
            workspaceId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
