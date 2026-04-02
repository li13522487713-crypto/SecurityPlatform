namespace Atlas.Infrastructure.LogicFlow.Expressions.Parsing;

public enum ExprTokenType
{
    // Literals
    IntegerLiteral,
    DoubleLiteral,
    StringLiteral,
    True,
    False,
    Null,

    // Identifiers & keywords
    Identifier,
    In,

    // Arithmetic
    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    // Comparison
    EqualEqual,
    BangEqual,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,

    // Logical
    AmpAmp,
    PipePipe,
    Bang,

    // Null coalesce
    QuestionQuestion,

    // Delimiters
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    LeftBrace,
    RightBrace,
    Comma,
    Dot,
    Colon,
    Question,
    Arrow,

    // Special
    Eof,
}

public readonly struct ExprToken
{
    public ExprTokenType Type { get; init; }
    public string Lexeme { get; init; }
    public int Position { get; init; }
    public object? LiteralValue { get; init; }

    public override string ToString() => $"[{Type} '{Lexeme}' @{Position}]";
}
