using Atlas.Application.LogicFlow.Nodes.Models;
using FluentValidation;

namespace Atlas.Application.LogicFlow.Nodes.Validators;

public sealed class NodeTypeCreateRequestValidator : AbstractValidator<NodeTypeCreateRequest>
{
    public NodeTypeCreateRequestValidator()
    {
        RuleFor(x => x.TypeKey)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*$")
            .WithMessage("TypeKey 须为 dot-separated 小写标识符，如 trigger.manual");

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
