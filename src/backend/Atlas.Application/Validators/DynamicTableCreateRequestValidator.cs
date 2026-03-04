using System.Text.RegularExpressions;
using Atlas.Application.DynamicTables.Models;
using FluentValidation;

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

    public DynamicTableCreateRequestValidator()
    {
        RuleFor(x => x.TableKey)
            .NotEmpty()
            .Must(BeValidKey).WithMessage("表标识格式不正确。");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DbType)
            .NotEmpty()
            .Must(type => AllowedDbTypes.Contains(type))
            .WithMessage("数据库类型不支持。");

        RuleFor(x => x.Fields)
            .NotNull()
            .Must(fields => fields.Count is > 0 and <= 200)
            .WithMessage("字段数量必须在 1-200 之间。");

        RuleForEach(x => x.Fields)
            .Must(field => BeValidField(field.Name))
            .WithMessage("字段名格式不正确。");

        RuleForEach(x => x.Fields)
            .Must(field => AllowedFieldTypes.Contains(field.FieldType))
            .WithMessage("字段类型不支持。");

        RuleForEach(x => x.Fields)
            .Must(ValidateFieldLength)
            .WithMessage("字段长度/精度设置不合法。");

        RuleFor(x => x.Fields)
            .Must(HaveSinglePrimaryKey)
            .WithMessage("必须且只能有一个主键字段。");

        RuleFor(x => x.Fields)
            .Must(IsPrimaryKeyTypeValid)
            .WithMessage("主键仅支持 Int/Long 类型。");

        RuleFor(x => x.Fields)
            .Must(HaveUniqueFieldNames)
            .WithMessage("字段名不能重复。");

        RuleFor(x => x.Fields)
            .Must(AutoIncrementMustBePrimaryKey)
            .WithMessage("自增字段必须为主键且类型为 Int/Long。");
    }

    private static bool BeValidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!KeyPattern.IsMatch(key))
        {
            return false;
        }

        return !ReservedNames.Contains(key);
    }

    private static bool BeValidField(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!KeyPattern.IsMatch(name))
        {
            return false;
        }

        return !ReservedNames.Contains(name);
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
