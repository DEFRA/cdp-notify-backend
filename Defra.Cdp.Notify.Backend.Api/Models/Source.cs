using System.Text.Json.Serialization;

namespace Defra.Cdp.Notify.Backend.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Source
{
    Grafana, 
    Github
}