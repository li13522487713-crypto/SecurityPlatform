using Atlas.Application.ExternalConnectors.Models;
using FluentValidation;

namespace Atlas.Application.ExternalConnectors.Validators;

public sealed class ManualBindingRequestValidator : AbstractValidator<ManualBindingRequest>
{
    public ManualBindingRequestValidator()
    {
        RuleFor(x => x.ProviderId).GreaterThan(0);
        RuleFor(x => x.LocalUserId).GreaterThan(0);
        RuleFor(x => x.ExternalUserId).NotEmpty().MaximumLength(128);
    }
}

public sealed class BindingConflictResolutionRequestValidator : AbstractValidator<BindingConflictResolutionRequest>
{
    public BindingConflictResolutionRequestValidator()
    {
        RuleFor(x => x.BindingId).GreaterThan(0);
        When(x => x.Resolution == BindingConflictResolution.SwitchToLocalUser, () =>
        {
            RuleFor(x => x.NewLocalUserId).NotNull().GreaterThan(0);
        });
    }
}

public sealed class OAuthInitiationRequestValidator : AbstractValidator<OAuthInitiationRequest>
{
    public OAuthInitiationRequestValidator()
    {
        RuleFor(x => x.ProviderId).GreaterThan(0);
        RuleFor(x => x.PostLoginRedirect).MaximumLength(2048);
    }
}

public sealed class OAuthCallbackRequestValidator : AbstractValidator<OAuthCallbackRequest>
{
    public OAuthCallbackRequestValidator()
    {
        RuleFor(x => x.State).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(512);
    }
}
