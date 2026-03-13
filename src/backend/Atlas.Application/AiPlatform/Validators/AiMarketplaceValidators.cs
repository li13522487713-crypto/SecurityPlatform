using Atlas.Application.AiPlatform.Models;
using FluentValidation;

namespace Atlas.Application.AiPlatform.Validators;

public sealed class AiProductCategoryCreateRequestValidator : AbstractValidator<AiProductCategoryCreateRequest>
{
    public AiProductCategoryCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(512).When(x => x.Description is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AiProductCategoryUpdateRequestValidator : AbstractValidator<AiProductCategoryUpdateRequest>
{
    public AiProductCategoryUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(512).When(x => x.Description is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AiMarketplaceProductCreateRequestValidator : AbstractValidator<AiMarketplaceProductCreateRequest>
{
    public AiMarketplaceProductCreateRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Summary).MaximumLength(512).When(x => x.Summary is not null);
        RuleFor(x => x.Description).MaximumLength(4000).When(x => x.Description is not null);
        RuleFor(x => x.Icon).MaximumLength(1024).When(x => x.Icon is not null);
        RuleForEach(x => x.Tags).MaximumLength(32);
    }
}

public sealed class AiMarketplaceProductUpdateRequestValidator : AbstractValidator<AiMarketplaceProductUpdateRequest>
{
    public AiMarketplaceProductUpdateRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Summary).MaximumLength(512).When(x => x.Summary is not null);
        RuleFor(x => x.Description).MaximumLength(4000).When(x => x.Description is not null);
        RuleFor(x => x.Icon).MaximumLength(1024).When(x => x.Icon is not null);
        RuleForEach(x => x.Tags).MaximumLength(32);
    }
}

public sealed class AiMarketplaceProductPublishRequestValidator : AbstractValidator<AiMarketplaceProductPublishRequest>
{
    public AiMarketplaceProductPublishRequestValidator()
    {
        RuleFor(x => x.Version).NotEmpty().MaximumLength(32);
    }
}
