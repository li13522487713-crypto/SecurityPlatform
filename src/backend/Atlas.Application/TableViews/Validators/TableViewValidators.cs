using Atlas.Application.TableViews.Models;
using FluentValidation;

namespace Atlas.Application.TableViews.Validators;

public sealed class TableViewCreateRequestValidator : AbstractValidator<TableViewCreateRequest>
{
    public TableViewCreateRequestValidator()
    {
        RuleFor(x => x.TableKey)
            .NotEmpty().WithMessage("TableKey不能为空")
            .MaximumLength(100).WithMessage("TableKey长度超出限制");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("视图名称不能为空")
            .MaximumLength(50).WithMessage("视图名称长度超出限制");
        RuleFor(x => x.Config)
            .NotNull().WithMessage("视图配置不能为空");
    }
}

public sealed class TableViewUpdateRequestValidator : AbstractValidator<TableViewUpdateRequest>
{
    public TableViewUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("视图名称不能为空")
            .MaximumLength(50).WithMessage("视图名称长度超出限制");
        RuleFor(x => x.Config)
            .NotNull().WithMessage("视图配置不能为空");
    }
}

public sealed class TableViewConfigUpdateRequestValidator : AbstractValidator<TableViewConfigUpdateRequest>
{
    public TableViewConfigUpdateRequestValidator()
    {
        RuleFor(x => x.Config)
            .NotNull().WithMessage("视图配置不能为空");
    }
}

public sealed class TableViewDuplicateRequestValidator : AbstractValidator<TableViewDuplicateRequest>
{
    public TableViewDuplicateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("视图名称不能为空")
            .MaximumLength(50).WithMessage("视图名称长度超出限制");
    }
}
