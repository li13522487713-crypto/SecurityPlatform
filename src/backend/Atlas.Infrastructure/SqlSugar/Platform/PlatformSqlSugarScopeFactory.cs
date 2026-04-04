using SqlSugar;

namespace Atlas.Infrastructure.DataScopes.Platform;

public sealed class PlatformSqlSugarScopeFactory : IPlatformSqlSugarScopeFactory
{
    private readonly ISqlSugarClient db;

    public PlatformSqlSugarScopeFactory(ISqlSugarClient db)
    {
        this.db = db;
    }

    public ISqlSugarClient Create()
    {
        return db;
    }
}
