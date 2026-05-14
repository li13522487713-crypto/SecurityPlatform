using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Audit;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using Atlas.Domain.Microflows.Entities;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowDesignSchemaStrictnessTests
{
    [Fact]
    public async Task GetSchemaAsync_WhenSnapshotIsLegacyRuntimeSchema_AutoUpgradesAndSavesNewSnapshot()
    {
        var fixture = BuildResourceFixture();
        fixture.ResourceRepo.GetByIdAsync("mf-1", Arg.Any<CancellationToken>()).Returns(CreateResource("schema-legacy"));
        fixture.SnapshotRepo.GetByIdAsync("schema-legacy", Arg.Any<CancellationToken>()).Returns(new MicroflowSchemaSnapshotEntity
        {
            Id = "schema-legacy",
            ResourceId = "mf-1",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            SchemaVersion = "1.0.0",
            SchemaJson = CreateLegacyRuntimeSchema("mf-1"),
            CreatedBy = "tester",
            CreatedAt = new DateTimeOffset(2026, 4, 30, 1, 0, 0, TimeSpan.Zero),
        });

        // 服务现行为：旧 schema 自动升级回写，不抛错
        var result = await fixture.Service.GetSchemaAsync("mf-1", CancellationToken.None);

        // 返回升级后的 schema，版本号为最新设计态协议版本
        Assert.Equal("flowgram.microflow.v1", result.SchemaVersion);
        // 新快照应当被持久化
        await fixture.SnapshotRepo.Received(1).InsertAsync(
            Arg.Is<MicroflowSchemaSnapshotEntity>(s => s.MigrationVersion == "legacy-schema-auto-upgrade"),
            Arg.Any<CancellationToken>());
        // 资源记录应当被更新（指向新快照）
        await fixture.ResourceRepo.Received(1).UpdateAsync(
            Arg.Any<MicroflowResourceEntity>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_KeepsCurrentDesignSnapshotAndStoresDesignPublishSnapshot()
    {
        var resourceRepo = Substitute.For<IMicroflowResourceRepository>();
        var snapshotRepo = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var versionRepo = Substitute.For<IMicroflowVersionRepository>();
        var publishSnapshotRepo = Substitute.For<IMicroflowPublishSnapshotRepository>();
        var referenceRepo = Substitute.For<IMicroflowReferenceRepository>();
        var impactService = Substitute.For<IMicroflowPublishImpactService>();
        var validationService = Substitute.For<IMicroflowValidationService>();
        var transaction = Substitute.For<IMicroflowStorageTransaction>();
        var requestContextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        var clock = Substitute.For<IMicroflowClock>();

        var now = new DateTimeOffset(2026, 4, 30, 2, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        requestContextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            UserName = "Tester",
            TraceId = "trace-publish-design"
        });
        transaction.ExecuteAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<Task>>().Invoke());

        var resource = CreateResource("schema-design");
        var currentSnapshot = new MicroflowSchemaSnapshotEntity
        {
            Id = "schema-design",
            ResourceId = resource.Id,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            SchemaVersion = "flowgram.microflow.v1",
            SchemaJson = CreateDesignSchema(resource.Id),
            CreatedBy = "tester",
            CreatedAt = now.AddMinutes(-10),
        };

        resourceRepo.GetByIdAsync(resource.Id, Arg.Any<CancellationToken>()).Returns(resource);
        snapshotRepo.GetByIdAsync("schema-design", Arg.Any<CancellationToken>()).Returns(currentSnapshot);
        versionRepo.GetByResourceVersionAsync(resource.Id, "1.2.3", Arg.Any<CancellationToken>()).Returns((MicroflowVersionEntity?)null);
        publishSnapshotRepo.GetLatestByResourceIdAsync(resource.Id, Arg.Any<CancellationToken>()).Returns((MicroflowPublishSnapshotEntity?)null);
        referenceRepo.ListByTargetMicroflowIdAsync(resource.Id, false, Arg.Any<CancellationToken>()).Returns(Array.Empty<MicroflowReferenceEntity>());
        validationService.ValidateAsync(resource.Id, Arg.Any<ValidateMicroflowRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateMicroflowResponseDto
            {
                Issues = Array.Empty<MicroflowValidationIssueDto>(),
                Summary = new MicroflowValidationSummaryDto()
            });
        impactService.Analyze(Arg.Any<MicroflowResourceEntity>(), Arg.Any<JsonElement>(), Arg.Any<JsonElement?>(), Arg.Any<IReadOnlyList<MicroflowReferenceEntity>>(), "1.2.3")
            .Returns(new MicroflowPublishImpactAnalysisDto
            {
                ResourceId = resource.Id,
                CurrentVersion = resource.Version,
                NextVersion = "1.2.3",
                References = Array.Empty<MicroflowReferenceDto>(),
                BreakingChanges = Array.Empty<MicroflowBreakingChangeDto>(),
                ImpactLevel = "none",
                Summary = new MicroflowPublishImpactSummaryDto()
            });

        var service = new MicroflowPublishService(
            resourceRepo,
            snapshotRepo,
            versionRepo,
            publishSnapshotRepo,
            referenceRepo,
            impactService,
            validationService,
            transaction,
            requestContextAccessor,
            new NullMicroflowAuditWriter(),
            clock);

        await service.PublishAsync(resource.Id, new PublishMicroflowApiRequestDto
        {
            Version = "1.2.3",
            Description = "publish note",
            ConfirmBreakingChanges = true
        }, CancellationToken.None);

        await snapshotRepo.DidNotReceive().InsertAsync(Arg.Any<MicroflowSchemaSnapshotEntity>(), Arg.Any<CancellationToken>());
        await versionRepo.Received(1).InsertAsync(
            Arg.Is<MicroflowVersionEntity>(entity => entity.SchemaSnapshotId == currentSnapshot.Id && entity.Version == "1.2.3"),
            Arg.Any<CancellationToken>());
        await publishSnapshotRepo.Received(1).InsertAsync(
            Arg.Is<MicroflowPublishSnapshotEntity>(entity =>
                entity.SchemaSnapshotId == currentSnapshot.Id
                && entity.Version == "1.2.3"
                && entity.SchemaJson.Contains("\"workflow\"", StringComparison.Ordinal)
                && !entity.SchemaJson.Contains("\"objectCollection\"", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await resourceRepo.Received(1).UpdateAsync(
            Arg.Is<MicroflowResourceEntity>(entity =>
                entity.CurrentSchemaSnapshotId == currentSnapshot.Id
                && entity.SchemaId == currentSnapshot.Id
                && entity.Version == "1.2.3"
                && entity.PublishStatus == "published"),
            Arg.Any<CancellationToken>());
    }

    private static (MicroflowResourceService Service, IMicroflowResourceRepository ResourceRepo, IMicroflowSchemaSnapshotRepository SnapshotRepo) BuildResourceFixture()
    {
        var resourceRepo = Substitute.For<IMicroflowResourceRepository>();
        var folderRepo = Substitute.For<IMicroflowFolderRepository>();
        var snapshotRepo = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var referenceRepo = Substitute.For<IMicroflowReferenceRepository>();
        var referenceIndexer = Substitute.For<IMicroflowReferenceIndexer>();
        var requestContextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        var clock = Substitute.For<IMicroflowClock>();
        requestContextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            UserName = "Tester",
            TraceId = "trace-resource-design"
        });

        var service = new MicroflowResourceService(
            resourceRepo,
            folderRepo,
            snapshotRepo,
            referenceRepo,
            referenceIndexer,
            requestContextAccessor,
            new NullMicroflowAuditWriter(),
            clock);

        return (service, resourceRepo, snapshotRepo);
    }

    private static MicroflowResourceEntity CreateResource(string schemaId)
        => new()
        {
            Id = "mf-1",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            ModuleId = "sales",
            ModuleName = "Sales",
            Name = "OrderSubmit",
            DisplayName = "Order Submit",
            Version = "0.1.0",
            Status = "draft",
            PublishStatus = "neverPublished",
            CurrentSchemaSnapshotId = schemaId,
            SchemaId = schemaId,
            UpdatedAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero),
            UpdatedBy = "tester",
            CreatedAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "tester",
            OwnerId = "user-1",
            OwnerName = "Tester",
        };

    private static string CreateDesignSchema(string id)
        => JsonSerializer.Serialize(new
        {
            schemaVersion = "flowgram.microflow.v1",
            id,
            stableId = id,
            name = "OrderSubmit",
            displayName = "Order Submit",
            description = "",
            moduleId = "sales",
            moduleName = "Sales",
            workflow = new
            {
                nodes = new object[]
                {
                    new
                    {
                        id = "start",
                        type = "startEvent",
                        data = new
                        {
                            objectId = "start",
                            objectKind = "startEvent",
                            collectionId = "root-collection",
                            title = "Start",
                            officialType = "Microflows$StartEvent"
                        },
                        meta = new
                        {
                            position = new { x = 320, y = 220 },
                            size = new { width = 132, height = 70 }
                        }
                    },
                    new
                    {
                        id = "end",
                        type = "endEvent",
                        data = new
                        {
                            objectId = "end",
                            objectKind = "endEvent",
                            collectionId = "root-collection",
                            title = "End",
                            officialType = "Microflows$EndEvent"
                        },
                        meta = new
                        {
                            position = new { x = 620, y = 220 },
                            size = new { width = 132, height = 70 }
                        }
                    }
                },
                edges = Array.Empty<object>()
            },
            parameters = Array.Empty<object>(),
            returnType = new { kind = "void" },
            variables = Array.Empty<object>(),
            validation = new { issues = Array.Empty<object>() },
            editor = new { viewport = new { x = 0, y = 0, zoom = 1 }, selection = new { } },
            audit = new
            {
                version = "0.1.0",
                status = "draft",
                createdBy = "tester",
                createdAt = "2026-04-30T00:00:00.000Z",
                updatedBy = "tester",
                updatedAt = "2026-04-30T00:00:00.000Z"
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string CreateLegacyRuntimeSchema(string id)
        => JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0.0",
            mendixProfile = "mx10",
            id,
            stableId = id,
            name = "OrderSubmit",
            displayName = "Order Submit",
            moduleId = "sales",
            moduleName = "Sales",
            parameters = Array.Empty<object>(),
            returnType = new { kind = "void" },
            objectCollection = new
            {
                id = "root-collection",
                officialType = "Microflows$MicroflowObjectCollection",
                objects = Array.Empty<object>()
            },
            flows = Array.Empty<object>()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
