using Atlas.Application.Microflows.Contracts;

namespace Atlas.Application.Microflows.Exceptions;

public sealed class MicroflowApiException : Exception
{
    public MicroflowApiException(
        string code,
        string message,
        int httpStatus,
        bool retryable = false,
        string? details = null,
        IReadOnlyList<MicroflowApiFieldError>? fieldErrors = null,
        IReadOnlyList<MicroflowValidationIssueDto>? validationIssues = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        HttpStatus = httpStatus;
        Retryable = retryable;
        Details = details;
        FieldErrors = fieldErrors ?? Array.Empty<MicroflowApiFieldError>();
        ValidationIssues = validationIssues ?? Array.Empty<MicroflowValidationIssueDto>();
    }

    public string Code { get; }

    public int HttpStatus { get; }

    public bool Retryable { get; }

    public string? Details { get; }

    public IReadOnlyList<MicroflowApiFieldError> FieldErrors { get; }

    public IReadOnlyList<MicroflowValidationIssueDto> ValidationIssues { get; }
}
