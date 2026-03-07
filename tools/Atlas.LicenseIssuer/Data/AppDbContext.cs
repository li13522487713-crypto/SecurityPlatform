using SqlSugar;

namespace Atlas.LicenseIssuer.Data;

public static class AppDbContext
{
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Atlas", "LicenseIssuer", "issuer.db");

    public static ISqlSugarClient Create()
    {
        var dir = Path.GetDirectoryName(DbPath)!;
        Directory.CreateDirectory(dir);

        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={DbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }
}
