using System.Globalization;

namespace Atlas.Application.Microflows.Runtime.Expressions;

public sealed class MicroflowExpressionParser
{
    private readonly MicroflowExpressionTokenizer _tokenizer = new();
    private IReadOnlyList<MicroflowExpressionToken> _tokens = Array.Empty<MicroflowExpressionToken>();
    private string _raw = string.Empty;
    private int _position;
    private readonly List<MicroflowExpressionDiagnostic> _diagnostics = [];

    public MicroflowExpressionParseResult Parse(string? raw)
    {
        _raw = raw ?? string.Empty;
        _tokens = _tokenizer.Tokenize(_raw);
        _position = 0;
        _diagnostics.Clear();
        _diagnostics.AddRange(_tokens.SelectMany(token => token.Diagnostics));

        if (_tokens.Count == 0 || Current.Kind == MicroflowExpressionTokenKind.Eof)
        {
            var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, "表达式不能为空。", 0, 0);
            _diagnostics.Add(diagnostic);
            return Result(new MicroflowExpressionInvalidNode
            {
                Raw = _raw,
                Start = 0,
                End = 0,
                Diagnostics = [diagnostic]
            });
        }

        var expression = ParseIfExpression();
        if (Current.Kind != MicroflowExpressionTokenKind.Eof)
        {
            _diagnostics.Add(Diagnostic(
                MicroflowExpressionDiagnosticCode.TrailingToken,
                $"表达式存在未消费 token：{Current.Text}",
                Current.Start,
                Current.End));
        }

        return Result(expression with { Diagnostics = expression.Diagnostics.Concat(_diagnostics).Distinct().ToArray() });
    }

    private MicroflowExpressionAstNode ParseIfExpression()
    {
        if (!MatchKeyword("if"))
        {
            return ParseOr();
        }

        var ifToken = Previous;
        var condition = ParseOr();
        if (!MatchKeyword("then"))
        {
            var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, "if 表达式缺少 then。", Current.Start, Current.End);
            _diagnostics.Add(diagnostic);
            return Invalid(ifToken.Start, Current.End, diagnostic);
        }

        var thenExpression = ParseIfExpression();
        if (!MatchKeyword("else"))
        {
            var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, "if 表达式缺少 else。", Current.Start, Current.End);
            _diagnostics.Add(diagnostic);
            return Invalid(ifToken.Start, Current.End, diagnostic);
        }

        var elseExpression = ParseIfExpression();
        return new MicroflowExpressionIfNode
        {
            Raw = Slice(ifToken.Start, elseExpression.End),
            Start = ifToken.Start,
            End = elseExpression.End,
            Condition = condition,
            ThenExpression = thenExpression,
            ElseExpression = elseExpression
        };
    }

    private MicroflowExpressionAstNode ParseOr()
    {
        var expression = ParseAnd();
        while (MatchKeyword("or"))
        {
            var op = Previous;
            var right = ParseAnd();
            expression = Binary(op, expression, right);
        }

        return expression;
    }

    private MicroflowExpressionAstNode ParseAnd()
    {
        var expression = ParseComparison();
        while (MatchKeyword("and"))
        {
            var op = Previous;
            var right = ParseComparison();
            expression = Binary(op, expression, right);
        }

        return expression;
    }

    private MicroflowExpressionAstNode ParseComparison()
    {
        var expression = ParseAdditive();
        while (Current.Kind == MicroflowExpressionTokenKind.Operator
               && Current.Text is "=" or "!=" or ">" or "<" or ">=" or "<=")
        {
            var op = Advance();
            var right = ParseAdditive();
            expression = Binary(op, expression, right);
        }

        return expression;
    }

    private MicroflowExpressionAstNode ParseAdditive()
    {
        var expression = ParseMultiplicative();
        while (Current.Kind == MicroflowExpressionTokenKind.Operator && Current.Text is "+" or "-")
        {
            var op = Advance();
            var right = ParseMultiplicative();
            expression = Binary(op, expression, right);
        }

        return expression;
    }

    private MicroflowExpressionAstNode ParseMultiplicative()
    {
        var expression = ParseUnary();
        while ((Current.Kind == MicroflowExpressionTokenKind.Operator && Current.Text == "*")
               || Current.Kind == MicroflowExpressionTokenKind.Slash)
        {
            var op = Advance();
            var right = ParseUnary();
            expression = Binary(op with { Text = op.Kind == MicroflowExpressionTokenKind.Slash ? "/" : op.Text }, expression, right);
        }

        return expression;
    }

    private MicroflowExpressionAstNode ParseUnary()
    {
        if (MatchKeyword("not"))
        {
            var op = Previous;
            var operand = ParseUnary();
            return new MicroflowExpressionUnaryNode
            {
                Raw = Slice(op.Start, operand.End),
                Start = op.Start,
                End = operand.End,
                Operator = op.Text,
                Operand = operand
            };
        }

        if (Current.Kind == MicroflowExpressionTokenKind.Operator && Current.Text == "-")
        {
            var op = Advance();
            var operand = ParseUnary();
            return new MicroflowExpressionUnaryNode
            {
                Raw = Slice(op.Start, operand.End),
                Start = op.Start,
                End = operand.End,
                Operator = op.Text,
                Operand = operand
            };
        }

        return ParsePrimary();
    }

    private MicroflowExpressionAstNode ParsePrimary()
    {
        if (Current.Kind == MicroflowExpressionTokenKind.StringLiteral)
        {
            var token = Advance();
            return new MicroflowExpressionLiteralNode
            {
                Raw = Slice(token.Start, token.End),
                Start = token.Start,
                End = token.End,
                LiteralKind = MicroflowExpressionLiteralKind.String,
                StringValue = token.Text
            };
        }

        if (Current.Kind == MicroflowExpressionTokenKind.NumberLiteral)
        {
            var token = Advance();
            var isDecimal = token.Text.Contains('.', StringComparison.Ordinal);
            return new MicroflowExpressionLiteralNode
            {
                Raw = token.Text,
                Start = token.Start,
                End = token.End,
                LiteralKind = isDecimal ? MicroflowExpressionLiteralKind.Decimal : MicroflowExpressionLiteralKind.Integer,
                DecimalValue = decimal.Parse(token.Text, CultureInfo.InvariantCulture),
                IntegerValue = isDecimal ? null : long.Parse(token.Text, CultureInfo.InvariantCulture)
            };
        }

        if (Current.Kind == MicroflowExpressionTokenKind.BooleanLiteral)
        {
            var token = Advance();
            return new MicroflowExpressionLiteralNode
            {
                Raw = token.Text,
                Start = token.Start,
                End = token.End,
                LiteralKind = MicroflowExpressionLiteralKind.Boolean,
                BoolValue = string.Equals(token.Text, "true", StringComparison.OrdinalIgnoreCase)
            };
        }

        if (Current.Kind == MicroflowExpressionTokenKind.NullLiteral)
        {
            var token = Advance();
            return new MicroflowExpressionLiteralNode
            {
                Raw = token.Text,
                Start = token.Start,
                End = token.End,
                LiteralKind = MicroflowExpressionLiteralKind.Null
            };
        }

        if (Current.Kind == MicroflowExpressionTokenKind.DollarVariable)
        {
            return ParseVariableOrMemberAccess();
        }

        if ((Current.Kind == MicroflowExpressionTokenKind.Identifier || Current.Kind == MicroflowExpressionTokenKind.Keyword)
            && Peek.Kind == MicroflowExpressionTokenKind.OpenParen)
        {
            return ParseFunctionCall();
        }

        if (Current.Kind == MicroflowExpressionTokenKind.Keyword && KeywordEquals(Current, "empty"))
        {
            var token = Advance();
            return new MicroflowExpressionLiteralNode
            {
                Raw = token.Text,
                Start = token.Start,
                End = token.End,
                LiteralKind = MicroflowExpressionLiteralKind.Empty
            };
        }

        if (Current.Kind == MicroflowExpressionTokenKind.Identifier)
        {
            return ParseEnumerationValue();
        }

        if (Match(MicroflowExpressionTokenKind.OpenParen))
        {
            var open = Previous;
            var expression = ParseIfExpression();
            if (!Match(MicroflowExpressionTokenKind.CloseParen))
            {
                var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, "括号不匹配，缺少 )。", open.Start, Current.End);
                _diagnostics.Add(diagnostic);
                return Invalid(open.Start, Current.End, diagnostic);
            }

            return expression;
        }

        var current = Advance();
        var error = current.Diagnostics.FirstOrDefault() ?? Diagnostic(
            MicroflowExpressionDiagnosticCode.ParseError,
            $"无法解析表达式 token：{current.Text}",
            current.Start,
            current.End);
        _diagnostics.Add(error);
        return Invalid(current.Start, current.End, error);
    }

    private MicroflowExpressionAstNode ParseVariableOrMemberAccess()
    {
        var variable = Advance();
        var variableName = variable.Text.TrimStart('$');
        var normalized = variableName;
        var members = new List<string>();
        var end = variable.End;

        while (Current.Kind == MicroflowExpressionTokenKind.Slash)
        {
            var slash = Advance();
            if (Current.Kind != MicroflowExpressionTokenKind.Identifier)
            {
                var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, "成员访问 / 后必须跟成员名。", slash.Start, slash.End);
                _diagnostics.Add(diagnostic);
                return Invalid(variable.Start, slash.End, diagnostic);
            }

            var member = Advance();
            members.Add(member.Text);
            end = member.End;
        }

        if (members.Count == 0)
        {
            return new MicroflowExpressionVariableReferenceNode
            {
                Raw = variable.Text,
                Start = variable.Start,
                End = variable.End,
                VariableName = variable.Text,
                NormalizedName = normalized,
                IncludesDollarPrefix = true
            };
        }

        return new MicroflowExpressionMemberAccessNode
        {
            Raw = Slice(variable.Start, end),
            Start = variable.Start,
            End = end,
            RootVariableName = normalized,
            MemberPath = members,
            RawPath = string.Join("/", members),
            NormalizedPath = string.Join(".", members)
        };
    }

    private MicroflowExpressionAstNode ParseFunctionCall()
    {
        var name = Advance();
        var open = Advance();
        var args = new List<MicroflowExpressionAstNode>();
        if (Current.Kind != MicroflowExpressionTokenKind.CloseParen)
        {
            do
            {
                args.Add(ParseIfExpression());
            }
            while (Match(MicroflowExpressionTokenKind.Comma));
        }

        if (!Match(MicroflowExpressionTokenKind.CloseParen))
        {
            var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, $"函数 {name.Text} 缺少闭合括号。", open.Start, Current.End);
            _diagnostics.Add(diagnostic);
            return Invalid(name.Start, Current.End, diagnostic);
        }

        return new MicroflowExpressionFunctionCallNode
        {
            Raw = Slice(name.Start, Previous.End),
            Start = name.Start,
            End = Previous.End,
            FunctionName = name.Text,
            Arguments = args
        };
    }

    private MicroflowExpressionAstNode ParseEnumerationValue()
    {
        var start = Current.Start;
        var parts = new List<string> { Advance().Text };
        while (Current.Kind == MicroflowExpressionTokenKind.Dot)
        {
            Advance();
            if (Current.Kind != MicroflowExpressionTokenKind.Identifier)
            {
                var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, "枚举值 . 后必须跟名称。", Previous.Start, Previous.End);
                _diagnostics.Add(diagnostic);
                return Invalid(start, Previous.End, diagnostic);
            }

            parts.Add(Advance().Text);
        }

        if (parts.Count < 2)
        {
            var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, $"裸标识符暂不支持：{parts[0]}", start, Previous.End);
            _diagnostics.Add(diagnostic);
            return Invalid(start, Previous.End, diagnostic);
        }

        var qualifiedName = string.Join(".", parts);
        return new MicroflowExpressionEnumerationValueNode
        {
            Raw = qualifiedName,
            Start = start,
            End = Previous.End,
            QualifiedName = qualifiedName,
            EnumQualifiedName = parts.Count > 2 ? string.Join(".", parts.Take(parts.Count - 1)) : null,
            ValueName = parts[^1]
        };
    }

    private MicroflowExpressionBinaryNode Binary(MicroflowExpressionToken op, MicroflowExpressionAstNode left, MicroflowExpressionAstNode right)
        => new()
        {
            Raw = Slice(left.Start, right.End),
            Start = left.Start,
            End = right.End,
            Operator = op.Text,
            Left = left,
            Right = right
        };

    private MicroflowExpressionInvalidNode Invalid(int start, int end, params MicroflowExpressionDiagnostic[] diagnostics)
        => new()
        {
            Raw = Slice(start, end),
            Start = start,
            End = end,
            Diagnostics = diagnostics
        };

    private MicroflowExpressionParseResult Result(MicroflowExpressionAstNode ast)
        => new()
        {
            Raw = _raw,
            Tokens = _tokens,
            Ast = ast,
            Diagnostics = _diagnostics.ToArray()
        };

    private bool Match(string kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool MatchKeyword(string text)
    {
        if (Current.Kind != MicroflowExpressionTokenKind.Keyword || !KeywordEquals(Current, text))
        {
            return false;
        }

        Advance();
        return true;
    }

    private MicroflowExpressionToken Advance()
    {
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }

        return Previous;
    }

    private MicroflowExpressionToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];

    private MicroflowExpressionToken Previous => _tokens[Math.Max(0, _position - 1)];

    private MicroflowExpressionToken Peek => _tokens[Math.Min(_position + 1, _tokens.Count - 1)];

    private static bool KeywordEquals(MicroflowExpressionToken token, string value)
        => string.Equals(token.Text, value, StringComparison.OrdinalIgnoreCase);

    private MicroflowExpressionDiagnostic Diagnostic(string code, string message, int start, int end)
        => new()
        {
            Code = code,
            Severity = MicroflowExpressionDiagnosticSeverity.Error,
            Message = message,
            Start = start,
            End = end
        };

    private string Slice(int start, int end)
    {
        var safeStart = Math.Clamp(start, 0, _raw.Length);
        var safeEnd = Math.Clamp(end, safeStart, _raw.Length);
        return _raw[safeStart..safeEnd];
    }
}
