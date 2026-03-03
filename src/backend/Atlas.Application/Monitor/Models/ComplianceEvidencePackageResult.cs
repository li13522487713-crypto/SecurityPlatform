namespace Atlas.Application.Monitor.Models;

public sealed record ComplianceEvidencePackageResult(
    string FileName,
    string ContentType,
    byte[] Content,
    IReadOnlyList<string> IncludedFiles,
    IReadOnlyList<string> MissingFiles);
