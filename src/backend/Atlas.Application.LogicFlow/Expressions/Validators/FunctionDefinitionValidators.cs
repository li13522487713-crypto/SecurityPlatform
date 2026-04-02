using Atlas.Application.LogicFlow.Expressions.Models;
using FluentValidation;

namespace Atlas.Application.LogicFlow.Expressions.Validators;

public sealed class FunctionDefinitionCreateRequestValidator : AbstractValidator<FunctionDefinitionCreateRequest>
{
    public FunctionDefinitionCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .Matches("^[A-Za-z_][A-Za-z0-9_]*$").WithMessage("函数名只能包含字母、数字和下划线");
        RuleFor(x => x.ParametersJson).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.ReturnType).IsInEnum();
    }
}

public sealed class FunctionDefinitionUpdateRequestValidator : AbstractValidator<FunctionDefinitionUpdateRequest>
{
    public FunctionDefinitionUpdateRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .Matches("^[A-Za-z_][A-Za-z0-9_]*$").WithMessage("函数名只能包含字母、数字和下划线");
        RuleFor(x => x.ParametersJson).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.ReturnType).IsInEnum();
    }
}

public sealed class DecisionTableCreateRequestValidator : AbstractValidator<DecisionTableCreateRequest>
{
    public DecisionTableCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.HitPolicy).IsInEnum();
        RuleFor(x => x.InputColumnsJson).NotEmpty();
        RuleFor(x => x.OutputColumnsJson).NotEmpty();
        RuleFor(x => x.RowsJson).NotEmpty();
    }
}

public sealed class DecisionTableUpdateRequestValidator : AbstractValidator<DecisionTableUpdateRequest>
{
    public DecisionTableUpdateRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.HitPolicy).IsInEnum();
        RuleFor(x => x.InputColumnsJson).NotEmpty();
        RuleFor(x => x.OutputColumnsJson).NotEmpty();
        RuleFor(x => x.RowsJson).NotEmpty();
    }
}

public sealed class RuleChainCreateRequestValidator : AbstractValidator<RuleChainCreateRequest>
{
    public RuleChainCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StepsJson).NotEmpty();
    }
}

public sealed class RuleChainUpdateRequestValidator : AbstractValidator<RuleChainUpdateRequest>
{
    public RuleChainUpdateRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StepsJson).NotEmpty();
    }
}
