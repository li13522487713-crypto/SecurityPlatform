using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowCastExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CastSubclassToParentSucceedsAndDefinesTypedOutputVariable()
    {
        var access = AllowingAccess();
        var executor = new CastObjectActionExecutor(access);
        var context = Context(
            new { sourceVariable = "order", targetEntity = "Sales.Document", outputVariable = "document" },
            Catalog());
        DefineObject(context, "order", "Sales.Order", "order-1");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.True(context.VariableStore.TryGet("document", out var output));
        Assert.Contains("Sales.Document", output!.DataTypeJson);
        await access.Received(1).CanReadAsync(
            Arg.Any<MicroflowRuntimeSecurityContext>(),
            Arg.Is<MicroflowResolvedEntity>(entity => entity.QualifiedName == "Sales.Document"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CastParentToChildWhenRuntimeEntityMatchesSucceeds()
    {
        var executor = new CastObjectActionExecutor(AllowingAccess());
        var context = Context(
            new { sourceVariable = "document", targetEntity = "Sales.Order", outputVariable = "order" },
            Catalog());
        DefineObject(context, "document", "Sales.Order", "order-1", staticType: "Sales.Document");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.True(context.VariableStore.TryGet("order", out var output));
        Assert.Contains("Sales.Order", output!.DataTypeJson);
    }

    [Fact]
    public async Task NullSourceAllowNullWritesNullOutput()
    {
        var executor = new CastObjectActionExecutor(AllowingAccess());
        var context = Context(
            new { sourceVariable = "source", targetEntity = "Sales.Order", outputVariable = "order", castMode = "allowNull" },
            Catalog());
        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = "source",
            DataTypeJson = JsonSerializer.Serialize(new { kind = "object", entityQualifiedName = "Sales.Document" }, JsonOptions),
            RawValueJson = "null",
            ValuePreview = "null"
        });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.True(context.VariableStore.TryGet("order", out var output));
        Assert.Equal("null", output!.RawValueJson);
    }

    [Fact]
    public async Task IncompatibleStrictCastFails()
    {
        var executor = new CastObjectActionExecutor(AllowingAccess());
        var context = Context(
            new { sourceVariable = "customer", targetEntity = "Sales.Order", outputVariable = "order", castMode = "strict" },
            Catalog());
        DefineObject(context, "customer", "Sales.Customer", "customer-1");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeVariableTypeMismatch, result.Error?.Code);
    }

    [Fact]
    public async Task EntityAccessDeniedFails()
    {
        var access = Substitute.For<IMicroflowEntityAccessService>();
        access.CanReadAsync(Arg.Any<MicroflowRuntimeSecurityContext>(), Arg.Any<MicroflowResolvedEntity>(), Arg.Any<CancellationToken>())
            .Returns(new MicroflowEntityAccessDecision
            {
                Allowed = false,
                Operation = "read",
                EntityQualifiedName = "Sales.Document",
                Reason = "denied"
            });
        var executor = new CastObjectActionExecutor(access);
        var context = Context(
            new { sourceVariable = "order", targetEntity = "Sales.Document", outputVariable = "document" },
            Catalog());
        DefineObject(context, "order", "Sales.Order", "order-1");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Failed, result.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeEntityAccessDenied, result.Error?.Code);
    }

    private static IMicroflowEntityAccessService AllowingAccess()
    {
        var access = Substitute.For<IMicroflowEntityAccessService>();
        access.CanReadAsync(Arg.Any<MicroflowRuntimeSecurityContext>(), Arg.Any<MicroflowResolvedEntity>(), Arg.Any<CancellationToken>())
            .Returns(call => new MicroflowEntityAccessDecision
            {
                Allowed = true,
                Operation = "read",
                EntityQualifiedName = call.ArgAt<MicroflowResolvedEntity>(1).QualifiedName
            });
        return access;
    }

    private static MicroflowActionExecutionContext Context(object config, MicroflowMetadataCatalogDto catalog)
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "cast-test",
            SchemaId = "cast-test",
            ResourceId = "mf-cast"
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-cast",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            metadataCatalog: catalog);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "cast-node", ActionId = "cast-action" },
            ObjectId = "cast-node",
            ActionId = "cast-action",
            ActionKind = "cast",
            ActionConfig = JsonSerializer.SerializeToElement(config, JsonOptions),
            VariableStore = runtime.VariableStore,
            MetadataCatalog = catalog,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            Options = new MicroflowActionExecutionOptions { Mode = MicroflowRuntimeExecutionMode.TestRun }
        };
    }

    private static void DefineObject(MicroflowActionExecutionContext context, string variableName, string runtimeEntity, string objectId, string? staticType = null)
    {
        var value = JsonSerializer.SerializeToElement(new { id = objectId, entityType = runtimeEntity }, JsonOptions);
        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = variableName,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "object", entityQualifiedName = staticType ?? runtimeEntity }, JsonOptions),
            RawValueJson = value.GetRawText(),
            ValuePreview = objectId,
            SourceKind = MicroflowVariableSourceKind.Parameter
        });
    }

    private static MicroflowMetadataCatalogDto Catalog()
        => new()
        {
            Entities =
            [
                new MetadataEntityDto
                {
                    QualifiedName = "Sales.Document",
                    Generalization = null,
                    Specializations = ["Sales.Order"]
                },
                new MetadataEntityDto
                {
                    QualifiedName = "Sales.Order",
                    Generalization = "Sales.Document",
                    Specializations = []
                },
                new MetadataEntityDto
                {
                    QualifiedName = "Sales.Customer",
                    Generalization = null,
                    Specializations = []
                }
            ]
        };
}
