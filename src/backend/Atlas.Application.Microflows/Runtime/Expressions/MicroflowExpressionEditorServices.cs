using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Expressions;

public sealed class MicroflowExpressionLexer : MicroflowExpressionTokenizer;

public sealed class MicroflowExpressionLanguageService
{
    public MicroflowExpressionLexer Lexer { get; } = new();

    public MicroflowExpressionParser Parser { get; } = new();
}

public sealed class MicroflowExpressionAstModel
{
    public MicroflowExpressionAstNode Parse(string raw) => new MicroflowExpressionParser().Parse(raw).Ast;
}

public sealed class MicroflowExpressionParserService
{
    private readonly MicroflowExpressionParser _parser = new();

    public MicroflowExpressionParseResult Parse(string raw) => _parser.Parse(raw);
}

public sealed class MicroflowExpressionTypeChecker
{
    private readonly MicroflowExpressionParser _parser = new();
    private readonly MicroflowExpressionTypeInference _inference = new();

    public MicroflowExpressionTypeInferenceResult Check(string raw, MicroflowExpressionEvaluationContext context)
    {
        var parsed = _parser.Parse(raw);
        var result = _inference.Infer(parsed.Ast, context);
        return result with { Diagnostics = result.Diagnostics.Concat(parsed.Diagnostics).Distinct().ToArray() };
    }
}

public sealed record MicroflowExpressionCompletionItem(string Label, string Kind, string InsertText, string? Detail = null);

public sealed class MicroflowExpressionCompletionProvider
{
    public IReadOnlyList<MicroflowExpressionCompletionItem> Complete(MicroflowExpressionEvaluationContext context)
    {
        var items = new List<MicroflowExpressionCompletionItem>
        {
            new("if then else", "keyword", "if  then  else ", "Conditional expression"),
            new("empty", "literal", "empty"),
            new("$latestError", "system", "$latestError"),
            new("$latestHttpResponse", "system", "$latestHttpResponse")
        };
        items.AddRange(context.VariableStore
            .Snapshot()
            .Variables
            .Select(variable => new MicroflowExpressionCompletionItem($"${variable.Name}", "variable", $"${variable.Name}", variable.ValuePreview)));
        items.AddRange(["length", "contains", "startsWith", "endsWith", "toString", "round"].Select(name => new MicroflowExpressionCompletionItem(name, "function", $"{name}()")));
        return items.OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public sealed class MicroflowExpressionDiagnosticsProvider
{
    private readonly MicroflowExpressionTypeChecker _typeChecker = new();

    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnose(string raw, MicroflowExpressionEvaluationContext context)
        => _typeChecker.Check(raw, context).Diagnostics;
}

public sealed class MicroflowExpressionFormatter
{
    public string Format(string raw)
        => string.Join(" ", new MicroflowExpressionTokenizer()
            .Tokenize(raw)
            .Where(token => token.Kind != MicroflowExpressionTokenKind.Eof)
            .Select(token => token.Text));
}

public sealed class MicroflowExpressionPreviewService
{
    private readonly IMicroflowExpressionEvaluator _evaluator;

    public MicroflowExpressionPreviewService(IMicroflowExpressionEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public MicroflowExpressionEvaluationResult Preview(string raw, MicroflowExpressionEvaluationContext context)
        => _evaluator.Evaluate(raw, context);
}

public static class MicroflowExpressionSampleContext
{
    public static MicroflowExpressionEvaluationContext Create(MicroflowExpressionType? expectedType = null)
    {
        var plan = new MicroflowExecutionPlan { Id = "expression-preview", SchemaId = "expression-preview" };
        var store = new MicroflowVariableStore(() => DateTimeOffset.UtcNow);
        store.Define(new MicroflowVariableDefinition
        {
            Name = "sample",
            DataTypeJson = JsonSerializer.Serialize(new { kind = "string" }),
            RawValueJson = JsonSerializer.Serialize("sample"),
            ValuePreview = "sample",
            SourceKind = MicroflowVariableSourceKind.Parameter
        });
        var runtime = RuntimeExecutionContext.Create(
            "expression-preview",
            plan,
            MicroflowRuntimeExecutionMode.PreviewRun,
            new Dictionary<string, JsonElement>(),
            null,
            DateTimeOffset.UtcNow,
            variableStore: store);
        return new MicroflowExpressionEvaluationContext
        {
            RuntimeExecutionContext = runtime,
            VariableStore = store,
            ExpectedType = expectedType,
            Options = new MicroflowExpressionEvaluationOptions
            {
                AllowUnknownVariables = false,
                AllowUnsupportedFunctions = false,
                StrictTypeCheck = true
            }
        };
    }
}
