using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Parsing;

/// <summary>
/// 递归下降语法分析器 —— 将 token 序列解析为 AST。
/// 优先级（由低到高）：ternary → or → and → equality → comparison → add → mul → unary → postfix → primary
/// </summary>
public sealed class ExprParser
{
    private readonly List<ExprToken> _tokens;
    private int _pos;

    public ExprParser(List<ExprToken> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public ExprAstNode Parse()
    {
        var node = ParseTernary();
        if (Current.Type != ExprTokenType.Eof)
            throw new ExprParseException($"Unexpected token '{Current.Lexeme}' at position {Current.Position}");
        return node;
    }

    private ExprToken Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private ExprToken Advance()
    {
        var tok = Current;
        if (_pos < _tokens.Count) _pos++;
        return tok;
    }

    private ExprToken Expect(ExprTokenType type)
    {
        if (Current.Type != type)
            throw new ExprParseException($"Expected {type} but got '{Current.Lexeme}' at position {Current.Position}");
        return Advance();
    }

    private ExprAstNode ParseTernary()
    {
        var node = ParseNullCoalesce();
        if (Current.Type == ExprTokenType.Question)
        {
            var start = Current.Position;
            Advance();
            var trueExpr = ParseTernary();
            Expect(ExprTokenType.Colon);
            var falseExpr = ParseTernary();
            return new ConditionalNode
            {
                Condition = node, TrueExpr = trueExpr, FalseExpr = falseExpr,
                StartPos = start, EndPos = falseExpr.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseNullCoalesce()
    {
        var node = ParseOr();
        while (Current.Type == ExprTokenType.QuestionQuestion)
        {
            Advance();
            var right = ParseOr();
            node = new BinaryNode
            {
                Operator = BinaryOperator.NullCoalesce, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseOr()
    {
        var node = ParseAnd();
        while (Current.Type == ExprTokenType.PipePipe)
        {
            Advance();
            var right = ParseAnd();
            node = new BinaryNode
            {
                Operator = BinaryOperator.Or, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseAnd()
    {
        var node = ParseEquality();
        while (Current.Type == ExprTokenType.AmpAmp)
        {
            Advance();
            var right = ParseEquality();
            node = new BinaryNode
            {
                Operator = BinaryOperator.And, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseEquality()
    {
        var node = ParseComparison();
        while (Current.Type is ExprTokenType.EqualEqual or ExprTokenType.BangEqual)
        {
            var op = Advance().Type == ExprTokenType.EqualEqual ? BinaryOperator.Equal : BinaryOperator.NotEqual;
            var right = ParseComparison();
            node = new BinaryNode
            {
                Operator = op, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseComparison()
    {
        var node = ParseIn();
        while (Current.Type is ExprTokenType.Less or ExprTokenType.Greater
                            or ExprTokenType.LessEqual or ExprTokenType.GreaterEqual)
        {
            var op = Advance().Type switch
            {
                ExprTokenType.Less => BinaryOperator.LessThan,
                ExprTokenType.Greater => BinaryOperator.GreaterThan,
                ExprTokenType.LessEqual => BinaryOperator.LessOrEqual,
                ExprTokenType.GreaterEqual => BinaryOperator.GreaterOrEqual,
                _ => throw new InvalidOperationException(),
            };
            var right = ParseIn();
            node = new BinaryNode
            {
                Operator = op, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseIn()
    {
        var node = ParseAddition();
        if (Current.Type == ExprTokenType.In)
        {
            Advance();
            var right = ParseAddition();
            node = new BinaryNode
            {
                Operator = BinaryOperator.In, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseAddition()
    {
        var node = ParseMultiplication();
        while (Current.Type is ExprTokenType.Plus or ExprTokenType.Minus)
        {
            var op = Advance().Type == ExprTokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            var right = ParseMultiplication();
            node = new BinaryNode
            {
                Operator = op, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseMultiplication()
    {
        var node = ParseUnary();
        while (Current.Type is ExprTokenType.Star or ExprTokenType.Slash or ExprTokenType.Percent)
        {
            var op = Advance().Type switch
            {
                ExprTokenType.Star => BinaryOperator.Multiply,
                ExprTokenType.Slash => BinaryOperator.Divide,
                ExprTokenType.Percent => BinaryOperator.Modulo,
                _ => throw new InvalidOperationException(),
            };
            var right = ParseUnary();
            node = new BinaryNode
            {
                Operator = op, Left = node, Right = right,
                StartPos = node.StartPos, EndPos = right.EndPos,
            };
        }
        return node;
    }

    private ExprAstNode ParseUnary()
    {
        if (Current.Type is ExprTokenType.Bang)
        {
            var tok = Advance();
            var operand = ParseUnary();
            return new UnaryNode
            {
                Operator = UnaryOperator.Not, Operand = operand,
                StartPos = tok.Position, EndPos = operand.EndPos,
            };
        }
        if (Current.Type is ExprTokenType.Minus)
        {
            var tok = Advance();
            var operand = ParseUnary();
            return new UnaryNode
            {
                Operator = UnaryOperator.Negate, Operand = operand,
                StartPos = tok.Position, EndPos = operand.EndPos,
            };
        }
        if (Current.Type is ExprTokenType.Plus)
        {
            Advance();
            return ParseUnary();
        }
        return ParsePostfix();
    }

    private ExprAstNode ParsePostfix()
    {
        var node = ParsePrimary();
        while (true)
        {
            if (Current.Type == ExprTokenType.Dot)
            {
                Advance();
                var member = Expect(ExprTokenType.Identifier);
                node = new MemberAccessNode
                {
                    Object = node, MemberName = member.Lexeme,
                    StartPos = node.StartPos, EndPos = member.Position + member.Lexeme.Length,
                };
            }
            else if (Current.Type == ExprTokenType.LeftBracket)
            {
                Advance();
                var index = ParseTernary();
                var end = Expect(ExprTokenType.RightBracket);
                node = new IndexAccessNode
                {
                    Object = node, Index = index,
                    StartPos = node.StartPos, EndPos = end.Position + 1,
                };
            }
            else if (Current.Type == ExprTokenType.LeftParen && node is IdentifierNode id)
            {
                node = ParseFunctionCall(id);
            }
            else
            {
                break;
            }
        }
        return node;
    }

    private FunctionCallNode ParseFunctionCall(IdentifierNode id)
    {
        Advance(); // (
        var args = new List<ExprAstNode>();
        if (Current.Type != ExprTokenType.RightParen)
        {
            args.Add(ParseLambdaOrExpr());
            while (Current.Type == ExprTokenType.Comma)
            {
                Advance();
                args.Add(ParseLambdaOrExpr());
            }
        }
        var end = Expect(ExprTokenType.RightParen);
        return new FunctionCallNode
        {
            FunctionName = id.Name, Arguments = args,
            StartPos = id.StartPos, EndPos = end.Position + 1,
        };
    }

    private ExprAstNode ParseLambdaOrExpr()
    {
        if (Current.Type == ExprTokenType.LeftParen && LooksLikeLambda())
            return ParseLambda();
        if (Current.Type == ExprTokenType.Identifier && _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == ExprTokenType.Arrow)
            return ParseSingleParamLambda();
        return ParseTernary();
    }

    private bool LooksLikeLambda()
    {
        var saved = _pos;
        try
        {
            _pos++; // skip (
            if (Current.Type == ExprTokenType.RightParen) { _pos++; return Current.Type == ExprTokenType.Arrow; }
            if (Current.Type != ExprTokenType.Identifier) return false;
            _pos++;
            while (Current.Type == ExprTokenType.Comma) { _pos++; if (Current.Type != ExprTokenType.Identifier) return false; _pos++; }
            if (Current.Type != ExprTokenType.RightParen) return false;
            _pos++;
            return Current.Type == ExprTokenType.Arrow;
        }
        finally { _pos = saved; }
    }

    private LambdaNode ParseLambda()
    {
        var start = Current.Position;
        Expect(ExprTokenType.LeftParen);
        var parameters = new List<string>();
        if (Current.Type != ExprTokenType.RightParen)
        {
            parameters.Add(Expect(ExprTokenType.Identifier).Lexeme);
            while (Current.Type == ExprTokenType.Comma) { Advance(); parameters.Add(Expect(ExprTokenType.Identifier).Lexeme); }
        }
        Expect(ExprTokenType.RightParen);
        Expect(ExprTokenType.Arrow);
        var body = ParseTernary();
        return new LambdaNode { Parameters = parameters, Body = body, StartPos = start, EndPos = body.EndPos };
    }

    private LambdaNode ParseSingleParamLambda()
    {
        var start = Current.Position;
        var param = Expect(ExprTokenType.Identifier).Lexeme;
        Expect(ExprTokenType.Arrow);
        var body = ParseTernary();
        return new LambdaNode { Parameters = [param], Body = body, StartPos = start, EndPos = body.EndPos };
    }

    private ExprAstNode ParsePrimary()
    {
        var tok = Current;

        switch (tok.Type)
        {
            case ExprTokenType.IntegerLiteral:
                Advance();
                return new LiteralNode
                {
                    LiteralType = tok.LiteralValue is int ? ExprType.Integer : ExprType.Long,
                    Value = tok.LiteralValue, StartPos = tok.Position, EndPos = tok.Position + tok.Lexeme.Length,
                };

            case ExprTokenType.DoubleLiteral:
                Advance();
                return new LiteralNode
                {
                    LiteralType = ExprType.Double, Value = tok.LiteralValue,
                    StartPos = tok.Position, EndPos = tok.Position + tok.Lexeme.Length,
                };

            case ExprTokenType.StringLiteral:
                Advance();
                return new LiteralNode
                {
                    LiteralType = ExprType.String, Value = tok.LiteralValue,
                    StartPos = tok.Position, EndPos = tok.Position + tok.Lexeme.Length,
                };

            case ExprTokenType.True or ExprTokenType.False:
                Advance();
                return new LiteralNode
                {
                    LiteralType = ExprType.Boolean, Value = tok.Type == ExprTokenType.True,
                    StartPos = tok.Position, EndPos = tok.Position + tok.Lexeme.Length,
                };

            case ExprTokenType.Null:
                Advance();
                return new LiteralNode
                {
                    LiteralType = ExprType.Null, Value = null,
                    StartPos = tok.Position, EndPos = tok.Position + tok.Lexeme.Length,
                };

            case ExprTokenType.Identifier:
                Advance();
                return new IdentifierNode
                {
                    Name = tok.Lexeme, StartPos = tok.Position, EndPos = tok.Position + tok.Lexeme.Length,
                };

            case ExprTokenType.LeftParen:
                Advance();
                var expr = ParseTernary();
                Expect(ExprTokenType.RightParen);
                return expr;

            case ExprTokenType.LeftBracket:
                return ParseListLiteral();

            case ExprTokenType.LeftBrace:
                return ParseMapLiteral();

            default:
                throw new ExprParseException($"Unexpected token '{tok.Lexeme}' at position {tok.Position}");
        }
    }

    private ListLiteralNode ParseListLiteral()
    {
        var start = Current.Position;
        Advance(); // [
        var elements = new List<ExprAstNode>();
        if (Current.Type != ExprTokenType.RightBracket)
        {
            elements.Add(ParseTernary());
            while (Current.Type == ExprTokenType.Comma) { Advance(); elements.Add(ParseTernary()); }
        }
        var end = Expect(ExprTokenType.RightBracket);
        return new ListLiteralNode { Elements = elements, StartPos = start, EndPos = end.Position + 1 };
    }

    private MapLiteralNode ParseMapLiteral()
    {
        var start = Current.Position;
        Advance(); // {
        var entries = new List<MapEntry>();
        if (Current.Type != ExprTokenType.RightBrace)
        {
            entries.Add(ParseMapEntry());
            while (Current.Type == ExprTokenType.Comma) { Advance(); entries.Add(ParseMapEntry()); }
        }
        var end = Expect(ExprTokenType.RightBrace);
        return new MapLiteralNode { Entries = entries, StartPos = start, EndPos = end.Position + 1 };
    }

    private MapEntry ParseMapEntry()
    {
        var key = ParsePrimary();
        Expect(ExprTokenType.Colon);
        var value = ParseTernary();
        return new MapEntry { Key = key, Value = value };
    }
}
