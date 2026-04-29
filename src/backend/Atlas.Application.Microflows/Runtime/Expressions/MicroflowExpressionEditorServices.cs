using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Expressions;

public sealed class MicroflowExpressionLexer
{
    private readonly MicroflowExpressionTokenizer _tokenizer = new();

    public IReadOnlyList<MicroflowExpressionToken> Tokenize(string? raw)
        => _tokenizer.Tokenize(raw);
}

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
            .CurrentVariables
            .Values
            .Select(variable => new MicroflowExpressionCompletionItem($"${variable.Name}", "variable", $"${variable.Name}", variable.ValuePreview)));
        items.AddRange(new[] { "length", "contains", "startsWith", "endsWith", "toString", "round" }
            .Select(name => new MicroflowExpressionCompletionItem(name, "function", $"{name}()")));
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
            .Select(static token => token.Kind == MicroflowExpressionTokenKind.StringLiteral
                ? $"'{token.Text}'"
                : token.Text));
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

public sealed record MicroflowExpressionEditorRequest
{
    public string Expression { get; init; } = string.Empty;
}

public sealed record MicroflowExpressionEditorResponse
{
    public string Expression { get; init; } = string.Empty;

    public string? FormattedExpression { get; init; }

    public MicroflowExpressionAstNode? Ast { get; init; }

    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowExpressionDiagnostic>();

    public IReadOnlyList<MicroflowExpressionCompletionItem> Completions { get; init; } = Array.Empty<MicroflowExpressionCompletionItem>();

    public string? Type { get; init; }

    public string? PreviewValue { get; init; }
}

public sealed class MicroflowExpressionEditorService
{
    private readonly MicroflowExpressionParserService _parser = new();
    private readonly MicroflowExpressionTypeChecker _typeChecker = new();
    private readonly MicroflowExpressionFormatter _formatter = new();
    private readonly MicroflowExpressionCompletionProvider _completionProvider = new();
    private readonly MicroflowExpressionPreviewService _previewService;

    public MicroflowExpressionEditorService(IMicroflowExpressionEvaluator evaluator)
    {
        _previewService = new MicroflowExpressionPreviewService(evaluator);
    }

    public MicroflowExpressionEditorResponse Parse(MicroflowExpressionEditorRequest request)
    {
        var parsed = _parser.Parse(request.Expression);
        return new MicroflowExpressionEditorResponse
        {
            Expression = request.Expression,
            Ast = parsed.Ast,
            Diagnostics = parsed.Diagnostics
        };
    }

    public MicroflowExpressionEditorResponse Validate(MicroflowExpressionEditorRequest request)
    {
        var context = MicroflowExpressionSampleContext.Create();
        var checkedResult = _typeChecker.Check(request.Expression, context);
        return new MicroflowExpressionEditorResponse
        {
            Expression = request.Expression,
            Diagnostics = checkedResult.Diagnostics,
            Type = checkedResult.InferredType.Kind
        };
    }

    public MicroflowExpressionEditorResponse InferType(MicroflowExpressionEditorRequest request)
        => Validate(request);

    public MicroflowExpressionEditorResponse Completions(MicroflowExpressionEditorRequest request)
        => new()
        {
            Expression = request.Expression,
            Completions = _completionProvider.Complete(MicroflowExpressionSampleContext.Create())
        };

    public MicroflowExpressionEditorResponse Format(MicroflowExpressionEditorRequest request)
        => new()
        {
            Expression = request.Expression,
            FormattedExpression = _formatter.Format(request.Expression)
        };

    public MicroflowExpressionEditorResponse Preview(MicroflowExpressionEditorRequest request)
    {
        var result = _previewService.Preview(request.Expression, MicroflowExpressionSampleContext.Create());
        return new MicroflowExpressionEditorResponse
        {
            Expression = request.Expression,
            Diagnostics = result.Diagnostics,
            Type = result.ValueType.Kind,
            PreviewValue = result.ValuePreview
        };
    }
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
