using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public sealed class SkeletonMicroflowValidationService : IMicroflowValidationService
{
    private readonly IMicroflowClock _clock;

    public SkeletonMicroflowValidationService(IMicroflowClock clock)
    {
        _clock = clock;
    }

    public Task<ValidateMicroflowResponseDto> ValidateAsync(
        string id,
        ValidateMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new ValidateMicroflowResponseDto
        {
            Issues = Array.Empty<Contracts.MicroflowValidationIssueDto>(),
            Summary = new MicroflowValidationSummaryDto(),
            ServerValidatedAt = _clock.UtcNow
        });
    }
}
