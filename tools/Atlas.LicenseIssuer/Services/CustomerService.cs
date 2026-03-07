using Atlas.LicenseIssuer.Data;
using Atlas.LicenseIssuer.Models;

namespace Atlas.LicenseIssuer.Services;

public sealed class CustomerService
{
    public List<CustomerRecord> GetAll()
    {
        using var db = AppDbContext.Create();
        return db.Queryable<CustomerRecord>().OrderBy(x => x.Name).ToList();
    }

    public CustomerRecord? GetById(string id)
    {
        using var db = AppDbContext.Create();
        return db.Queryable<CustomerRecord>().Where(x => x.Id == id).First();
    }

    public void Add(CustomerRecord record)
    {
        record.Id = Guid.NewGuid().ToString();
        record.CreatedAt = DateTimeOffset.UtcNow.ToString("o");
        using var db = AppDbContext.Create();
        db.Insertable(record).ExecuteCommand();
    }

    public void Update(CustomerRecord record)
    {
        using var db = AppDbContext.Create();
        db.Updateable(record).ExecuteCommand();
    }

    public void Delete(string id)
    {
        using var db = AppDbContext.Create();
        db.Deleteable<CustomerRecord>().Where(x => x.Id == id).ExecuteCommand();
    }
}
