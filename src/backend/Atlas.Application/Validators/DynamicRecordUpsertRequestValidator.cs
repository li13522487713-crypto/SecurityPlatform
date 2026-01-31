using System.Text.RegularExpressions;
using Atlas.Application.DynamicTables.Models;
using FluentValidation;

namespace Atlas.Application.Validators;

public sealed class DynamicRecordUpsertRequestValidator : AbstractValidator<DynamicRecordUpsertRequest>
{
    private static readonly Regex FieldPattern = new("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "String", "Int", "Long", "Decimal", "Bool", "DateTime", "Date"
    };

    public DynamicRecordUpsertRequestValidator()
    {
        RuleFor(x => x.Values)
            .NotNull()
            .Must(values => values.Count > 0)
            .WithMessage("记录值不能为空。");

        RuleForEach(x => x.Values)
            .Must(value => !string.IsNullOrWhiteSpace(value.Field))
            .WithMessage("字段名不能为空。");

        RuleForEach(x => x.Values)
            .Must(value => FieldPattern.IsMatch(value.Field))
            .WithMessage("字段名格式不正确。");

        RuleForEach(x => x.Values)
            .Must(value => AllowedValueTypes.Contains(value.ValueType))
            .WithMessage("字段值类型不支持。");

        RuleForEach(x => x.Values)
            .Must(HasSingleValue)
            .WithMessage("字段值必须且只能填写一个具体类型。");

        RuleFor(x => x.Values)
            .Must(HaveUniqueFields)
            .WithMessage("字段名不能重复。");
    }

    private static bool HasSingleValue(DynamicFieldValueDto value)
    {
        var count = 0;
        if (value.StringValue is not null) count++;
        if (value.IntValue.HasValue) count++;
        if (value.LongValue.HasValue) count++;
        if (value.DecimalValue.HasValue) count++;
        if (value.BoolValue.HasValue) count++;
        if (value.DateTimeValue.HasValue) count++;
        if (value.DateValue.HasValue) count++;
        return count == 1;
    }

    private static bool HaveUniqueFields(IReadOnlyList<DynamicFieldValueDto> values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (!set.Add(value.Field))
            {
                return false;
            }
        }

        return true;
    }
}
