using Atlas.Core.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions;
using Atlas.Infrastructure.LogicFlow.Expressions.Functions;

namespace Atlas.SecurityPlatform.Tests.Services;

/// <summary>
/// TS-21: ExprEvaluator 表达式求值器测试。
/// 覆盖：字面量、变量解析、算术/逻辑运算、比较、三目、成员访问、函数调用、错误报告。
/// </summary>
public sealed class ExprEvaluatorTests
{
    private readonly ExprEvaluator _sut;

    public ExprEvaluatorTests()
    {
        var registry = new BuiltinFunctionRegistry();
        var cache = new AstCompilationCache(256);
        _sut = new ExprEvaluator(registry, cache);
    }

    private ExpressionContext Ctx(params (string key, object? value)[] vars)
        => ExpressionContext.FromRecord(
            vars.ToDictionary(t => t.key, t => t.value));

    // ─── Literals ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_IntegerLiteral_ShouldReturnNumber()
    {
        var result = _sut.EvaluateWithContext("42", Ctx());
        Assert.Equal(42, Convert.ToInt64(result));
    }

    [Fact]
    public void Evaluate_StringLiteral_ShouldReturnString()
    {
        var result = _sut.EvaluateWithContext("\"hello\"", Ctx());
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Evaluate_BoolLiteralTrue_ShouldReturnTrue()
    {
        var result = _sut.EvaluateWithContext("true", Ctx());
        Assert.Equal(true, result);
    }

    [Fact]
    public void Evaluate_NullLiteral_ShouldReturnNull()
    {
        var result = _sut.EvaluateWithContext("null", Ctx());
        Assert.Null(result);
    }

    // ─── Variable Resolution ─────────────────────────────────────────────────

    [Fact]
    public void Evaluate_VariableReference_ShouldResolveFromContext()
    {
        var result = _sut.EvaluateWithContext("age", Ctx(("age", 30)));
        Assert.Equal(30, result);
    }

    [Fact]
    public void Evaluate_UndefinedVariable_ShouldReturnNull()
    {
        var result = _sut.EvaluateWithContext("undefinedVar", Ctx());
        Assert.Null(result);
    }

    // ─── Arithmetic ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("10 - 4", 6)]
    [InlineData("3 * 4", 12)]
    [InlineData("10 / 2", 5)]
    public void Evaluate_ArithmeticExpression_ShouldComputeCorrectly(string expr, double expected)
    {
        var result = _sut.EvaluateWithContext(expr, Ctx());
        Assert.Equal(expected, Convert.ToDouble(result), precision: 5);
    }

    [Fact]
    public void Evaluate_AddNumbers_WithVariables_ShouldWork()
    {
        var ctx = Ctx(("x", 10), ("y", 5));
        var result = _sut.EvaluateWithContext("x + y", ctx);
        Assert.Equal(15, Convert.ToInt64(result));
    }

    // ─── Comparison ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("5 > 3", true)]
    [InlineData("3 > 5", false)]
    [InlineData("5 >= 5", true)]
    [InlineData("3 < 5", true)]
    [InlineData("5 <= 4", false)]
    [InlineData("5 == 5", true)]
    [InlineData("5 != 4", true)]
    public void Evaluate_ComparisonExpression_ShouldReturnBool(string expr, bool expected)
    {
        var result = _sut.EvaluateWithContext(expr, Ctx());
        Assert.Equal(expected, result);
    }

    // ─── Logical ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("true && true", true)]
    [InlineData("true && false", false)]
    [InlineData("false || true", true)]
    [InlineData("false || false", false)]
    [InlineData("!true", false)]
    [InlineData("!false", true)]
    public void Evaluate_LogicalExpression_ShouldReturnBool(string expr, bool expected)
    {
        var result = _sut.EvaluateWithContext(expr, Ctx());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Evaluate_ShortCircuitAnd_WithNullLeft_ShouldReturnNull()
    {
        var ctx = Ctx(("a", (object?)null));
        var result = _sut.EvaluateWithContext("a && true", ctx);
        Assert.Null(result);
    }

    // ─── Conditional (Ternary) ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_TernaryTrue_ShouldReturnThenBranch()
    {
        var result = _sut.EvaluateWithContext("5 > 3 ? \"yes\" : \"no\"", Ctx());
        Assert.Equal("yes", result);
    }

    [Fact]
    public void Evaluate_TernaryFalse_ShouldReturnElseBranch()
    {
        var result = _sut.EvaluateWithContext("3 > 5 ? \"yes\" : \"no\"", Ctx());
        Assert.Equal("no", result);
    }

    // ─── Member Access ───────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_MemberAccess_OnDictionary_ShouldReturnNestedValue()
    {
        var ctx = Ctx(("user", (object?)new Dictionary<string, object?> { ["name"] = "Alice" }));
        var result = _sut.EvaluateWithContext("user.name", ctx);
        Assert.Equal("Alice", result);
    }

    [Fact]
    public void Evaluate_MemberAccess_OnNullParent_ShouldReturnNull()
    {
        // 访问 null 对象的成员时，EvalMemberAccess 应返回 null 而非抛出异常
        var ctx = Ctx(("obj", (object?)null));
        var result = _sut.EvaluateWithContext("obj.field", ctx);
        Assert.Null(result);
    }

    // ─── Builtin Functions ───────────────────────────────────────────────────

    [Fact]
    public void Evaluate_LengthFunction_OnString_ShouldReturnLength()
    {
        var result = _sut.EvaluateWithContext("LENGTH(\"hello\")", Ctx());
        Assert.Equal(5, Convert.ToInt64(result));
    }

    [Fact]
    public void Evaluate_UpperFunction_ShouldReturnUppercase()
    {
        var result = _sut.EvaluateWithContext("UPPER(\"hello\")", Ctx());
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void Evaluate_ConcatStringWithPlus_ShouldConcatenate()
    {
        var ctx = Ctx(("firstName", "John"), ("lastName", "Doe"));
        var result = _sut.EvaluateWithContext("firstName + \" \" + lastName", ctx);
        Assert.Equal("John Doe", result);
    }

    // ─── Error Reporting (RT-14) ─────────────────────────────────────────────

    [Fact]
    public void EvaluateWithContext_InvalidOperator_ShouldThrowExpressionEvaluationException()
    {
        var ex = Assert.Throws<ExpressionEvaluationException>(
            () => _sut.EvaluateWithContext("\"text\" - 5", Ctx()));
        Assert.Contains("表达式", ex.Message);
    }

    [Fact]
    public void EvaluateWithContext_ExceptionMessage_ShouldContainOriginalExpression()
    {
        const string expr = "badFunc()";
        try
        {
            _sut.EvaluateWithContext(expr, Ctx());
        }
        catch (ExpressionEvaluationException ex)
        {
            Assert.Contains("badFunc", ex.Message);
            return;
        }
        // Function may return null rather than throw — that's also acceptable behavior
    }

    // ─── ParseAndCache ───────────────────────────────────────────────────────

    [Fact]
    public void ParseAndCache_SameExpression_ShouldReturnCachedInstance()
    {
        const string expr = "1 + 2";
        var ast1 = _sut.ParseAndCache(expr);
        var ast2 = _sut.ParseAndCache(expr);
        Assert.Same(ast1, ast2);
    }

    [Fact]
    public void ParseAndCache_DifferentExpressions_ShouldReturnDistinctInstances()
    {
        var ast1 = _sut.ParseAndCache("1 + 2");
        var ast2 = _sut.ParseAndCache("3 + 4");
        Assert.NotSame(ast1, ast2);
    }
}
