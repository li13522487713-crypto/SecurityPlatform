namespace Atlas.Presentation.Shared.Models;

public sealed record RegisterViewModel(
    string Username,
    string Password,
    string ConfirmPassword,
    string? CaptchaKey,
    string? CaptchaCode);
