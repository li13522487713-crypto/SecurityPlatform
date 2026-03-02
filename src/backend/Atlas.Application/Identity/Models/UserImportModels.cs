namespace Atlas.Application.Identity.Models;

public sealed record UserImportRow(
    int RowNumber,
    string? Username,
    string? DisplayName,
    string? Email,
    string? Phone);

public sealed record UserImportError(
    int Row,
    string Field,
    string Message);

public sealed record UserImportResult(
    int TotalRows,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<UserImportError> Errors);
