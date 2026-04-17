using System.Text.Json;

namespace Atlas.Application.LowCode.Models;

public sealed record RuntimeChatflowInvokeRequest(
    string ChatflowId,
    string? SessionId,
    string Input,
    Dictionary<string, JsonElement>? Context);

public sealed record RuntimeSessionInfo(
    string Id,
    string? Title,
    bool Pinned,
    bool Archived,
    DateTimeOffset UpdatedAt);

public sealed record RuntimeSessionCreateRequest(string? Title);
public sealed record RuntimeSessionPinRequest(bool Pinned);
public sealed record RuntimeSessionArchiveRequest(bool Archived);
public sealed record RuntimeChatflowInjectRequest(string Message);

public sealed record RuntimeMessageLogQuery(string? SessionId, string? WorkflowId, string? AgentId, DateTimeOffset? From, DateTimeOffset? To, int? PageIndex, int? PageSize);

public sealed record RuntimeMessageLogEntryDto(
    string EntryId,
    string Source,
    string Kind,
    string? SessionId,
    string? WorkflowId,
    string? AgentId,
    string? TraceId,
    JsonElement? Payload,
    DateTimeOffset OccurredAt);
