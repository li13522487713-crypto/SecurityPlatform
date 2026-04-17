using FluentValidation;
using Atlas.Application.LowCode.Models;

namespace Atlas.Application.LowCode.Validators;

public sealed class PageDefinitionCreateRequestValidator : AbstractValidator<PageDefinitionCreateRequest>
{
    public PageDefinitionCreateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(LowCodeCodeRule.Pattern);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(256)
            .Must(p => p.StartsWith('/'))
            .WithMessage("页面 path 必须以 / 开头");
        RuleFor(x => x.TargetType).MaximumLength(32);
        RuleFor(x => x.Layout).MaximumLength(32);
    }
}

public sealed class PageDefinitionUpdateRequestValidator : AbstractValidator<PageDefinitionUpdateRequest>
{
    public PageDefinitionUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(256)
            .Must(p => p.StartsWith('/'));
        RuleFor(x => x.TargetType).NotEmpty().MaximumLength(32)
            .Must(t => t is "web" or "mini_program" or "hybrid")
            .WithMessage("targetType 仅允许 web / mini_program / hybrid");
        RuleFor(x => x.Layout).NotEmpty().MaximumLength(32)
            .Must(l => l is "free" or "flow" or "responsive")
            .WithMessage("layout 仅允许 free / flow / responsive");
    }
}

public sealed class PageSchemaReplaceRequestValidator : AbstractValidator<PageSchemaReplaceRequest>
{
    public PageSchemaReplaceRequestValidator()
    {
        RuleFor(x => x.SchemaJson).NotEmpty().MaximumLength(2_000_000);
    }
}

public sealed class PagesReorderRequestValidator : AbstractValidator<PagesReorderRequest>
{
    public PagesReorderRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(child =>
        {
            child.RuleFor(i => i.Id).NotEmpty();
            child.RuleFor(i => i.OrderNo).GreaterThanOrEqualTo(0);
        });
    }
}
