using Atlas.Application.ExternalConnectors.Models;
using FluentValidation;

namespace Atlas.Application.ExternalConnectors.Validators;

public sealed class ExternalIdentityProviderCreateRequestValidator : AbstractValidator<ExternalIdentityProviderCreateRequest>
{
    public ExternalIdentityProviderCreateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ProviderTenantId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AppId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SecretJson).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.CallbackBaseUrl).NotEmpty().MaximumLength(512);
        RuleFor(x => x.TrustedDomains).MaximumLength(1024);
        RuleFor(x => x.AgentId).MaximumLength(64);
        RuleFor(x => x.VisibilityScope).MaximumLength(2048);
        RuleFor(x => x.SyncCron).MaximumLength(64);
    }
}

public sealed class ExternalIdentityProviderUpdateRequestValidator : AbstractValidator<ExternalIdentityProviderUpdateRequest>
{
    public ExternalIdentityProviderUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ProviderTenantId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AppId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CallbackBaseUrl).NotEmpty().MaximumLength(512);
        RuleFor(x => x.TrustedDomains).MaximumLength(1024);
    }
}

public sealed class ExternalIdentityProviderRotateSecretRequestValidator : AbstractValidator<ExternalIdentityProviderRotateSecretRequest>
{
    public ExternalIdentityProviderRotateSecretRequestValidator()
    {
        RuleFor(x => x.SecretJson).NotEmpty().MaximumLength(4096);
    }
}
