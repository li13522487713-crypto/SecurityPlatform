using Atlas.Core.Attributes;
using Atlas.Core.Masking;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Presentation.Shared.Json;

/// <summary>
/// 针对所有对象类型的 JsonConverter 工厂，识别并脱敏标注了 <see cref="SensitiveAttribute"/> 的字符串属性。
/// 注意：此转换器仅在反射模式下工作，不支持 AOT/Source Generation。
/// </summary>
public sealed class SensitiveObjectConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert.IsPrimitive || typeToConvert == typeof(string)) return false;
        if (typeToConvert.IsEnum) return false;
        return typeToConvert.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.GetCustomAttribute<SensitiveAttribute>() is not null);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(SensitiveObjectConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

/// <summary>
/// 泛型对象转换器，序列化时对 <see cref="SensitiveAttribute"/> 标注的 string 属性进行脱敏。
/// </summary>
internal sealed class SensitiveObjectConverter<T> : JsonConverter<T>
{
    private static readonly (PropertyInfo Property, SensitiveAttribute Attribute)[] SensitiveProperties =
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p, p.GetCustomAttribute<SensitiveAttribute>()!))
            .Where(x => x.Item2 is not null && x.p.PropertyType == typeof(string))
            .ToArray();

    private static readonly JsonSerializerOptions _innerOptions = BuildInnerOptions();

    private static JsonSerializerOptions BuildInnerOptions()
    {
        // 创建不带当前工厂的 options，防止无限递归
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return opts;
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<T>(ref reader, _innerOptions)!;

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }

        writer.WriteStartObject();

        var allProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in allProperties)
        {
            if (!prop.CanRead) continue;

            var jsonPropName = options.PropertyNamingPolicy?.ConvertName(prop.Name) ?? prop.Name;

            var sensitiveAttr = SensitiveProperties
                .FirstOrDefault(sp => sp.Property == prop).Attribute;

            var propValue = prop.GetValue(value);

            if (sensitiveAttr is not null && propValue is string strValue)
            {
                writer.WritePropertyName(jsonPropName);
                var masked = SensitiveMasker.Mask(strValue, sensitiveAttr.MaskType, sensitiveAttr.PrefixLength, sensitiveAttr.SuffixLength);
                writer.WriteStringValue(masked);
            }
            else
            {
                writer.WritePropertyName(jsonPropName);
                JsonSerializer.Serialize(writer, propValue, prop.PropertyType, options);
            }
        }

        writer.WriteEndObject();
    }
}
