using SqlSugar;

namespace Atlas.Infrastructure.DataScopes.Platform;

public interface IPlatformSqlSugarScopeFactory
{
    ISqlSugarClient Create();
}
