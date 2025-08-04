using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Notify.Backend.Api.Models;

public class GrafanaEventAlert : IAlert
{
    [property: JsonPropertyName("service")] public string? Service { get; set; }
    [property: JsonPropertyName("environment")] public required Environment Environment { get; set; }
    [property: JsonPropertyName("alertURL")] public required string AlertUrl { get; set; }
    [property: JsonPropertyName("alertName")] public string? AlertName { get; set; }
    [property: JsonPropertyName("status")] [JsonConverter(typeof(JsonStringEnumConverter))] public required GrafanaAlertStatus Status { get; set; }
    [property: JsonPropertyName("startsAt")] public DateTime StartsAt { get; set; }
    [property: JsonPropertyName("endsAt")] public DateTime? EndsAt { get; set; }
    [property: JsonPropertyName("summary")] public required string Summary { get; set; }
    [JsonConverter(typeof(StringToNullableBoolConverter))][property: JsonPropertyName("pagerDuty")] public bool? PagerDuty { get; set; }

    public NotifyEvent ToNotifyEvent(string awsMessageId)
    {
        return new GrafanaNotifyEvent(awsMessageId, Environment, Service!, Status, AlertName, Summary, AlertUrl, StartsAt, EndsAt, PagerDuty ?? false);
    }
}

public enum GrafanaAlertStatus
{
    firing,
    resolved
}

public class StringToNullableBoolConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => bool.TryParse(reader.GetString(), out var b) ? b : null,
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token parsing boolean: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteBooleanValue(value.Value);
        else
            writer.WriteNullValue();
    }
}