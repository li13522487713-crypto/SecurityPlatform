using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class MicroflowDbResourceQueryService : IMicroflowResourceQueryService
{
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;

    public MicroflowDbResourceQueryService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
    }

    public async Task<MicroflowApiPageResult<MicroflowResourceDto>> GetPagedAsync(
        MicroflowResourceQueryDto query,
        CancellationToken cancellationToken)
    {
        var items = await _resourceRepository.ListAsync(query, cancellationToken);
        var total = await _resourceRepository.CountAsync(query, cancellationToken);
        var dtos = items.Select(entity => MicroflowResourceMapper.ToDto(entity)).ToArray();
        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        return new MicroflowApiPageResult<MicroflowResourceDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
            HasMore = pageIndex * pageSize < total
        };
    }

    public async Task<MicroflowResourceDto?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await _resourceRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        MicroflowSchemaSnapshotEntity? snapshot = null;
        if (!string.IsNullOrWhiteSpace(entity.CurrentSchemaSnapshotId))
        {
            snapshot = await _schemaSnapshotRepository.GetByIdAsync(entity.CurrentSchemaSnapshotId, cancellationToken);
        }

        snapshot ??= await _schemaSnapshotRepository.GetLatestByResourceIdAsync(entity.Id, cancellationToken);
        return MicroflowResourceMapper.ToDto(entity, snapshot);
    }
}

public sealed class MicroflowDbMetadataQueryService : IMicroflowMetadataQueryService
{
    private readonly IMicroflowMetadataCacheRepository _metadataCacheRepository;

    public MicroflowDbMetadataQueryService(IMicroflowMetadataCacheRepository metadataCacheRepository)
    {
        _metadataCacheRepository = metadataCacheRepository;
    }

    public async Task<MicroflowMetadataCatalogDto> GetCatalogAsync(MicroflowMetadataQueryDto query, CancellationToken cancellationToken)
    {
        var cache = await _metadataCacheRepository.GetLatestAsync(query.WorkspaceId, null, cancellationToken);
        if (cache is null || string.IsNullOrWhiteSpace(cache.CatalogJson))
        {
            return new MicroflowMetadataCatalogDto
            {
                Version = "backend-skeleton",
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        try
        {
            return JsonSerializer.Deserialize<MicroflowMetadataCatalogDto>(
                    cache.CatalogJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new MicroflowMetadataCatalogDto { Version = cache.CatalogVersion, UpdatedAt = cache.UpdatedAt };
        }
        catch (JsonException)
        {
            return new MicroflowMetadataCatalogDto
            {
                Version = cache.CatalogVersion,
                UpdatedAt = cache.UpdatedAt
            };
        }
    }
}

public sealed class MicroflowStorageDiagnosticsService : IMicroflowStorageDiagnosticsService
{
    private static readonly string[] TableNames =
    [
        "MicroflowResource",
        "MicroflowSchemaSnapshot",
        "MicroflowVersion",
        "MicroflowPublishSnapshot",
        "MicroflowReference",
        "MicroflowRunSession",
        "MicroflowRunTraceFrame",
        "MicroflowRunLog",
        "MicroflowMetadataCache",
        "MicroflowSchemaMigration"
    ];

    private readonly ISqlSugarClient _db;

    public MicroflowStorageDiagnosticsService(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task<MicroflowStorageHealthDto> GetHealthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tables = TableNames
            .Select(name => new MicroflowStorageTableHealthDto
            {
                Name = name,
                Exists = _db.DbMaintenance.IsAnyTable(name, false)
            })
            .ToArray();

        var status = tables.All(static table => table.Exists) ? "ok" : "degraded";
        return Task.FromResult(new MicroflowStorageHealthDto
        {
            Status = status,
            Provider = $"SqlSugar/{_db.CurrentConnectionConfig?.DbType.ToString() ?? "unknown"}",
            Tables = tables
        });
    }
}

public sealed class MicroflowSeedDataHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MicroflowSeedDataHostedService> _logger;

    public MicroflowSeedDataHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<MicroflowSeedDataHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue<bool>("Microflows:SeedData:Enabled");
        if (!enabled || !_environment.IsDevelopment())
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var resourceRepository = scope.ServiceProvider.GetRequiredService<IMicroflowResourceRepository>();
        var snapshotRepository = scope.ServiceProvider.GetRequiredService<IMicroflowSchemaSnapshotRepository>();

        const string seedId = "mf-seed-blank";
        if (await resourceRepository.GetByIdAsync(seedId, cancellationToken) is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var schemaJson = """
            {
              "schemaVersion": "1.0",
              "id": "mf-seed-blank-schema",
              "name": "SeedBlankMicroflow",
              "displayName": "Seed Blank Microflow",
              "objects": [],
              "flows": [],
              "parameters": [],
              "variables": [],
              "returnType": { "kind": "void" }
            }
            """;
        var snapshot = new MicroflowSchemaSnapshotEntity
        {
            Id = "mfs-seed-blank",
            ResourceId = seedId,
            WorkspaceId = "demo-workspace",
            TenantId = "demo-tenant",
            SchemaVersion = "1.0",
            MigrationVersion = "backend-skeleton",
            SchemaJson = schemaJson,
            SchemaHash = "seed",
            CreatedBy = "seed",
            CreatedAt = now,
            Reason = "development seed"
        };
        var resource = new MicroflowResourceEntity
        {
            Id = seedId,
            WorkspaceId = "demo-workspace",
            TenantId = "demo-tenant",
            ModuleId = "demo-module",
            ModuleName = "Demo",
            Name = "SeedBlankMicroflow",
            DisplayName = "Seed Blank Microflow",
            Description = "Development-only microflow seed generated by backend skeleton.",
            TagsJson = JsonSerializer.Serialize(new[] { "seed", "skeleton" }),
            OwnerId = "seed",
            OwnerName = "Seed",
            CreatedBy = "seed",
            CreatedAt = now,
            UpdatedBy = "seed",
            UpdatedAt = now,
            Version = "0.1.0",
            Status = "draft",
            PublishStatus = "neverPublished",
            Favorite = false,
            Archived = false,
            ReferenceCount = 0,
            LastRunStatus = "neverRun",
            SchemaId = snapshot.Id,
            CurrentSchemaSnapshotId = snapshot.Id,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        await snapshotRepository.InsertAsync(snapshot, cancellationToken);
        await resourceRepository.InsertAsync(resource, cancellationToken);
        _logger.LogInformation("[MicroflowSeedData] Seeded development microflow resource {ResourceId}.", seedId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
