using System.Globalization;
using System.Text;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Parsing;

/// <summary>
/// 词法分析器 —— 将表达式字符串拆分为 token 序列。
/// 支持：数字（整数/浮点）、字符串（单/双引号）、标识符、运算符、关键字。
/// </summary>
public sealed class ExprLexer
{
    private readonly string _source;
    private int _pos;

    public ExprLexer(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public List<ExprToken> Tokenize()
    {
        var tokens = new List<ExprToken>();
        while (_pos < _source.Length)
        {
            SkipWhitespace();
            if (_pos >= _source.Length) break;

            var token = ReadToken();
            tokens.Add(token);
        }
        tokens.Add(new ExprToken { Type = ExprTokenType.Eof, Lexeme = "", Position = _pos });
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos]))
            _pos++;
    }

    private ExprToken ReadToken()
    {
        var start = _pos;
        var ch = _source[_pos];

        if (char.IsDigit(ch)) return ReadNumber(start);
        if (ch is '\'' or '"') return ReadString(start);
        if (IsIdentStart(ch)) return ReadIdentifier(start);

        return ReadOperator(start);
    }

    private ExprToken ReadNumber(int start)
    {
        while (_pos < _source.Length && char.IsDigit(_source[_pos])) _pos++;

        bool isDouble = false;
        if (_pos < _source.Length && _source[_pos] == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1]))
        {
            isDouble = true;
            _pos++;
            while (_pos < _source.Length && char.IsDigit(_source[_pos])) _pos++;
        }

        var lexeme = _source[start.._pos];
        if (isDouble)
        {
            var value = double.Parse(lexeme, CultureInfo.InvariantCulture);
            return new ExprToken { Type = ExprTokenType.DoubleLiteral, Lexeme = lexeme, Position = start, LiteralValue = value };
        }
        else
        {
            var value = long.Parse(lexeme, CultureInfo.InvariantCulture);
            return new ExprToken
            {
                Type = ExprTokenType.IntegerLiteral,
                Lexeme = lexeme,
                Position = start,
                LiteralValue = value <= int.MaxValue && value >= int.MinValue ? (int)value : value,
            };
        }
    }

    private ExprToken ReadString(int start)
    {
        var quote = _source[_pos++];
        var sb = new StringBuilder();
        while (_pos < _source.Length && _source[_pos] != quote)
        {
            if (_source[_pos] == '\\' && _pos + 1 < _source.Length)
            {
                _pos++;
                sb.Append(_source[_pos] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    _ => _source[_pos],
                });
            }
            else
            {
                sb.Append(_source[_pos]);
            }
            _pos++;
        }
        if (_pos >= _source.Length) throw new ExprParseException($"Unterminated string at position {start}");
        _pos++; // closing quote
        return new ExprToken { Type = ExprTokenType.StringLiteral, Lexeme = _source[start.._pos], Position = start, LiteralValue = sb.ToString() };
    }

    private ExprToken ReadIdentifier(int start)
    {
        while (_pos < _source.Length && IsIdentPart(_source[_pos])) _pos++;
        var lexeme = _source[start.._pos];
        var type = lexeme switch
        {
            "true" => ExprTokenType.True,
            "false" => ExprTokenType.False,
            "null" => ExprTokenType.Null,
            "in" => ExprTokenType.In,
            "and" => ExprTokenType.AmpAmp,
            "or" => ExprTokenType.PipePipe,
            "not" => ExprTokenType.Bang,
            _ => ExprTokenType.Identifier,
        };
        object? literalValue = type switch
        {
            ExprTokenType.True => true,
            ExprTokenType.False => false,
            ExprTokenType.Null => null,
            _ => null,
        };
        return new ExprToken { Type = type, Lexeme = lexeme, Position = start, LiteralValue = literalValue };
    }

    private ExprToken ReadOperator(int start)
    {
        var ch = _source[_pos++];
        ExprTokenType Peek2(char expected, ExprTokenType match, ExprTokenType fallback)
        {
            if (_pos < _source.Length && _source[_pos] == expected) { _pos++; return match; }
            return fallback;
        }

        ExprTokenType PeekOrThrow(char expected, ExprTokenType match, string errorMessage)
        {
            if (_pos < _source.Length && _source[_pos] == expected) { _pos++; return match; }
            throw new ExprParseException(errorMessage);
        }

        var type = ch switch
        {
            '+' => ExprTokenType.Plus,
            '-' => ExprTokenType.Minus,
            '*' => ExprTokenType.Star,
            '/' => ExprTokenType.Slash,
            '%' => ExprTokenType.Percent,
            '(' => ExprTokenType.LeftParen,
            ')' => ExprTokenType.RightParen,
            '[' => ExprTokenType.LeftBracket,
            ']' => ExprTokenType.RightBracket,
            '{' => ExprTokenType.LeftBrace,
            '}' => ExprTokenType.RightBrace,
            ',' => ExprTokenType.Comma,
            '.' => ExprTokenType.Dot,
            ':' => ExprTokenType.Colon,
            '=' => PeekOrThrow('=', ExprTokenType.EqualEqual, $"Unexpected '=' at {start}; use '==' for equality"),
            '!' => Peek2('=', ExprTokenType.BangEqual, ExprTokenType.Bang),
            '<' => Peek2('=', ExprTokenType.LessEqual, ExprTokenType.Less),
            '>' => Peek2('=', ExprTokenType.GreaterEqual, ExprTokenType.Greater),
            '&' => PeekOrThrow('&', ExprTokenType.AmpAmp, $"Unexpected '&' at {start}; use '&&'"),
            '|' => PeekOrThrow('|', ExprTokenType.PipePipe, $"Unexpected '|' at {start}; use '||'"),
            '?' => Peek2('?', ExprTokenType.QuestionQuestion, ExprTokenType.Question),
            _ => throw new ExprParseException($"Unexpected character '{ch}' at position {start}"),
        };

        if (type == ExprTokenType.EqualEqual && _source[start] == '=')
        {
            // already handled
        }

        // => arrow
        if (type == ExprTokenType.EqualEqual)
        {
            // not arrow
        }
        else if (ch == '=' && _pos < _source.Length && _source[_pos] == '>')
        {
            _pos++;
            type = ExprTokenType.Arrow;
        }

        return new ExprToken { Type = type, Lexeme = _source[start.._pos], Position = start };
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}

public sealed class ExprParseException : Exception
{
    public ExprParseException(string message) : base(message) { }
}
