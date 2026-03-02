namespace Atlas.Application.Models;

public sealed record AuthTokenRequest(
    string Username,
    string Password,
    string? TotpCode = null,
    string? CaptchaKey = null,
    string? CaptchaCode = null,
    bool RememberMe = false);