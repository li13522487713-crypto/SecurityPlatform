using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowValidationService
{
    Task<ValidateMicroflowResponseDto> ValidateAsync(
        string id,
        ValidateMicroflowRequestDto request,
        CancellationToken cancellationToken);
}
