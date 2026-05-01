using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowValidationCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ValidateAsync_Allows_LoopBody_Entry_And_TopLevel_Boolean_CaseValues()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-loop-body-compat",
            ["name"] = "mf-loop-body-compat",
            ["displayName"] = "mf-loop-body-compat",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("loop", "loopedActivity", "root-collection", new JsonObject
                    {
                        ["bodyCollectionId"] = "loop-body",
                        ["loopSource"] = new JsonObject { ["kind"] = "whileCondition", ["expression"] = "false" }
                    }),
                    Node("decision", "exclusiveSplit", "loop-body", new JsonObject
                    {
                        ["splitCondition"] = new JsonObject { ["expression"] = "true", ["resultType"] = "boolean" }
                    }),
                    Node("break", "breakEvent", "loop-body"),
                    Node("continue", "continueEvent", "loop-body"),
                    Node("end", "endEvent", "root-collection")
                },
                ["edges"] = new JsonArray
                {
                    Edge("f-start-loop", "start", "loop"),
                    Edge("f-loop-end", "loop", "end"),
                    Edge("f-loop-body", "loop", "decision", "loopBody", "loop-body"),
                    Edge("f-true", "decision", "break", "decisionCondition", "loop-body", topLevelCaseValues: true, value: true),
                    Edge("f-false", "decision", "continue", "decisionCondition", "loop-body", topLevelCaseValues: true, value: false)
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-loop-body-compat",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == MicroflowValidationCodes.FlowInvalidTarget);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == MicroflowValidationCodes.DecisionBooleanTrueMissing);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == MicroflowValidationCodes.DecisionBooleanFalseMissing);
    }

    private static MicroflowValidationService CreateValidationService()
    {
        var metadata = Substitute.For<IMicroflowMetadataService>();
        metadata.GetCatalogAsync(Arg.Any<GetMicroflowMetadataRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new MicroflowMetadataCatalogDto { UpdatedAt = DateTimeOffset.UtcNow });
        var context = Substitute.For<IMicroflowRequestContextAccessor>();
        context.Current.Returns(new MicroflowRequestContext { WorkspaceId = "workspace-test" });
        return new MicroflowValidationService(
            Substitute.For<IMicroflowResourceRepository>(),
            Substitute.For<IMicroflowSchemaSnapshotRepository>(),
            metadata,
            new MicroflowSchemaReader(),
            new MicroflowActionSupportMatrix(),
            context,
            new TestClock());
    }

    private static JsonObject Node(string id, string kind, string collectionId, JsonObject? extraData = null)
    {
        var data = new JsonObject
        {
            ["objectId"] = id,
            ["objectKind"] = kind,
            ["collectionId"] = collectionId,
            ["title"] = id,
            ["officialType"] = $"Microflows${kind}",
        };
        if (extraData is not null)
        {
            foreach (var property in extraData)
            {
                data[property.Key] = property.Value?.DeepClone();
            }
        }

        return new JsonObject
        {
            ["id"] = id,
            ["type"] = kind,
            ["data"] = data,
            ["meta"] = new JsonObject
            {
                ["collectionId"] = collectionId,
                ["position"] = new JsonObject { ["x"] = 0, ["y"] = 0 }
            }
        };
    }

    private static JsonObject Edge(
        string id,
        string source,
        string target,
        string edgeKind = "sequence",
        string collectionId = "root-collection",
        bool topLevelCaseValues = false,
        bool? value = null)
    {
        var edge = new JsonObject
        {
            ["id"] = id,
            ["sourceNodeID"] = source,
            ["targetNodeID"] = target,
            ["edgeKind"] = edgeKind,
            ["data"] = new JsonObject
            {
                ["flowId"] = id,
                ["flowKind"] = "sequence",
                ["edgeKind"] = edgeKind,
                ["collectionId"] = collectionId
            }
        };
        if (value.HasValue)
        {
            var cases = new JsonArray
            {
                new JsonObject { ["kind"] = "boolean", ["value"] = value.Value }
            };
            if (topLevelCaseValues)
            {
                edge["caseValues"] = cases;
            }
            else
            {
                ((JsonObject)edge["data"]!)["caseValues"] = cases;
            }
        }

        return edge;
    }

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
