using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Runtime.Expressions;

public static class MicroflowExpressionTokenKind
{
    public const string Identifier = "identifier";
    public const string DollarVariable = "dollarVariable";
    public const string StringLiteral = "stringLiteral";
    public const string NumberLiteral = "numberLiteral";
    public const string BooleanLiteral = "booleanLiteral";
    public const string NullLiteral = "nullLiteral";
    public const string Operator = "operator";
    public const string Slash = "slash";
    public const string Dot = "dot";
    public const string Comma = "comma";
    public const string OpenParen = "openParen";
    public const string CloseParen = "closeParen";
    public const string Keyword = "keyword";
    public const string Eof = "eof";
    public const string Unknown = "unknown";
}

public sealed record MicroflowExpressionToken
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = MicroflowExpressionTokenKind.Unknown;

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("end")]
    public int End { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowExpressionDiagnostic>();
}

public sealed class MicroflowExpressionTokenizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not", "if", "then", "else", "empty"
    };

    public IReadOnlyList<MicroflowExpressionToken> Tokenize(string? raw)
    {
        var source = raw ?? string.Empty;
        var tokens = new List<MicroflowExpressionToken>();
        var index = 0;

        while (index < source.Length)
        {
            var c = source[index];
            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            var start = index;
            if (c == '$')
            {
                index++;
                var nameStart = index;
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    index++;
                }

                if (index == nameStart)
                {
                    tokens.Add(Token(MicroflowExpressionTokenKind.Unknown, "$", start, index, Diagnostic(
                        MicroflowExpressionDiagnosticCode.ParseError,
                        "$ 后必须跟变量名。",
                        start,
                        index)));
                    continue;
                }

                tokens.Add(Token(MicroflowExpressionTokenKind.DollarVariable, source[start..index], start, index));
                continue;
            }

            if (c is '\'' or '"')
            {
                tokens.Add(ReadString(source, ref index));
                continue;
            }

            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(source, ref index));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                index++;
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    index++;
                }

                var text = source[start..index];
                var lower = text.ToLowerInvariant();
                if (lower is "true" or "false")
                {
                    tokens.Add(Token(MicroflowExpressionTokenKind.BooleanLiteral, text, start, index));
                }
                else if (lower == "null")
                {
                    tokens.Add(Token(MicroflowExpressionTokenKind.NullLiteral, text, start, index));
                }
                else if (Keywords.Contains(text))
                {
                    tokens.Add(Token(MicroflowExpressionTokenKind.Keyword, text, start, index));
                }
                else
                {
                    tokens.Add(Token(MicroflowExpressionTokenKind.Identifier, text, start, index));
                }
                continue;
            }

            if (c == '>' || c == '<' || c == '!' || c == '=')
            {
                index++;
                if (index < source.Length && source[index] == '=')
                {
                    index++;
                }

                var op = source[start..index];
                if (op is "!" or "=>")
                {
                    tokens.Add(Token(MicroflowExpressionTokenKind.Unknown, op, start, index, Diagnostic(
                        MicroflowExpressionDiagnosticCode.UnknownToken,
                        $"不支持的操作符：{op}",
                        start,
                        index)));
                }
                else
                {
                    tokens.Add(Token(MicroflowExpressionTokenKind.Operator, op, start, index));
                }
                continue;
            }

            if (c is '+' or '-' or '*')
            {
                index++;
                tokens.Add(Token(MicroflowExpressionTokenKind.Operator, source[start..index], start, index));
                continue;
            }

            if (c == '/')
            {
                index++;
                tokens.Add(Token(MicroflowExpressionTokenKind.Slash, "/", start, index));
                continue;
            }

            if (c == '.')
            {
                index++;
                tokens.Add(Token(MicroflowExpressionTokenKind.Dot, ".", start, index));
                continue;
            }

            if (c == ',')
            {
                index++;
                tokens.Add(Token(MicroflowExpressionTokenKind.Comma, ",", start, index));
                continue;
            }

            if (c == '(')
            {
                index++;
                tokens.Add(Token(MicroflowExpressionTokenKind.OpenParen, "(", start, index));
                continue;
            }

            if (c == ')')
            {
                index++;
                tokens.Add(Token(MicroflowExpressionTokenKind.CloseParen, ")", start, index));
                continue;
            }

            index++;
            tokens.Add(Token(MicroflowExpressionTokenKind.Unknown, source[start..index], start, index, Diagnostic(
                MicroflowExpressionDiagnosticCode.UnknownToken,
                $"未知表达式 token：{source[start..index]}",
                start,
                index)));
        }

        tokens.Add(Token(MicroflowExpressionTokenKind.Eof, string.Empty, source.Length, source.Length));
        return tokens;
    }

    private static MicroflowExpressionToken ReadString(string source, ref int index)
    {
        var quote = source[index];
        var start = index;
        index++;
        var builder = new StringBuilder();
        var closed = false;

        while (index < source.Length)
        {
            var c = source[index++];
            if (c == quote)
            {
                closed = true;
                break;
            }

            if (c == '\\' && index < source.Length)
            {
                var escaped = source[index++];
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    _ => escaped
                });
                continue;
            }

            builder.Append(c);
        }

        var diagnostics = closed
            ? Array.Empty<MicroflowExpressionDiagnostic>()
            : new[]
            {
                Diagnostic(
                    MicroflowExpressionDiagnosticCode.ParseError,
                    "字符串字面量未闭合。",
                    start,
                    index)
            };

        return new MicroflowExpressionToken
        {
            Kind = MicroflowExpressionTokenKind.StringLiteral,
            Text = builder.ToString(),
            Start = start,
            End = index,
            Diagnostics = diagnostics
        };
    }

    private static MicroflowExpressionToken ReadNumber(string source, ref int index)
    {
        var start = index;
        while (index < source.Length && char.IsDigit(source[index]))
        {
            index++;
        }

        if (index < source.Length && source[index] == '.')
        {
            index++;
            while (index < source.Length && char.IsDigit(source[index]))
            {
                index++;
            }
        }

        var text = source[start..index];
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            return Token(MicroflowExpressionTokenKind.Unknown, text, start, index, Diagnostic(
                MicroflowExpressionDiagnosticCode.ParseError,
                $"数字字面量不合法：{text}",
                start,
                index));
        }

        return Token(MicroflowExpressionTokenKind.NumberLiteral, text, start, index);
    }

    private static MicroflowExpressionToken Token(
        string kind,
        string text,
        int start,
        int end,
        params MicroflowExpressionDiagnostic[] diagnostics)
        => new()
        {
            Kind = kind,
            Text = text,
            Start = start,
            End = end,
            Diagnostics = diagnostics
        };

    private static MicroflowExpressionDiagnostic Diagnostic(string code, string message, int start, int end)
        => new()
        {
            Code = code,
            Severity = MicroflowExpressionDiagnosticSeverity.Error,
            Message = message,
            Start = start,
            End = end
        };

    private static bool IsIdentifierStart(char c)
        => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c)
        => char.IsLetterOrDigit(c) || c == '_';
}
