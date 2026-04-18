using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Identity.Models;

public sealed record TenantNetworkPolicyDto(
    string Id,
    string Mode,
    IReadOnlyList<string> Allowlist,
    IReadOnlyList<string> Denylist,
    DateTimeOffset UpdatedAt);

public sealed record TenantNetworkPolicyUpdateRequest(
    [Required] string Mode,
    IReadOnlyList<string>? Allowlist,
    IReadOnlyList<string>? Denylist);

public sealed record TenantDataResidencyPolicyDto(
    string Id,
    IReadOnlyList<string> AllowedRegions,
    string? Notes,
    DateTimeOffset UpdatedAt);

public sealed record TenantDataResidencyPolicyUpdateRequest(
    [Required, MinLength(1)] IReadOnlyList<string> AllowedRegions,
    [StringLength(512)] string? Notes);
