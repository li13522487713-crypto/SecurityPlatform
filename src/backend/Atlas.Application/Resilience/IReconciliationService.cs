namespace Atlas.Application.Resilience;

public sealed record ReconciliationMismatch(string EntityType, string EntityId, string Expected, string Actual);

public sealed record ReconciliationReport(
    string Scope,
    int TotalChecked,
    int Mismatches,
    IReadOnlyList<ReconciliationMismatch> Details);

public interface IReconciliationService
{
    Task<ReconciliationReport> ReconcileAsync(string scope, CancellationToken cancellationToken);
}
