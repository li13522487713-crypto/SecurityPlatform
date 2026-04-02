namespace Atlas.Application.Resilience;

public sealed record CompensationStepResult(string NodeKey, bool Succeeded, string? ErrorMessage);

public sealed record CompensationResult(bool AllSucceeded, IReadOnlyList<CompensationStepResult> Steps);

public interface ICompensationService
{
    Task RegisterCompensationAsync(
        long executionId,
        string nodeKey,
        string compensationDataJson,
        CancellationToken cancellationToken);

    Task<CompensationResult> ExecuteCompensationsAsync(long executionId, CancellationToken cancellationToken);
}
