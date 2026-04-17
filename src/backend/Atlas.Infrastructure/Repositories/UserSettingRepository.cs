using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserSettingRepository : RepositoryBase<UserSetting>
{
    public UserSettingRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<UserSetting?> FindAsync(
        TenantId tenantId,
        long userId,
        string settingKey,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<UserSetting>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.SettingKey == settingKey)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 按用户批量删除所有 setting。用于"删除账号"语义（M6.3 暂时只清空个人偏好，不真删账号）。
    /// </summary>
    public Task DeleteByUserAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        return Db.Deleteable<UserSetting>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
