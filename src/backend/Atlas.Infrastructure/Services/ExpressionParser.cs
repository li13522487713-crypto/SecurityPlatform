using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using SqlSugar;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Parses simplified XPath-like filter expressions into SqlSugar conditional models.
/// Supported syntax: [Field = 'Value' and Amount > 100 or Status contains 'Active']
/// Operators: =, !=, >, <, >=, <=, contains, not_contains, starts_with, ends_with
/// Logic: and, or
/// </summary>
public sealed partial class ExpressionParser
{
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "!=", ">", "<", ">=", "<=", "contains", "not_contains", "starts_with", "ends_with"
    };

    private static readonly HashSet<string> LogicOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or"
    };

    public List<IConditionalModel> Parse(string expression, HashSet<string> validFields)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return [];
        }

        var trimmed = expression.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        var conditions = new List<IConditionalModel>();
        var tokens = Tokenize(trimmed);
        var i = 0;

        while (i < tokens.Count)
        {
            if (i + 2 >= tokens.Count)
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Incomplete condition near token '{tokens[i]}'.");
            }

            var field = tokens[i];
            var op = tokens[i + 1];
            var value = tokens[i + 2];

            if (!validFields.Contains(field))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Unknown field: '{field}'.");
            }

            if (!AllowedOperators.Contains(op))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Unsupported operator: '{op}'.");
            }

            value = UnquoteValue(value);

            conditions.Add(new ConditionalModel
            {
                FieldName = field,
                ConditionalType = MapOperator(op),
                FieldValue = value
            });

            i += 3;

            if (i < tokens.Count && LogicOperators.Contains(tokens[i]))
            {
                i++;
                if (i >= tokens.Count)
                {
                    throw new BusinessException(ErrorCodes.ValidationError,
                        $"Expression ends with a dangling logic operator. A condition must follow.");
                }
            }
        }

        return conditions;
    }

    private static ConditionalType MapOperator(string op) => op.ToLowerInvariant() switch
    {
        "=" => ConditionalType.Equal,
        "!=" => ConditionalType.NoEqual,
        ">" => ConditionalType.GreaterThan,
        "<" => ConditionalType.LessThan,
        ">=" => ConditionalType.GreaterThanOrEqual,
        "<=" => ConditionalType.LessThanOrEqual,
        "contains" => ConditionalType.Like,
        "not_contains" => ConditionalType.NoLike,
        "starts_with" => ConditionalType.Like,
        "ends_with" => ConditionalType.Like,
        _ => ConditionalType.Equal
    };

    private static string UnquoteValue(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith('\'') && value.EndsWith('\'')) ||
             (value.StartsWith('"') && value.EndsWith('"'))))
        {
            return value[1..^1];
        }
        return value;
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var matches = TokenRegex().Matches(input);
        foreach (Match match in matches)
        {
            var token = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }
        return tokens;
    }

    [GeneratedRegex("""'[^']*'|"[^"]*"|[^\s]+""")]
    private static partial Regex TokenRegex();
}
