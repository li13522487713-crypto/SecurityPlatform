namespace Atlas.Application.Options;

public sealed class BootstrapAdminOptions
{
    public bool Enabled { get; init; }
    public string TenantId { get; init; } = "00000000-0000-0000-0000-000000000001";
    public string Username { get; init; } = "admin";
    public string Password { get; init; } = string.Empty;
    public string Roles { get; init; } = "Admin";
    public bool IsPlatformAdmin { get; init; } = true;
}
