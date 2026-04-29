using System.Text.Json;
using Atlas.Application.Microflows.Runtime.Variables;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowRuntimeVariableTypesTests
{
    [Fact]
    public void PrimitiveFactoryInfersPrimitiveTypeAndPreview()
    {
        var value = RuntimeVariableValue.Primitive("count", 3);

        var type = Assert.IsType<PrimitiveTypeDescriptor>(value.DataType);
        Assert.Equal("primitive", value.Kind);
        Assert.Equal("integer", type.PrimitiveKind);
        Assert.Equal("3", value.RawValueJson);
        Assert.Equal("3", value.Preview);
    }

    [Fact]
    public void ObjectListAndExternalRefsExposeStableKinds()
    {
        RuntimeVariableValue[] values =
        [
            new RuntimeObjectRef { Name = "order", ObjectId = "order-1", EntityQualifiedName = "Sales.Order", DataType = new EntityTypeDescriptor { QualifiedName = "Sales.Order" } },
            new RuntimeListValue { Name = "orders", DataType = new ListTypeDescriptor { ItemType = new EntityTypeDescriptor { QualifiedName = "Sales.Order" } }, Items = [RuntimeVariableValue.Primitive("id", "order-1")] },
            new RuntimeExternalObjectRef { Name = "external", ConnectorId = "crm", ExternalId = "ext-1" },
            new RuntimeFileRef { Name = "file", FileId = "file-1", FileName = "report.pdf" },
            new RuntimeCommandValue { Name = "command", CommandName = "showPage" },
        ];

        Assert.Equal(["objectRef", "list", "externalObjectRef", "fileRef", "runtimeCommand"], values.Select(value => value.Kind));
        var json = JsonSerializer.Serialize(values, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Sales.Order", Assert.IsType<RuntimeObjectRef>(values[0]).EntityQualifiedName);
        Assert.Equal("Sales.Order", Assert.IsType<EntityTypeDescriptor>(values[0].DataType).QualifiedName);
        Assert.Equal("showPage", Assert.IsType<RuntimeCommandValue>(values[4]).CommandName);
    }

    [Fact]
    public void VariableScopeFrameCarriesTypedVariables()
    {
        var frame = new VariableScopeFrame
        {
            Kind = "loop",
            OwnerObjectId = "loop-1",
            Variables = new Dictionary<string, RuntimeVariableValue>
            {
                ["$iterator"] = new RuntimeObjectRef
                {
                    Name = "$iterator",
                    ObjectId = "order-1",
                    EntityQualifiedName = "Sales.Order",
                    DataType = new EntityTypeDescriptor { QualifiedName = "Sales.Order" }
                }
            }
        };

        Assert.Equal("loop", frame.Kind);
        Assert.True(frame.Variables.ContainsKey("$iterator"));
        Assert.IsType<RuntimeObjectRef>(frame.Variables["$iterator"]);
    }
}
