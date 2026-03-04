namespace Atlas.WebApi.Models;

public sealed record UserProfileDetailViewModel(
    string DisplayName,
    string? Email,
    string? PhoneNumber);

public sealed record UserProfileUpdateViewModel(
    string DisplayName,
    string? Email,
    string? PhoneNumber);
