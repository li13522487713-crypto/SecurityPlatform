using Atlas.LicenseIssuer.Data;
using Atlas.LicenseIssuer.Models;

namespace Atlas.LicenseIssuer.Services;

public sealed class IssuanceLogService
{
    public void Append(IssuanceLogEntry entry)
    {
        entry.IssuedAt = DateTimeOffset.UtcNow.ToString("o");
        using var db = AppDbContext.Create();
        db.Insertable(entry).ExecuteCommand();
    }

    public List<IssuanceLogEntry> GetByCustomer(string customerId)
    {
        using var db = AppDbContext.Create();
        return db.Queryable<IssuanceLogEntry>()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.IssuedAt)
            .ToList();
    }

    public List<IssuanceLogEntry> GetAll(int top = 100)
    {
        using var db = AppDbContext.Create();
        return db.Queryable<IssuanceLogEntry>()
            .OrderByDescending(x => x.IssuedAt)
            .Take(top)
            .ToList();
    }
}
