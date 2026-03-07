using Atlas.LicenseIssuer.Models;

namespace Atlas.LicenseIssuer.Data;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var db = AppDbContext.Create();
        db.CodeFirst.InitTables(
            typeof(CustomerRecord),
            typeof(IssuanceLogEntry));
    }
}
