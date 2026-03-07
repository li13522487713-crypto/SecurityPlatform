namespace Atlas.LicenseIssuer.Models;

public sealed class LicenseFeatures
{
    public bool LowCode { get; set; }
    public bool Workflow { get; set; }
    public bool Approval { get; set; }
    public bool Alert { get; set; }
    public bool OfflineDeploy { get; set; }
    public bool MultiTenant { get; set; }
    public bool Audit { get; set; } = true;

    public Dictionary<string, bool> ToDictionary() => new()
    {
        ["lowCode"] = LowCode,
        ["workflow"] = Workflow,
        ["approval"] = Approval,
        ["alert"] = Alert,
        ["offlineDeploy"] = OfflineDeploy,
        ["multiTenant"] = MultiTenant,
        ["audit"] = Audit,
    };

    public static LicenseFeatures ForEdition(string edition)
    {
        return edition switch
        {
            "Enterprise" => new LicenseFeatures
            {
                LowCode = true, Workflow = true, Approval = true,
                Alert = true, OfflineDeploy = true, MultiTenant = true, Audit = true
            },
            "Pro" => new LicenseFeatures
            {
                LowCode = true, Workflow = true, Approval = true,
                Alert = true, OfflineDeploy = true, MultiTenant = true, Audit = true
            },
            _ => new LicenseFeatures // Trial
            {
                LowCode = true, Workflow = false, Approval = false,
                Alert = false, OfflineDeploy = false, MultiTenant = false, Audit = true
            }
        };
    }
}
