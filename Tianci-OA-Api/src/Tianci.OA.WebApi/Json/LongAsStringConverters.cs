using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tianci.OA.WebApi.Json;

public sealed class LongAsStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out var value) ? value :
        reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out value) ? value :
        throw new JsonException("64 位 ID 格式无效");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public sealed class NullableLongAsStringConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out var value))
        {
            return value;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out value))
        {
            return value;
        }

        throw new JsonException("64 位 ID 格式无效");
    }
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString());
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
