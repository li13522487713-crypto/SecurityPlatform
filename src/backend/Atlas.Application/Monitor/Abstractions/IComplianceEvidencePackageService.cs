using Atlas.Application.Monitor.Models;

namespace Atlas.Application.Monitor.Abstractions;

public interface IComplianceEvidencePackageService
{
    Task<ComplianceEvidencePackageResult> BuildPackageAsync(CancellationToken cancellationToken = default);
}
