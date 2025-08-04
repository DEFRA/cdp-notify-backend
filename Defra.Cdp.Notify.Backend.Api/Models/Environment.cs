using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Notify.Backend.Api.Models;


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Environment
{
    [EnumMember(Value = "infra-dev")]
    Infradev, 
    [EnumMember(Value = "management")]
    Management,
    [EnumMember(Value = "dev")]
    Dev,
    [EnumMember(Value = "test")]
    Test,
    [EnumMember(Value = "perf-test")]
    Perftest,
    [EnumMember(Value = "ext-test")]
    Exttest,
    [EnumMember(Value = "prod")]
    Prod
}