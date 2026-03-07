using Atlas.Domain.License;

namespace Atlas.Application.License.Abstractions;

public interface ILicenseRepository
{
    Task<LicenseRecord?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<LicenseRecord?> GetByLicenseIdAsync(Guid licenseId, CancellationToken cancellationToken = default);
    Task AddAsync(LicenseRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(LicenseRecord record, CancellationToken cancellationToken = default);
    Task SaveActivatedAsync(
        LicenseRecord activatedRecord,
        LicenseRecord? previousActiveRecord,
        CancellationToken cancellationToken = default);
}
