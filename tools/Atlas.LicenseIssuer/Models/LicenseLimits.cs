namespace Atlas.LicenseIssuer.Models;

public sealed class LicenseLimits
{
    public int MaxApps { get; set; } = -1;
    public int MaxUsers { get; set; } = -1;
    public int MaxTenants { get; set; } = -1;
    public int MaxDataSources { get; set; } = -1;
    public int AuditRetentionDays { get; set; } = 180;

    public Dictionary<string, int> ToDictionary() => new()
    {
        ["maxApps"] = MaxApps,
        ["maxUsers"] = MaxUsers,
        ["maxTenants"] = MaxTenants,
        ["maxDataSources"] = MaxDataSources,
        ["auditRetentionDays"] = AuditRetentionDays,
    };

    public static LicenseLimits ForEdition(string edition)
    {
        return edition switch
        {
            "Enterprise" => new LicenseLimits
            {
                MaxApps = -1, MaxUsers = -1, MaxTenants = -1,
                MaxDataSources = -1, AuditRetentionDays = 365
            },
            "Pro" => new LicenseLimits
            {
                MaxApps = 20, MaxUsers = 500, MaxTenants = 5,
                MaxDataSources = 10, AuditRetentionDays = 180
            },
            _ => new LicenseLimits // Trial
            {
                MaxApps = 3, MaxUsers = 10, MaxTenants = 1,
                MaxDataSources = 2, AuditRetentionDays = 7
            }
        };
    }
}
