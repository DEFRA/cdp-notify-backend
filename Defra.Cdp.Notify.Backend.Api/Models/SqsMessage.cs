namespace Defra.Cdp.Notify.Backend.Api.Models;

public record SqsMessage(Source Source, string MessageId, DateTime DateTime, string MessageBody);
