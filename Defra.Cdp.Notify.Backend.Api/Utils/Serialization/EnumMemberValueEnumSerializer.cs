using System.Reflection;
using System.Runtime.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Defra.Cdp.Notify.Backend.Api.Utils.Serialization;

public class EnumMemberValueEnumSerializer<TEnum> : SerializerBase<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> s_stringToEnum = typeof(TEnum)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToDictionary(
            f => f.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? f.Name,
            f => (TEnum)f.GetValue(null)!,
            StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<TEnum, string> s_enumToString = s_stringToEnum
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var value = context.Reader.ReadString();
        if (s_stringToEnum.TryGetValue(value, out var result))
        {
            return result;
        }
        throw new BsonSerializationException($"Cannot deserialize '{value}' to {typeof(TEnum).Name}");
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
    {
        if (s_enumToString.TryGetValue(value, out var stringValue))
        {
            context.Writer.WriteString(stringValue);
        }
        else
        {
            throw new BsonSerializationException($"Cannot serialize unknown value '{value}' of {typeof(TEnum).Name}");
        }
    }
}