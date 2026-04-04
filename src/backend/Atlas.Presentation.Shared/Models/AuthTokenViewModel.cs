namespace Atlas.Presentation.Shared.Models;

public sealed record AuthTokenViewModel(
    string Username,
    string Password,
    string? TotpCode = null,
    string? CaptchaKey = null,
    string? CaptchaCode = null,
    bool RememberMe = false);