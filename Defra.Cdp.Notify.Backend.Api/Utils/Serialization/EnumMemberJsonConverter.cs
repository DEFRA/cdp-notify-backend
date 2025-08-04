namespace Defra.Cdp.Notify.Backend.Api.Utils;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.Serialization;
using System.Reflection;

public class EnumMemberJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private readonly Dictionary<string, T> _fromValue = new();
    private readonly Dictionary<T, string> _toValue = new();

    public EnumMemberJsonConverter()
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var name = field.Name;
            var enumValue = (T)field.GetValue(null)!;
            var enumMemberAttr = field.GetCustomAttribute<EnumMemberAttribute>();
            var value = enumMemberAttr?.Value ?? name;

            _fromValue[value] = enumValue;
            _toValue[enumValue] = value;
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var key = reader.GetString();
        if (key != null && _fromValue.TryGetValue(key, out var value))
            return value;

        throw new JsonException($"Unknown value '{key}' for enum {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (_toValue.TryGetValue(value, out var stringValue))
            writer.WriteStringValue(stringValue);
        else
            writer.WriteStringValue(value.ToString());
    }
}