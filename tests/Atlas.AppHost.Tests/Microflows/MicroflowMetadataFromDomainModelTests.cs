using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowMetadataFromDomainModelTests
{
    [Fact]
    public async Task GetCatalogAsync_Merges_DomainModel_Entities_Into_Metadata_Catalog()
    {
        var cacheRepository = Substitute.For<IMicroflowMetadataCacheRepository>();
        cacheRepository.GetLatestAsync("workspace-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns((Atlas.Domain.Microflows.Entities.MicroflowMetadataCacheEntity?)null);
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        resourceRepository.ListAsync(Arg.Any<MicroflowResourceQueryDto>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Atlas.Domain.Microflows.Entities.MicroflowResourceEntity>());
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        snapshotRepository.ListByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Atlas.Domain.Microflows.Entities.MicroflowSchemaSnapshotEntity>());
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            TraceId = "trace-domain-model-metadata"
        });
        var domainModelService = Substitute.For<IMendixDomainModelService>();
        domainModelService.GetMetadataCatalogAsync("app-1", "workspace-1", "sales", Arg.Any<CancellationToken>())
            .Returns(new MendixDomainModelMetadataCatalogDto
            {
                Entities =
                [
                    new MetadataEntityDto
                    {
                        Id = "entity:sales-order",
                        Name = "Order",
                        QualifiedName = "db1.main.sales_order",
                        ModuleId = "sales",
                        ModuleName = "sales",
                        BindingId = "binding:1",
                        SourceId = "ai:1",
                        AiDatabaseId = "1",
                        DriverCode = "SQLite",
                        SchemaName = "main",
                        TableName = "sales_order",
                        Attributes =
                        [
                            new MetadataAttributeDto
                            {
                                Id = "attr:id",
                                Name = "id",
                                QualifiedName = "db1.main.sales_order.id",
                                ColumnName = "id",
                                PrimaryKey = true,
                                Type = JsonSerializer.SerializeToElement(new { kind = "string" })
                            }
                        ]
                    }
                ]
            });

        var service = new MicroflowMetadataService(
            cacheRepository,
            resourceRepository,
            snapshotRepository,
            accessor,
            new FixedClock(),
            domainModelService);

        var catalog = await service.GetCatalogAsync(new GetMicroflowMetadataRequestDto
        {
            AppId = "app-1",
            WorkspaceId = "workspace-1",
            ModuleId = "sales",
            IncludeSystem = true
        }, CancellationToken.None);

        var entity = Assert.Single(catalog.Entities, item => item.QualifiedName == "db1.main.sales_order");
        Assert.Equal("sales_order", entity.TableName);
        Assert.Equal("1", entity.AiDatabaseId);
        Assert.Equal("id", entity.Attributes[0].ColumnName);
        Assert.True(entity.Attributes[0].PrimaryKey);
    }

    private sealed class FixedClock : Atlas.Application.Microflows.Infrastructure.IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
