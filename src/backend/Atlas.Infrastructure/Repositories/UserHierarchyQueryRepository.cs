using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserHierarchyQueryRepository : IUserHierarchyQueryRepository
{
    private sealed record LeaderRow(long LeaderUserId, int Depth);

    private const int MaxRecursionCap = 100;

    private readonly ISqlSugarClient _db;

    public UserHierarchyQueryRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<long>> GetLeaderChainAsync(
        TenantId tenantId,
        long userId,
        int maxLevels,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxLevels <= 0)
        {
            return Array.Empty<long>();
        }

        var depth = Math.Min(maxLevels, MaxRecursionCap);
        var sql = BuildLeaderChainSql(_db.CurrentConnectionConfig.DbType);
        var rows = await _db.Ado.SqlQueryAsync<LeaderRow>(
            sql,
            new { TenantId = tenantId.Value, UserId = userId, MaxLevels = depth });

        return rows
            .OrderBy(x => x.Depth)
            .Select(x => x.LeaderUserId)
            .Distinct()
            .ToList();
    }

    public async Task<long?> GetLeaderAtLevelAsync(
        TenantId tenantId,
        long userId,
        int level,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (level < 1)
        {
            return null;
        }

        var depth = Math.Min(level, MaxRecursionCap);
        var sql = BuildLeaderChainSql(_db.CurrentConnectionConfig.DbType);
        var rows = await _db.Ado.SqlQueryAsync<LeaderRow>(
            sql,
            new { TenantId = tenantId.Value, UserId = userId, MaxLevels = depth });

        return rows.FirstOrDefault(x => x.Depth == level)?.LeaderUserId;
    }

    private static string BuildLeaderChainSql(DbType dbType)
    {
        var withKeyword = dbType switch
        {
            DbType.SqlServer => "WITH",
            DbType.MySql => "WITH RECURSIVE",
            DbType.Sqlite => "WITH RECURSIVE",
            _ => "WITH"
        };

        var pathAnchor = dbType == DbType.Sqlite
            ? "(',' || @UserId || ',' || adl.LeaderUserId || ',')"
            : "CONCAT(',', @UserId, ',', adl.LeaderUserId, ',')";

        var pathNext = dbType == DbType.Sqlite
            ? "(cte.Path || adl.LeaderUserId || ',')"
            : "CONCAT(cte.Path, adl.LeaderUserId, ',')";

        var pathCheck = dbType == DbType.Sqlite
            ? "cte.Path NOT LIKE ('%,' || adl.LeaderUserId || ',%')"
            : "cte.Path NOT LIKE CONCAT('%,', adl.LeaderUserId, ',%')";

        return $@"
{withKeyword} leader_cte AS (
    SELECT
        1 AS Depth,
        ud.UserId AS UserId,
        adl.LeaderUserId AS LeaderUserId,
        ud.DepartmentId AS DepartmentId,
        {pathAnchor} AS Path
    FROM UserDepartment ud
    INNER JOIN ApprovalDepartmentLeader adl
        ON adl.TenantIdValue = @TenantId
        AND adl.DepartmentId = ud.DepartmentId
    WHERE ud.TenantIdValue = @TenantId
      AND ud.UserId = @UserId
      AND ud.DepartmentId = (
          SELECT COALESCE(
              MAX(CASE WHEN IsPrimary = 1 THEN DepartmentId END),
              MIN(DepartmentId)
          )
          FROM UserDepartment
          WHERE TenantIdValue = @TenantId AND UserId = @UserId
      )
    UNION ALL
    SELECT
        cte.Depth + 1,
        ud.UserId AS UserId,
        adl.LeaderUserId AS LeaderUserId,
        ud.DepartmentId AS DepartmentId,
        {pathNext} AS Path
    FROM leader_cte cte
    INNER JOIN UserDepartment ud
        ON ud.TenantIdValue = @TenantId
        AND ud.UserId = cte.LeaderUserId
        AND ud.DepartmentId = (
            SELECT COALESCE(
                MAX(CASE WHEN IsPrimary = 1 THEN DepartmentId END),
                MIN(DepartmentId)
            )
            FROM UserDepartment
            WHERE TenantIdValue = @TenantId AND UserId = cte.LeaderUserId
        )
    INNER JOIN ApprovalDepartmentLeader adl
        ON adl.TenantIdValue = @TenantId
        AND adl.DepartmentId = ud.DepartmentId
    WHERE cte.Depth < @MaxLevels
      AND {pathCheck}
)
SELECT LeaderUserId, Depth
FROM leader_cte
ORDER BY Depth;
";
    }
}
