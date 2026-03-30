using System.Text.RegularExpressions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Atlas.Application.Validators;

public sealed class DynamicTableCreateRequestValidator : AbstractValidator<DynamicTableCreateRequest>
{
    private static readonly Regex KeyPattern = new("^[A-Za-z][A-Za-z0-9_]{1,63}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedDbTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sqlite", "SqlServer", "MySql", "PostgreSql"
    };
    private static readonly HashSet<string> AllowedFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Int", "Long", "Decimal", "String", "Text", "Bool", "DateTime", "Date"
    };
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "where", "table", "index", "create", "drop", "delete", "update", "insert", "alter",
        "TenantIdValue"
    };

    public DynamicTableCreateRequestValidator(IStringLocalizer<Messages> localizer)
    {
        RuleFor(x => x.TableKey)
            .NotEmpty().WithMessage(localizer["DynamicTableTableKeyInvalid"].Value)
            .Must(BeValidKey).WithMessage(localizer["DynamicTableTableKeyInvalid"].Value);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(localizer["DynamicTableDisplayNameRequired"].Value)
            .MaximumLength(100).WithMessage(localizer["DynamicTableDisplayNameMaxLength", 100].Value);

        RuleFor(x => x.DbType)
            .NotEmpty().WithMessage(localizer["DynamicTableDbTypeInvalid"].Value)
            .Must(type => AllowedDbTypes.Contains(type))
            .WithMessage(localizer["DynamicTableDbTypeInvalid"].Value);

        RuleFor(x => x.Fields)
            .NotNull()
            .Must(fields => fields.Count is > 0 and <= 200)
            .WithMessage(localizer["DynamicTableFieldsCountInvalid"].Value);

        RuleForEach(x => x.Fields)
            .Must(field => BeValidField(field.Name))
            .WithMessage(localizer["DynamicTableFieldNameInvalid"].Value);

        RuleForEach(x => x.Fields)
            .Must(field => AllowedFieldTypes.Contains(field.FieldType))
            .WithMessage(localizer["DynamicTableFieldTypeInvalid"].Value);

        RuleForEach(x => x.Fields)
            .Must(ValidateFieldLength)
            .WithMessage(localizer["DynamicTableFieldLengthInvalid"].Value);

        RuleForEach(x => x.Fields)
            .Must(ValidateFieldValidation)
            .WithMessage(localizer["DynamicTableFieldValidationInvalid"].Value);

        RuleFor(x => x.Fields)
            .Must(HaveSinglePrimaryKey)
            .WithMessage(localizer["DynamicTablePrimaryKeyRequired"].Value);

        RuleFor(x => x.Fields)
            .Must(IsPrimaryKeyTypeValid)
            .WithMessage(localizer["DynamicTablePrimaryKeyTypeInvalid"].Value);

        RuleFor(x => x.Fields)
            .Must(HaveUniqueFieldNames)
            .WithMessage(localizer["DynamicTableFieldNamesDuplicated"].Value);

        RuleFor(x => x.Fields)
            .Must(AutoIncrementMustBePrimaryKey)
            .WithMessage(localizer["DynamicTableAutoIncrementInvalid"].Value);

        RuleFor(x => x.AppId)
            .Must(appId => string.IsNullOrWhiteSpace(appId) || (long.TryParse(appId, out var parsed) && parsed > 0))
            .WithMessage("AppId 格式无效");
    }

    private static bool BeValidKey(string key)
    {
        var normalized = key?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (!KeyPattern.IsMatch(normalized))
        {
            return false;
        }

        return !ReservedNames.Contains(normalized);
    }

    private static bool BeValidField(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (!KeyPattern.IsMatch(normalized))
        {
            return false;
        }

        return !ReservedNames.Contains(normalized);
    }

    private static bool ValidateFieldLength(DynamicFieldDefinition field)
    {
        if (field.FieldType.Equals("String", StringComparison.OrdinalIgnoreCase))
        {
            return field.Length is > 0 and <= 4000;
        }

        if (field.FieldType.Equals("Decimal", StringComparison.OrdinalIgnoreCase))
        {
            return field.Precision is > 0 and <= 38
                && field.Scale is >= 0 and <= 18
                && field.Scale <= field.Precision;
        }

        return true;
    }

    private static bool ValidateFieldValidation(DynamicFieldDefinition field)
    {
        var validation = field.Validation;
        if (validation is null)
        {
            return true;
        }

        if (validation.MinLength.HasValue && validation.MinLength < 0)
        {
            return false;
        }

        if (validation.MaxLength.HasValue && validation.MaxLength < 0)
        {
            return false;
        }

        if (validation.MinLength.HasValue && validation.MaxLength.HasValue && validation.MinLength > validation.MaxLength)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(validation.Pattern))
        {
            try
            {
                _ = new Regex(validation.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HaveSinglePrimaryKey(IReadOnlyList<DynamicFieldDefinition> fields)
    {
        var count = fields.Count(x => x.IsPrimaryKey);
        return count == 1;
    }

    private static bool IsPrimaryKeyTypeValid(IReadOnlyList<DynamicFieldDefinition> fields)
    {
        var pk = fields.FirstOrDefault(x => x.IsPrimaryKey);
        if (pk is null)
        {
            return false;
        }

        var isInt = pk.FieldType.Equals("Int", StringComparison.OrdinalIgnoreCase);
        var isLong = pk.FieldType.Equals("Long", StringComparison.OrdinalIgnoreCase);
        if (!isInt && !isLong)
        {
            return false;
        }

        if (pk.IsAutoIncrement && !pk.IsPrimaryKey)
        {
            return false;
        }

        return true;
    }

    private static bool HaveUniqueFieldNames(IReadOnlyList<DynamicFieldDefinition> fields)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (!set.Add(field.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AutoIncrementMustBePrimaryKey(IReadOnlyList<DynamicFieldDefinition> fields)
    {
        foreach (var field in fields)
        {
            if (!field.IsAutoIncrement)
            {
                continue;
            }

            if (!field.IsPrimaryKey)
            {
                return false;
            }

            var isInt = field.FieldType.Equals("Int", StringComparison.OrdinalIgnoreCase);
            var isLong = field.FieldType.Equals("Long", StringComparison.OrdinalIgnoreCase);
            if (!isInt && !isLong)
            {
                return false;
            }
        }

        return true;
    }
}
