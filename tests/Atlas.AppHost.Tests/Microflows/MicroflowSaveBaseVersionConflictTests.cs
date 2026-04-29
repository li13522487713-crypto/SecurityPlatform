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

public sealed class MicroflowSaveBaseVersionConflictTests
{
    [Fact]
    public async Task SaveSchemaAsync_WhenBaseVersionIsStale_Returns409WithRemoteDetails()
    {
        var fixture = Build();
        var resource = Resource();
        var snapshot = Snapshot("schema-current");
        fixture.ResourceRepo.GetByIdAsync(resource.Id, Arg.Any<CancellationToken>()).Returns(resource);
        fixture.SnapshotRepo.GetByIdAsync(resource.CurrentSchemaSnapshotId!, Arg.Any<CancellationToken>()).Returns(snapshot);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => fixture.Service.SaveSchemaAsync(
            resource.Id,
            new SaveMicroflowSchemaRequestDto
            {
                Schema = MinimalSchema(resource.Id),
                BaseVersion = "schema-stale",
                ClientRequestId = "req-conflict"
            },
            CancellationToken.None));

        Assert.Equal(409, ex.HttpStatus);
        Assert.Equal(MicroflowApiErrorCode.MicroflowVersionConflict, ex.Code);
        Assert.NotNull(ex.Details);
        using var details = JsonDocument.Parse(ex.Details!);
        Assert.Equal(resource.Version, details.RootElement.GetProperty("remoteVersion").GetString());
        Assert.Equal(snapshot.Id, details.RootElement.GetProperty("remoteSchemaId").GetString());
        Assert.Equal(resource.UpdatedBy, details.RootElement.GetProperty("remoteUpdatedBy").GetString());
        Assert.Equal("schema-stale", details.RootElement.GetProperty("baseVersion").GetString());
        await fixture.SnapshotRepo.DidNotReceive().InsertAsync(Arg.Any<MicroflowSchemaSnapshotEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveSchemaAsync_WithSameClientRequestId_ReplaysCachedResultWithoutNewSnapshot()
    {
        var fixture = Build();
        var resource = Resource();
        var snapshot = Snapshot(resource.CurrentSchemaSnapshotId!);
        resource.ExtraJson = JsonSerializer.Serialize(new
        {
            microflow = new
            {
                lastSaveIdempotency = new
                {
                    clientRequestId = "req-1",
                    schemaId = snapshot.Id,
                    schemaVersion = snapshot.SchemaVersion,
                    saveReason = "autosave",
                    updatedAt = resource.UpdatedAt,
                    savedAt = DateTimeOffset.UtcNow,
                    changedAfterPublish = false
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        fixture.ResourceRepo.GetByIdAsync(resource.Id, Arg.Any<CancellationToken>()).Returns(resource);
        fixture.SnapshotRepo.GetByIdAsync(resource.CurrentSchemaSnapshotId!, Arg.Any<CancellationToken>()).Returns(snapshot);

        var result = await fixture.Service.SaveSchemaAsync(
            resource.Id,
            new SaveMicroflowSchemaRequestDto
            {
                Schema = MinimalSchema(resource.Id),
                BaseVersion = resource.SchemaId,
                ClientRequestId = "req-1",
                SaveReason = "autosave"
            },
            CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Equal("req-1", result.ClientRequestId);
        Assert.Equal("autosave", result.SaveReason);
        await fixture.SnapshotRepo.DidNotReceive().InsertAsync(Arg.Any<MicroflowSchemaSnapshotEntity>(), Arg.Any<CancellationToken>());
        await fixture.ResourceRepo.DidNotReceive().UpdateAsync(Arg.Any<MicroflowResourceEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveSchemaAsync_RemembersClientRequestIdAndSaveReason()
    {
        var fixture = Build();
        var resource = Resource();
        var snapshot = Snapshot(resource.CurrentSchemaSnapshotId!);
        fixture.ResourceRepo.GetByIdAsync(resource.Id, Arg.Any<CancellationToken>()).Returns(resource);
        fixture.SnapshotRepo.GetByIdAsync(resource.CurrentSchemaSnapshotId!, Arg.Any<CancellationToken>()).Returns(snapshot);

        var result = await fixture.Service.SaveSchemaAsync(
            resource.Id,
            new SaveMicroflowSchemaRequestDto
            {
                Schema = MinimalSchema(resource.Id),
                BaseVersion = snapshot.Id,
                ClientRequestId = "req-2",
                SaveReason = "manual-save"
            },
            CancellationToken.None);

        Assert.False(result.IdempotentReplay);
        Assert.Equal("req-2", result.ClientRequestId);
        Assert.Equal("manual-save", result.SaveReason);
        await fixture.SnapshotRepo.Received(1).InsertAsync(
            Arg.Is<MicroflowSchemaSnapshotEntity>(entity => entity.Reason == "manual-save" && entity.BaseVersion == snapshot.Id),
            Arg.Any<CancellationToken>());
        await fixture.ResourceRepo.Received(1).UpdateAsync(
            Arg.Is<MicroflowResourceEntity>(entity => entity.ExtraJson != null && entity.ExtraJson.Contains("req-2", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("nodes")]
    [InlineData("edges")]
    [InlineData("workflowJson")]
    [InlineData("flowgram")]
    public async Task SaveSchemaAsync_WhenSchemaContainsFlowGramShape_RejectsAndDoesNotPersist(string forbiddenProperty)
    {
        var fixture = Build();
        var resource = Resource();
        var snapshot = Snapshot(resource.CurrentSchemaSnapshotId!);
        fixture.ResourceRepo.GetByIdAsync(resource.Id, Arg.Any<CancellationToken>()).Returns(resource);
        fixture.SnapshotRepo.GetByIdAsync(resource.CurrentSchemaSnapshotId!, Arg.Any<CancellationToken>()).Returns(snapshot);

        using var doc = JsonDocument.Parse(MinimalSchemaWithForbiddenProperty(resource.Id, forbiddenProperty));
        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => fixture.Service.SaveSchemaAsync(
            resource.Id,
            new SaveMicroflowSchemaRequestDto
            {
                Schema = doc.RootElement.Clone(),
                BaseVersion = snapshot.Id,
                ClientRequestId = $"req-{forbiddenProperty}",
                SaveReason = "manual-save"
            },
            CancellationToken.None));

        Assert.Equal(MicroflowApiErrorCode.MicroflowSchemaInvalid, ex.Code);
        await fixture.SnapshotRepo.DidNotReceive().InsertAsync(Arg.Any<MicroflowSchemaSnapshotEntity>(), Arg.Any<CancellationToken>());
        await fixture.ResourceRepo.DidNotReceive().UpdateAsync(Arg.Any<MicroflowResourceEntity>(), Arg.Any<CancellationToken>());
    }

    private static (MicroflowResourceService Service, IMicroflowResourceRepository ResourceRepo, IMicroflowSchemaSnapshotRepository SnapshotRepo) Build()
    {
        var resourceRepo = Substitute.For<IMicroflowResourceRepository>();
        var folderRepo = Substitute.For<IMicroflowFolderRepository>();
        var snapshotRepo = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var referenceRepo = Substitute.For<IMicroflowReferenceRepository>();
        var indexer = Substitute.For<IMicroflowReferenceIndexer>();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "100",
            TenantId = "tenant-1",
            UserId = "user-1",
            UserName = "Tester",
            TraceId = "trace-save"
        });
        var clock = Substitute.For<IMicroflowClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));
        return (new MicroflowResourceService(
            resourceRepo,
            folderRepo,
            snapshotRepo,
            referenceRepo,
            indexer,
            accessor,
            new NullMicroflowAuditWriter(),
            clock), resourceRepo, snapshotRepo);
    }

    private static MicroflowResourceEntity Resource()
        => new()
        {
            Id = "mf-1",
            WorkspaceId = "100",
            TenantId = "tenant-1",
            ModuleId = "sales",
            Name = "OrderSubmit",
            DisplayName = "Order Submit",
            Version = "0.1.5",
            SchemaId = "schema-current",
            CurrentSchemaSnapshotId = "schema-current",
            ConcurrencyStamp = "stamp-current",
            UpdatedAt = new DateTimeOffset(2026, 4, 29, 1, 2, 3, TimeSpan.Zero),
            UpdatedBy = "remote-user"
        };

    private static MicroflowSchemaSnapshotEntity Snapshot(string id)
        => new()
        {
            Id = id,
            ResourceId = "mf-1",
            WorkspaceId = "100",
            TenantId = "tenant-1",
            SchemaVersion = "1.0.0",
            SchemaJson = MinimalSchema("mf-1").GetRawText(),
            CreatedAt = new DateTimeOffset(2026, 4, 29, 1, 0, 0, TimeSpan.Zero),
            CreatedBy = "remote-user"
        };

    private static JsonElement MinimalSchema(string id)
        => JsonSerializer.SerializeToElement(new
        {
            schemaVersion = "1.0.0",
            id,
            name = "OrderSubmit",
            objectCollection = new { objects = Array.Empty<object>() },
            flows = Array.Empty<object>(),
            parameters = Array.Empty<object>(),
            returnType = new { kind = "void" }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string MinimalSchemaWithForbiddenProperty(string id, string forbiddenProperty)
    {
        var node = JsonSerializer.SerializeToNode(new
        {
            schemaVersion = "1.0.0",
            id,
            name = "OrderSubmit",
            objectCollection = new { objects = Array.Empty<object>() },
            flows = Array.Empty<object>(),
            parameters = Array.Empty<object>(),
            returnType = new { kind = "void" }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        node[forbiddenProperty] = forbiddenProperty == "workflowJson"
            ? "{}"
            : JsonSerializer.SerializeToNode(Array.Empty<object>());
        return node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
