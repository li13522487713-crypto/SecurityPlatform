using System.Text.Json;

namespace Atlas.Application.LowCode.Models;

/// <summary>
/// dispatch 协议 DTO（M13 S13-1）。与 docx §10.4.2/10.4.3 完全对齐，前端 lowcode-runtime-web 直接消费。
/// </summary>
public sealed record DispatchActionDto(
    string Kind,
    string? Id,
    string? When,
    bool? Parallel,
    /// <summary>动作 payload（按 kind 不同含不同字段，由前端 ActionSchema zod 校验保证）。</summary>
    JsonElement? Payload,
    JsonElement? Resilience,
    DispatchActionDto[]? OnError);

public sealed record DispatchRequest(
    string AppId,
    string? PageId,
    string? ComponentId,
    string? EventName,
    string? VersionId,
    Dictionary<string, JsonElement>? Inputs,
    JsonElement? StateSnapshot,
    DispatchActionDto[] Actions);

public sealed record DispatchStatePatchDto(
    string Scope,
    string Path,
    string Op,
    JsonElement? Value,
    string? ComponentId);

public sealed record DispatchMessageDto(string Kind, string Text);

public sealed record DispatchErrorDto(string Kind, string Message, string? Stack);

public sealed record DispatchResponse(
    string TraceId,
    Dictionary<string, JsonElement>? Outputs,
    IReadOnlyList<DispatchStatePatchDto>? StatePatches,
    IReadOnlyList<DispatchMessageDto>? Messages,
    IReadOnlyList<DispatchErrorDto>? Errors);

public sealed record RuntimeTraceQuery(
    string? TraceId,
    string? AppId,
    string? PageId,
    string? ComponentId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ErrorType,
    string? UserId,
    int? PageIndex,
    int? PageSize);

public sealed record RuntimeTraceDto(
    string TraceId,
    string AppId,
    string? PageId,
    string? ComponentId,
    string? EventName,
    string Status,
    string? ErrorKind,
    string UserId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<RuntimeSpanDto> Spans);

public sealed record RuntimeSpanDto(
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Status,
    JsonElement? Attributes,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);
