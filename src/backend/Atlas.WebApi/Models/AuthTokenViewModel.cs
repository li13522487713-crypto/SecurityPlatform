namespace Atlas.WebApi.Models;

public sealed record AuthTokenViewModel(string Username, string Password, string? TotpCode = null);