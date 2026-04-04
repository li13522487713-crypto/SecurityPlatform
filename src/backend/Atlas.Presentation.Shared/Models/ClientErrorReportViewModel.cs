namespace Atlas.Presentation.Shared.Models;

public sealed record ClientErrorReportViewModel(
    string Message,
    string? Stack,
    string? Url,
    string? Component,
    string? Level);
