using Atlas.Application.LogicFlow.Nodes.Models;
using FluentValidation;

namespace Atlas.Application.LogicFlow.Nodes.Validators;

public sealed class NodeTemplateCreateRequestValidator : AbstractValidator<NodeTemplateCreateRequest>
{
    public NodeTemplateCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NodeTypeKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Tags).MaximumLength(500);
    }
}
