namespace Atlas.Application.System.Models;

public sealed record LoginLogDto(
    long Id,
    string Username,
    string IpAddress,
    string? Browser,
    string? OperatingSystem,
    bool LoginStatus,
    string? Message,
    DateTimeOffset LoginTime);

public sealed record LoginLogExportResult(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record LoginLogWriteRequest(
    string Username,
    string IpAddress,
    string? UserAgent,
    bool LoginStatus,
    string? Message,
    DateTimeOffset LoginTime);

public sealed record OnlineUserDto(
    long SessionId,
    long UserId,
    string Username,
    string IpAddress,
    string ClientType,
    DateTimeOffset LoginTime,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt);
