using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Runtime.Variables;

public abstract record RuntimeVariableValue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [JsonPropertyName("kind")]
    public abstract string Kind { get; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("dataType")]
    public RuntimeTypeDescriptor? DataType { get; init; }

    [JsonPropertyName("rawValue")]
    public JsonElement? RawValue { get; init; }

    [JsonPropertyName("preview")]
    public string Preview { get; init; } = string.Empty;

    public string RawValueJson => RawValue?.GetRawText() ?? "null";

    public static RuntimePrimitiveValue Primitive(string? name, object? value, RuntimeTypeDescriptor? type = null)
        => new()
        {
            Name = name,
            DataType = type ?? PrimitiveTypeDescriptor.FromValue(value),
            RawValue = JsonSerializer.SerializeToElement(value, JsonOptions),
            Preview = value?.ToString() ?? "null"
        };
}

public sealed record RuntimePrimitiveValue : RuntimeVariableValue
{
    public override string Kind => "primitive";
}

public sealed record RuntimeObjectRef : RuntimeVariableValue
{
    public override string Kind => "objectRef";

    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("entityQualifiedName")]
    public string EntityQualifiedName { get; init; } = string.Empty;
}

public sealed record RuntimeListValue : RuntimeVariableValue
{
    public override string Kind => "list";

    [JsonPropertyName("items")]
    public IReadOnlyList<RuntimeVariableValue> Items { get; init; } = Array.Empty<RuntimeVariableValue>();
}

public sealed record RuntimeExternalObjectRef : RuntimeVariableValue
{
    public override string Kind => "externalObjectRef";

    [JsonPropertyName("connectorId")]
    public string ConnectorId { get; init; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string ExternalId { get; init; } = string.Empty;
}

public sealed record RuntimeFileRef : RuntimeVariableValue
{
    public override string Kind => "fileRef";

    [JsonPropertyName("fileId")]
    public string FileId { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
}

public sealed record RuntimeCommandValue : RuntimeVariableValue
{
    public override string Kind => "runtimeCommand";

    [JsonPropertyName("commandName")]
    public string CommandName { get; init; } = string.Empty;
}

public sealed record VariableScopeFrame
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "action";

    [JsonPropertyName("ownerObjectId")]
    public string? OwnerObjectId { get; init; }

    [JsonPropertyName("variables")]
    public IReadOnlyDictionary<string, RuntimeVariableValue> Variables { get; init; } =
        new Dictionary<string, RuntimeVariableValue>(StringComparer.Ordinal);
}

public abstract record RuntimeTypeDescriptor
{
    [JsonPropertyName("kind")]
    public abstract string Kind { get; }
}

public sealed record PrimitiveTypeDescriptor : RuntimeTypeDescriptor
{
    public override string Kind => "primitive";

    [JsonPropertyName("primitiveKind")]
    public string PrimitiveKind { get; init; } = "unknown";

    public static PrimitiveTypeDescriptor FromValue(object? value)
        => new()
        {
            PrimitiveKind = value switch
            {
                null => "null",
                bool => "boolean",
                int or long or short or byte => "integer",
                decimal or double or float => "decimal",
                DateTime or DateTimeOffset => "dateTime",
                _ => "string"
            }
        };
}

public sealed record EntityTypeDescriptor : RuntimeTypeDescriptor
{
    public override string Kind => "entity";

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("generalization")]
    public string? Generalization { get; init; }

    [JsonPropertyName("specializations")]
    public IReadOnlyList<string> Specializations { get; init; } = Array.Empty<string>();
}

public sealed record ListTypeDescriptor : RuntimeTypeDescriptor
{
    public override string Kind => "list";

    [JsonPropertyName("itemType")]
    public RuntimeTypeDescriptor ItemType { get; init; } = new PrimitiveTypeDescriptor();
}
