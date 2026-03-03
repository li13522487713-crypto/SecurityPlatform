using System.Text.RegularExpressions;
using Atlas.Application.DynamicTables.Models;
using FluentValidation;

namespace Atlas.Application.Validators;

public sealed class MigrationRecordCreateRequestValidator : AbstractValidator<MigrationRecordCreateRequest>
{
    private static readonly Regex TableKeyPattern = new("^[A-Za-z][A-Za-z0-9_]{1,63}$", RegexOptions.Compiled);

    public MigrationRecordCreateRequestValidator()
    {
        RuleFor(x => x.TableKey)
            .NotEmpty()
            .Must(v => TableKeyPattern.IsMatch(v))
            .WithMessage("TableKey 格式不正确。");

        RuleFor(x => x.Version)
            .GreaterThan(0);

        RuleFor(x => x.UpScript)
            .NotEmpty()
            .MaximumLength(100_000);

        RuleFor(x => x.DownScript)
            .MaximumLength(100_000)
            .When(x => x.DownScript is not null);
    }
}
