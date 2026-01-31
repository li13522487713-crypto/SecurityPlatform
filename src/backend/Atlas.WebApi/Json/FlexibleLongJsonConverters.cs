using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.WebApi.Json;

public sealed class FlexibleLongJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => ParseLong(reader.GetString()),
            _ => throw new JsonException("Invalid token for long value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }

    private static long ParseLong(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("Invalid long value.");
        }

        if (long.TryParse(raw, out var result))
        {
            return result;
        }

        throw new JsonException("Invalid long value.");
    }
}

public sealed class FlexibleNullableLongJsonConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => ParseNullableLong(reader.GetString()),
            _ => throw new JsonException("Invalid token for long value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    private static long? ParseNullableLong(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (long.TryParse(raw, out var result))
        {
            return result;
        }

        throw new JsonException("Invalid long value.");
    }
}
