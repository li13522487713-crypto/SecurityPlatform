using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.Expressions;

/// <summary>
/// CEL（Common Expression Language）安全子集引擎实现。
/// 支持：字符串/数字/布尔比较、contains/startsWith/endsWith、&amp;&amp;/||。
/// 禁止：eval、new、import、反射、危险函数调用。
/// </summary>
public sealed partial class CelExpressionEngine : IExpressionEngine
{
    private static readonly IReadOnlyList<string> SafeFunctions =
    [
        "contains", "startsWith", "endsWith", "matches",
        "size", "len", "now", "today",
        "string", "int", "double", "bool"
    ];

    private static readonly IReadOnlyList<string> DangerousPatterns =
    [
        "eval", "import", "require", "exec", "system", "Process",
        "Reflection", "Assembly", "Type.GetType", "Activator",
        "new ", "typeof(", "dynamic"
    ];

    public ExpressionValidationResult Validate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ExpressionValidationResult.Failure(["表达式不能为空"]);

        if (expression.Length > 4096)
            return ExpressionValidationResult.Failure(["表达式长度超过 4096 字符限制"]);

        var errors = new List<string>();
        foreach (var danger in DangerousPatterns)
        {
            if (expression.Contains(danger, StringComparison.OrdinalIgnoreCase))
                errors.Add($"表达式包含禁止模式：{danger}");
        }

        return errors.Count > 0
            ? ExpressionValidationResult.Failure(errors)
            : ExpressionValidationResult.Ok();
    }

    public bool EvaluateBool(string expression, ExpressionContext context)
    {
        var result = Evaluate(expression, context);
        return result switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var bv) ? bv : !string.IsNullOrEmpty(s),
            _ => result != null
        };
    }

    public object? Evaluate(string expression, ExpressionContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        var validation = Validate(expression);
        if (!validation.IsValid)
            return null;

        try
        {
            return EvaluateExpr(expression.Trim(), context);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<string> GetVariables(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return [];

        // 匹配 identifier.identifier 形式（排除函数调用右侧）
        var matches = VariablePattern().Matches(expression);
        return matches
            .Select(m => m.Value)
            .Where(v => !SafeFunctions.Contains(v.Split('.')[0], StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private object? EvaluateExpr(string expr, ExpressionContext context)
    {
        // || 运算（短路）
        var orParts = SplitTopLevel(expr, "||");
        if (orParts.Count > 1)
        {
            foreach (var part in orParts)
            {
                var result = EvaluateExpr(part.Trim(), context);
                if (IsTruthy(result))
                    return true;
            }
            return false;
        }

        // && 运算（短路）
        var andParts = SplitTopLevel(expr, "&&");
        if (andParts.Count > 1)
        {
            foreach (var part in andParts)
            {
                var result = EvaluateExpr(part.Trim(), context);
                if (!IsTruthy(result))
                    return false;
            }
            return true;
        }

        // contains 方法调用：form.field.contains("abc")
        const string containsToken = ".contains(";
        var ci = expr.IndexOf(containsToken, StringComparison.Ordinal);
        if (ci > 0 && expr.EndsWith(")", StringComparison.Ordinal))
        {
            var leftPart = expr[..ci].Trim();
            var argPart = expr[(ci + containsToken.Length)..^1].Trim();
            var leftVal = ResolveVariable(leftPart, context)?.ToString();
            var argVal = NormalizeLiteral(argPart);
            return leftVal?.Contains(argVal, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        // startsWith / endsWith
        const string startsToken = ".startsWith(";
        var si = expr.IndexOf(startsToken, StringComparison.Ordinal);
        if (si > 0 && expr.EndsWith(")", StringComparison.Ordinal))
        {
            var leftPart = expr[..si].Trim();
            var argPart = expr[(si + startsToken.Length)..^1].Trim();
            var leftVal = ResolveVariable(leftPart, context)?.ToString();
            return leftVal?.StartsWith(NormalizeLiteral(argPart), StringComparison.OrdinalIgnoreCase) ?? false;
        }

        const string endsToken = ".endsWith(";
        var ei = expr.IndexOf(endsToken, StringComparison.Ordinal);
        if (ei > 0 && expr.EndsWith(")", StringComparison.Ordinal))
        {
            var leftPart = expr[..ei].Trim();
            var argPart = expr[(ei + endsToken.Length)..^1].Trim();
            var leftVal = ResolveVariable(leftPart, context)?.ToString();
            return leftVal?.EndsWith(NormalizeLiteral(argPart), StringComparison.OrdinalIgnoreCase) ?? false;
        }

        // 比较运算
        var comparisonOps = new[] { ">=", "<=", "==", "!=", ">", "<" };
        foreach (var op in comparisonOps)
        {
            var idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx <= 0)
                continue;

            // 避免 != 被 ! 和 = 分别解析
            if (op == ">" && idx + 1 < expr.Length && expr[idx + 1] == '=') continue;
            if (op == "<" && idx + 1 < expr.Length && expr[idx + 1] == '=') continue;

            var leftPart = expr[..idx].Trim();
            var rightPart = expr[(idx + op.Length)..].Trim();
            var leftVal = ResolveVariable(leftPart, context)?.ToString() ?? NormalizeLiteral(leftPart);
            var rightVal = NormalizeLiteral(rightPart);
            return EvaluateComparison(leftVal, op, rightVal);
        }

        // 字面量 true/false
        if (string.Equals(expr, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(expr, "false", StringComparison.OrdinalIgnoreCase)) return false;

        // 变量解析
        return ResolveVariable(expr, context);
    }

    private static object? ResolveVariable(string identifier, ExpressionContext context)
    {
        // 支持 form.field、record.field、user.name、page.xxx 等前缀
        var parts = identifier.Split('.', 2);
        if (parts.Length == 2)
        {
            var prefix = parts[0].ToLowerInvariant();
            var fieldName = parts[1];
            var dict = prefix switch
            {
                "form" or "record" => context.Record,
                "user" => context.User,
                "page" => context.Page,
                "app" => context.App,
                "tenant" => context.Tenant,
                "global" => context.Global,
                _ => null
            };

            if (dict != null && dict.TryGetValue(fieldName, out var v))
                return v;
        }

        // 无前缀时按分层顺序查找
        if (context.TryGetVariable(identifier, out var val))
            return val;

        return null;
    }

    private static bool EvaluateComparison(string? left, string op, string? right)
    {
        if (left == null && right == null)
            return op is "==" or "<=";

        if (left == null || right == null)
            return op is "!=";

        return op switch
        {
            "==" => left == right,
            "!=" => left != right,
            ">" => CompareValues(left, right) > 0,
            "<" => CompareValues(left, right) < 0,
            ">=" => CompareValues(left, right) >= 0,
            "<=" => CompareValues(left, right) <= 0,
            _ => false
        };
    }

    private static int CompareValues(string a, string b)
    {
        if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var na)
            && double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out var nb))
            return na.CompareTo(nb);

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLiteral(string text)
    {
        if (text.Length >= 2
            && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
            return text[1..^1];

        return text;
    }

    private static bool IsTruthy(object? value) => value switch
    {
        bool b => b,
        string s => !string.IsNullOrEmpty(s) && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase),
        null => false,
        _ => true
    };

    /// <summary>
    /// 按顶层分隔符切分（不切分括号/引号内的内容）
    /// </summary>
    private static List<string> SplitTopLevel(string expr, string separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var inString = false;
        var stringChar = '\0';
        var start = 0;
        var sb = new StringBuilder();

        for (var i = 0; i < expr.Length; i++)
        {
            var c = expr[i];

            if (inString)
            {
                if (c == stringChar) inString = false;
                continue;
            }

            if (c is '"' or '\'') { inString = true; stringChar = c; continue; }
            if (c is '(' or '[') { depth++; continue; }
            if (c is ')' or ']') { depth--; continue; }

            if (depth == 0 && i + separator.Length <= expr.Length
                && expr.AsSpan(i, separator.Length).SequenceEqual(separator.AsSpan()))
            {
                parts.Add(expr[start..i].Trim());
                i += separator.Length - 1;
                start = i + 1;
            }
        }

        parts.Add(expr[start..].Trim());
        return parts.Count > 1 ? parts : [expr]; // only return split if found
    }

    [GeneratedRegex(@"[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)+")]
    private static partial Regex VariablePattern();
}
