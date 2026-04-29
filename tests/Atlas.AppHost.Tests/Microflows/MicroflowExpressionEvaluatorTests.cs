using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowExpressionEvaluatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Lexer_TokenizesVariablesMembersFunctionsOperatorsAndConditionals()
    {
        var lexer = new MicroflowExpressionLexer();

        var tokens = lexer.Tokenize("if $Order/TotalAmount > 100 and contains($Name, 'A') then now() else empty");
        var tokenKinds = tokens.Select(token => token.Kind).ToArray();

        Assert.Contains(MicroflowExpressionTokenKind.Keyword, tokenKinds);
        Assert.Contains(MicroflowExpressionTokenKind.DollarVariable, tokenKinds);
        Assert.Contains(MicroflowExpressionTokenKind.Slash, tokenKinds);
        Assert.Contains(MicroflowExpressionTokenKind.Identifier, tokenKinds);
        Assert.Contains(MicroflowExpressionTokenKind.NumberLiteral, tokenKinds);
        Assert.Contains(MicroflowExpressionTokenKind.StringLiteral, tokenKinds);
        Assert.Contains(MicroflowExpressionTokenKind.Operator, tokenKinds);
        Assert.Equal(MicroflowExpressionTokenKind.Eof, tokens[^1].Kind);
    }

    [Fact]
    public void Parser_ReturnsDiagnostics_ForInvalidExpressions()
    {
        var evaluator = new MicroflowExpressionEvaluator();

        Assert.Contains(evaluator.Parse("$").Diagnostics, item => item.Code == MicroflowExpressionDiagnosticCode.ParseError);
        Assert.Contains(evaluator.Parse("$Order/").Diagnostics, item => item.Code == MicroflowExpressionDiagnosticCode.ParseError);
        Assert.Contains(evaluator.Parse("'abc").Diagnostics, item => item.Code == MicroflowExpressionDiagnosticCode.ParseError);
    }

    [Theory]
    [InlineData("$Amount > 100", MicroflowExpressionAstKind.BinaryExpression)]
    [InlineData("$Order/Status = Sales.OrderStatus.New", MicroflowExpressionAstKind.BinaryExpression)]
    [InlineData("not empty($Order)", MicroflowExpressionAstKind.UnaryExpression)]
    [InlineData("if $Flag then 'yes' else 'no'", MicroflowExpressionAstKind.IfExpression)]
    [InlineData("$Amount + 10", MicroflowExpressionAstKind.BinaryExpression)]
    public void Parser_SupportsP0Subset(string raw, string expectedKind)
    {
        var evaluator = new MicroflowExpressionEvaluator();

        var parsed = evaluator.Parse(raw);

        Assert.DoesNotContain(parsed.Diagnostics, item => item.Severity == MicroflowExpressionDiagnosticSeverity.Error);
        Assert.Equal(expectedKind, parsed.Ast.Kind);
    }

    [Fact]
    public void TypeInference_ResolvesVariablesMembersEnumsAndConditionals()
    {
        var evaluator = new MicroflowExpressionEvaluator();
        var context = CreateContext();

        Assert.Equal(MicroflowExpressionTypeKind.String, evaluator.Infer(evaluator.Parse("'x'").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.Boolean, evaluator.Infer(evaluator.Parse("true").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.Integer, evaluator.Infer(evaluator.Parse("123").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.Decimal, evaluator.Infer(evaluator.Parse("123.45").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.Decimal, evaluator.Infer(evaluator.Parse("$Amount").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.Enumeration, evaluator.Infer(evaluator.Parse("$Order/Status").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.Boolean, evaluator.Infer(evaluator.Parse("$Order/TotalAmount > 100").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.Boolean, evaluator.Infer(evaluator.Parse("empty($Order)").Ast, context).InferredType.Kind);
        Assert.Equal(MicroflowExpressionTypeKind.String, evaluator.Infer(evaluator.Parse("if true then 'a' else 'b'").Ast, context).InferredType.Kind);
        Assert.Contains(evaluator.Infer(evaluator.Parse("$Order/Missing").Ast, context).Diagnostics, item => item.Code == MicroflowExpressionDiagnosticCode.MemberNotFound);
    }

    [Fact]
    public void Evaluator_ComputesP0SubsetAndStructuredErrors()
    {
        var evaluator = new MicroflowExpressionEvaluator();
        var context = CreateContext();

        Assert.Equal("120.5", evaluator.Evaluate("$Amount", context).ValuePreview);
        Assert.Equal("New", evaluator.Evaluate("$Order/Status", context).ValuePreview);
        Assert.True(evaluator.Evaluate("$Amount > 100", context).Value?.BoolValue);
        Assert.False(evaluator.Evaluate("$Flag and false", context).Value?.BoolValue);
        Assert.True(evaluator.Evaluate("$Flag or false", context).Value?.BoolValue);
        Assert.False(evaluator.Evaluate("not $Flag", context).Value?.BoolValue);
        Assert.True(evaluator.Evaluate("empty($EmptyText)", context).Value?.BoolValue);
        Assert.True(evaluator.Evaluate("empty($Items)", context).Value?.BoolValue);
        Assert.True(evaluator.Evaluate("empty($Nothing)", context).Value?.BoolValue);
        Assert.Equal("yes", evaluator.Evaluate("if $Flag then 'yes' else 'no'", context).ValuePreview);
        Assert.Equal("130.5", evaluator.Evaluate("$Amount + 10", context).ValuePreview);
        Assert.True(evaluator.Evaluate("$Order/Status = Sales.OrderStatus.New", context).Value?.BoolValue);

        Assert.False(evaluator.Evaluate("$Missing", context).Success);
        Assert.False(evaluator.Evaluate("$Flag + 1", context).Success);
        Assert.False(evaluator.Evaluate("$Amount / 0", context).Success);
        Assert.False(evaluator.Evaluate("unsupported($Amount)", context).Success);
    }

    [Theory]
    [InlineData("eval('$Amount')")]
    [InlineData("Function('$Amount')")]
    [InlineData("System.Reflection.Assembly.Load('x')")]
    [InlineData("select * from Orders")]
    public void Evaluator_RejectsNonWhitelistedFunctionsAndDynamicCode(string raw)
    {
        var evaluator = new MicroflowExpressionEvaluator();

        var result = evaluator.Evaluate(raw, CreateContext());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code is MicroflowExpressionDiagnosticCode.UnsupportedFunction
                or MicroflowExpressionDiagnosticCode.ParseError
                or MicroflowExpressionDiagnosticCode.TrailingToken
                or MicroflowExpressionDiagnosticCode.TypeMismatch);
    }

    [Fact]
    public void Evaluator_EnforcesExpectedType()
    {
        var evaluator = new MicroflowExpressionEvaluator();
        var context = CreateContext(MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean));

        var result = evaluator.Evaluate("'not boolean'", context);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, item => item.Code == MicroflowExpressionDiagnosticCode.ExpectedTypeMismatch);
    }

    [Fact]
    public void Formatter_NormalizesWhitespaceWithoutChangingStringLiteralContent()
    {
        var formatter = new MicroflowExpressionFormatter();

        var formatted = formatter.Format(" if   $Flag  then  'a  b'   else   'c' ");

        Assert.Equal("if $Flag then 'a  b' else 'c'", formatted);
    }

    [Fact]
    public void CompletionProvider_ReturnsVariablesFunctionsAndSystemVariables()
    {
        var provider = new MicroflowExpressionCompletionProvider();

        var completions = provider.Complete(CreateContext());
        var labels = completions.Select(item => item.Label).ToArray();

        Assert.Contains("$Amount", labels);
        Assert.Contains("$Order", labels);
        Assert.Contains("$latestError", labels);
        Assert.Contains("$latestHttpResponse", labels);
        Assert.Contains("contains", labels);
        Assert.Contains("length", labels);
    }

    [Fact]
    public void DiagnosticsProvider_ReturnsCodeSeverityAndRange()
    {
        var provider = new MicroflowExpressionDiagnosticsProvider();

        var diagnostics = provider.Diagnose("$Order/Missing", CreateContext());
        var diagnostic = Assert.Single(diagnostics, item => item.Code == MicroflowExpressionDiagnosticCode.MemberNotFound);

        Assert.Equal(MicroflowExpressionDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Start >= 0);
        Assert.True(diagnostic.End >= diagnostic.Start);
    }

    [Fact]
    public void PreviewService_EvaluatesWithSampleContextWithoutMutatingVariables()
    {
        var context = CreateContext();
        var before = context.VariableStore.CurrentVariables.Count;
        var preview = new MicroflowExpressionPreviewService(new MicroflowExpressionEvaluator());

        var result = preview.Preview("$Amount + 10", context);

        Assert.True(result.Success);
        Assert.Equal("130.5", result.ValuePreview);
        Assert.Equal(before, context.VariableStore.CurrentVariables.Count);
    }

    private static MicroflowExpressionEvaluationContext CreateContext(MicroflowExpressionType? expectedType = null)
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-expression-test",
            SchemaId = "schema-expression-test",
            StartNodeId = "start",
            Parameters =
            [
                Parameter("Amount", Type("decimal")),
                Parameter("Flag", Type("boolean")),
                Parameter("Order", Type("object", "Sales.Order")),
                Parameter("EmptyText", Type("string")),
                Parameter("Items", JsonSerializer.SerializeToElement(new { kind = "list", itemType = new { kind = "object", entityQualifiedName = "Sales.OrderLine" } }, JsonOptions)),
                Parameter("Nothing", Type("unknown"))
            ]
        };
        var input = new Dictionary<string, JsonElement>
        {
            ["Amount"] = JsonSerializer.SerializeToElement(120.5m, JsonOptions),
            ["Flag"] = JsonSerializer.SerializeToElement(true, JsonOptions),
            ["Order"] = JsonSerializer.SerializeToElement(new { Status = "New", TotalAmount = 120.5m }, JsonOptions),
            ["EmptyText"] = JsonSerializer.SerializeToElement(string.Empty, JsonOptions),
            ["Items"] = JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions),
            ["Nothing"] = JsonSerializer.SerializeToElement((string?)null, JsonOptions)
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-expression-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            input,
            securityContext: null,
            startedAt: DateTimeOffset.UtcNow);

        return new MicroflowExpressionEvaluationContext
        {
            RuntimeExecutionContext = runtime,
            VariableStore = runtime.VariableStore,
            MetadataCatalog = MicroflowSeedMetadataCatalog.Create(),
            ExpectedType = expectedType,
            Mode = MicroflowRuntimeExecutionMode.TestRun,
            Options = new MicroflowExpressionEvaluationOptions()
        };
    }

    private static MicroflowExecutionParameter Parameter(string name, JsonElement type)
        => new()
        {
            Id = name,
            Name = name,
            DataTypeJson = type,
            Required = false
        };

    private static JsonElement Type(string kind, string? entityQualifiedName = null)
        => entityQualifiedName is null
            ? JsonSerializer.SerializeToElement(new { kind }, JsonOptions)
            : JsonSerializer.SerializeToElement(new { kind, entityQualifiedName }, JsonOptions);
}
