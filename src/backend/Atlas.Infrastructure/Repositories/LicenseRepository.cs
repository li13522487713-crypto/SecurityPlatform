using Atlas.Application.License.Abstractions;
using Atlas.Domain.License;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LicenseRepository : ILicenseRepository
{
    private readonly ISqlSugarClient _db;

    public LicenseRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<LicenseRecord?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LicenseRecord>()
            .Where(x => x.Status == LicenseStatus.Active)
            .OrderByDescending(x => x.ActivatedAt)
            .FirstAsync(cancellationToken);
    }

    public async Task<LicenseRecord?> GetByLicenseIdAsync(Guid licenseId, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LicenseRecord>()
            .Where(x => x.LicenseId == licenseId)
            .OrderByDescending(x => x.Revision)
            .FirstAsync(cancellationToken);
    }

    public async Task AddAsync(LicenseRecord record, CancellationToken cancellationToken = default)
    {
        await _db.Insertable(record).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(LicenseRecord record, CancellationToken cancellationToken = default)
    {
        await _db.Updateable(record).ExecuteCommandAsync(cancellationToken);
    }

    public async Task SaveActivatedAsync(
        LicenseRecord activatedRecord,
        LicenseRecord? previousActiveRecord,
        CancellationToken cancellationToken = default)
    {
        var result = await _db.Ado.UseTranAsync(async () =>
        {
            if (previousActiveRecord is not null)
            {
                await _db.Updateable(previousActiveRecord).ExecuteCommandAsync(cancellationToken);
            }

            await _db.Insertable(activatedRecord).ExecuteCommandAsync(cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("保存激活证书失败。");
        }
    }
}
