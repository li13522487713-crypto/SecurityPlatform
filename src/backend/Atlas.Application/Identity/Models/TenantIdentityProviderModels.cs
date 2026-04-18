using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Identity.Models;

public sealed record TenantIdentityProviderDto(
    string Id,
    string Code,
    string DisplayName,
    string IdpType,
    bool Enabled,
    string ConfigJson,
    bool HasSecret,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TenantIdentityProviderCreateRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Code,
    [Required, StringLength(128, MinimumLength = 1)] string DisplayName,
    [Required, StringLength(16)] string IdpType,
    bool Enabled,
    [Required] string ConfigJson,
    string? SecretJson);

public sealed record TenantIdentityProviderUpdateRequest(
    [Required, StringLength(128, MinimumLength = 1)] string DisplayName,
    bool Enabled,
    [Required] string ConfigJson,
    string? SecretJson);
