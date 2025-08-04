using System.Text.Json.Serialization;
using Defra.Cdp.Notify.Backend.Api.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Notify.Backend.Api.Models;

public record AlertRule(
    Source Source,
    Environment? Environment,
    string? Service,
    bool? PagerDuty,
    List<AlertMethod> Methods)
{
    
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; }
}
