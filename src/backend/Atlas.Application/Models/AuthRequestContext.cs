using Atlas.Core.Identity;

namespace Atlas.Application.Models;

public sealed record AuthRequestContext(string? IpAddress, string? UserAgent, ClientContext ClientContext);
