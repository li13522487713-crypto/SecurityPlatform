namespace Atlas.Application.Models;

public sealed record AuthTokenRequest(string Username, string Password, string? TotpCode = null);