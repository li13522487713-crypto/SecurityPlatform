using System.Text.Json;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowSchemaMigrationServiceTests
{
    [Fact]
    public void NormalizeForLoad_RewritesLegacyActionKindsAndPreservesFields()
    {
        var service = new MicroflowSchemaMigrationService(new MicroflowActionDescriptorNormalizer());

        var result = service.NormalizeForLoad(SchemaJson("webserviceCall", extraField: "keep-me"));

        Assert.True(result.Changed);
        Assert.Contains(result.Changes, change => change.Original == "webserviceCall" && change.Canonical == "webServiceCall");
        Assert.Equal("webServiceCall", ReadActionKind(result.Schema));
        Assert.Equal("keep-me", result.Schema.GetProperty("objectCollection").GetProperty("objects")[0].GetProperty("customField").GetString());
    }

    [Fact]
    public void NormalizeForSave_IsIdempotentAndKeepsCanonicalKinds()
    {
        var service = new MicroflowSchemaMigrationService(new MicroflowActionDescriptorNormalizer());
        using var doc = JsonDocument.Parse(SchemaJson("webServiceCall"));

        var first = service.NormalizeForSave(doc.RootElement);
        var second = service.NormalizeForSave(first.Schema);

        Assert.False(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal("webServiceCall", ReadActionKind(second.Schema));
    }

    [Theory]
    [InlineData("listUnion", "union")]
    [InlineData("listIntersect", "intersect")]
    [InlineData("listSubtract", "subtract")]
    public void NormalizeForPublish_RewritesListOperationAliasesAndAddsOperation(string legacyKind, string operation)
    {
        var service = new MicroflowSchemaMigrationService(new MicroflowActionDescriptorNormalizer());
        using var doc = JsonDocument.Parse(SchemaJson(legacyKind));

        var result = service.NormalizeForPublish(doc.RootElement);
        var action = result.Schema.GetProperty("objectCollection").GetProperty("objects")[0].GetProperty("action");

        Assert.True(result.Changed);
        Assert.Equal("listOperation", action.GetProperty("kind").GetString());
        Assert.Equal(operation, action.GetProperty("operation").GetString());
    }

    private static string ReadActionKind(JsonElement schema)
        => schema.GetProperty("objectCollection").GetProperty("objects")[0].GetProperty("action").GetProperty("kind").GetString()!;

    private static string SchemaJson(string actionKind, string? extraField = null)
        => JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0.0",
            id = "mf-migration",
            name = "MigrationTest",
            objectCollection = new
            {
                objects = new[]
                {
                    new
                    {
                        id = "activity-1",
                        kind = "actionActivity",
                        customField = extraField,
                        action = new
                        {
                            id = "action-1",
                            kind = actionKind
                        }
                    }
                }
            },
            flows = Array.Empty<object>(),
            parameters = Array.Empty<object>(),
            returnType = new { kind = "void" }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
