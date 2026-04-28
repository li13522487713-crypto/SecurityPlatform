using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Metadata;

namespace Atlas.Application.Microflows.Runtime.Expressions;

public static class MicroflowExpressionDiagnosticCode
{
    public const string ParseError = "RUNTIME_EXPR_PARSE_ERROR";
    public const string UnknownToken = "RUNTIME_EXPR_UNKNOWN_TOKEN";
    public const string TrailingToken = "RUNTIME_EXPR_TRAILING_TOKEN";
    public const string VariableNotFound = RuntimeErrorCode.RuntimeVariableNotFound;
    public const string VariableOutOfScope = "RUNTIME_VARIABLE_OUT_OF_SCOPE";
    public const string TypeMismatch = RuntimeErrorCode.RuntimeVariableTypeMismatch;
    public const string MetadataNotFound = RuntimeErrorCode.RuntimeMetadataNotFound;
    public const string MemberNotFound = "RUNTIME_EXPR_MEMBER_NOT_FOUND";
    public const string UnsupportedFunction = "RUNTIME_EXPR_UNSUPPORTED_FUNCTION";
    public const string DivideByZero = "RUNTIME_EXPR_DIVIDE_BY_ZERO";
    public const string ExpectedTypeMismatch = "RUNTIME_EXPR_EXPECTED_TYPE_MISMATCH";
    public const string MaxDepthExceeded = "RUNTIME_EXPR_MAX_DEPTH_EXCEEDED";
}

public static class MicroflowExpressionDiagnosticSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

public sealed record MicroflowExpressionDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = MicroflowExpressionDiagnosticCode.ParseError;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = MicroflowExpressionDiagnosticSeverity.Error;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("end")]
    public int End { get; init; }

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }

    [JsonPropertyName("variableName")]
    public string? VariableName { get; init; }

    [JsonPropertyName("memberPath")]
    public string? MemberPath { get; init; }
}

public static class MicroflowExpressionAstKind
{
    public const string Literal = "literal";
    public const string VariableReference = "variableReference";
    public const string MemberAccess = "memberAccess";
    public const string BinaryExpression = "binaryExpression";
    public const string UnaryExpression = "unaryExpression";
    public const string FunctionCall = "functionCall";
    public const string IfExpression = "ifExpression";
    public const string EnumerationValue = "enumerationValue";
    public const string Invalid = "invalid";
}

public static class MicroflowExpressionLiteralKind
{
    public const string String = "string";
    public const string Integer = "integer";
    public const string Decimal = "decimal";
    public const string Boolean = "boolean";
    public const string Null = "null";
    public const string Empty = "empty";
}

public abstract record MicroflowExpressionAstNode
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = MicroflowExpressionAstKind.Invalid;

    [JsonPropertyName("raw")]
    public string Raw { get; init; } = string.Empty;

    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("end")]
    public int End { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowExpressionDiagnostic>();
}

public sealed record MicroflowExpressionLiteralNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionLiteralNode()
    {
        Kind = MicroflowExpressionAstKind.Literal;
    }

    [JsonPropertyName("literalKind")]
    public string LiteralKind { get; init; } = MicroflowExpressionLiteralKind.Null;

    [JsonPropertyName("stringValue")]
    public string? StringValue { get; init; }

    [JsonPropertyName("boolValue")]
    public bool? BoolValue { get; init; }

    [JsonPropertyName("decimalValue")]
    public decimal? DecimalValue { get; init; }

    [JsonPropertyName("integerValue")]
    public long? IntegerValue { get; init; }
}

public sealed record MicroflowExpressionVariableReferenceNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionVariableReferenceNode()
    {
        Kind = MicroflowExpressionAstKind.VariableReference;
    }

    [JsonPropertyName("variableName")]
    public string VariableName { get; init; } = string.Empty;

    [JsonPropertyName("normalizedName")]
    public string NormalizedName { get; init; } = string.Empty;

    [JsonPropertyName("includesDollarPrefix")]
    public bool IncludesDollarPrefix { get; init; } = true;
}

public sealed record MicroflowExpressionMemberAccessNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionMemberAccessNode()
    {
        Kind = MicroflowExpressionAstKind.MemberAccess;
    }

    [JsonPropertyName("rootVariableName")]
    public string RootVariableName { get; init; } = string.Empty;

    [JsonPropertyName("memberPath")]
    public IReadOnlyList<string> MemberPath { get; init; } = Array.Empty<string>();

    [JsonPropertyName("rawPath")]
    public string RawPath { get; init; } = string.Empty;

    [JsonPropertyName("normalizedPath")]
    public string NormalizedPath { get; init; } = string.Empty;
}

public sealed record MicroflowExpressionBinaryNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionBinaryNode()
    {
        Kind = MicroflowExpressionAstKind.BinaryExpression;
    }

    [JsonPropertyName("operator")]
    public string Operator { get; init; } = string.Empty;

    [JsonPropertyName("left")]
    public MicroflowExpressionAstNode Left { get; init; } = new MicroflowExpressionInvalidNode();

    [JsonPropertyName("right")]
    public MicroflowExpressionAstNode Right { get; init; } = new MicroflowExpressionInvalidNode();
}

public sealed record MicroflowExpressionUnaryNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionUnaryNode()
    {
        Kind = MicroflowExpressionAstKind.UnaryExpression;
    }

    [JsonPropertyName("operator")]
    public string Operator { get; init; } = string.Empty;

    [JsonPropertyName("operand")]
    public MicroflowExpressionAstNode Operand { get; init; } = new MicroflowExpressionInvalidNode();
}

public sealed record MicroflowExpressionFunctionCallNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionFunctionCallNode()
    {
        Kind = MicroflowExpressionAstKind.FunctionCall;
    }

    [JsonPropertyName("functionName")]
    public string FunctionName { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public IReadOnlyList<MicroflowExpressionAstNode> Arguments { get; init; } = Array.Empty<MicroflowExpressionAstNode>();
}

public sealed record MicroflowExpressionIfNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionIfNode()
    {
        Kind = MicroflowExpressionAstKind.IfExpression;
    }

    [JsonPropertyName("condition")]
    public MicroflowExpressionAstNode Condition { get; init; } = new MicroflowExpressionInvalidNode();

    [JsonPropertyName("thenExpression")]
    public MicroflowExpressionAstNode ThenExpression { get; init; } = new MicroflowExpressionInvalidNode();

    [JsonPropertyName("elseExpression")]
    public MicroflowExpressionAstNode ElseExpression { get; init; } = new MicroflowExpressionInvalidNode();
}

public sealed record MicroflowExpressionEnumerationValueNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionEnumerationValueNode()
    {
        Kind = MicroflowExpressionAstKind.EnumerationValue;
    }

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("enumQualifiedName")]
    public string? EnumQualifiedName { get; init; }

    [JsonPropertyName("valueName")]
    public string ValueName { get; init; } = string.Empty;
}

public sealed record MicroflowExpressionInvalidNode : MicroflowExpressionAstNode
{
    public MicroflowExpressionInvalidNode()
    {
        Kind = MicroflowExpressionAstKind.Invalid;
    }
}

public static class MicroflowExpressionTypeKind
{
    public const string Unknown = "unknown";
    public const string Void = "void";
    public const string Boolean = "boolean";
    public const string String = "string";
    public const string Integer = "integer";
    public const string Long = "long";
    public const string Decimal = "decimal";
    public const string DateTime = "dateTime";
    public const string Object = "object";
    public const string List = "list";
    public const string Enumeration = "enumeration";
    public const string Json = "json";
    public const string Error = "error";
    public const string HttpResponse = "httpResponse";
}

public sealed record MicroflowExpressionType
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = MicroflowExpressionTypeKind.Unknown;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("enumerationQualifiedName")]
    public string? EnumerationQualifiedName { get; init; }

    [JsonPropertyName("itemType")]
    public MicroflowExpressionType? ItemType { get; init; }

    [JsonPropertyName("nullable")]
    public bool Nullable { get; init; } = true;

    [JsonPropertyName("rawDataTypeJson")]
    public string? RawDataTypeJson { get; init; }

    public static MicroflowExpressionType Unknown(string? raw = null) => new() { Kind = MicroflowExpressionTypeKind.Unknown, RawDataTypeJson = raw };
    public static MicroflowExpressionType Simple(string kind, bool nullable = true) => new() { Kind = kind, Nullable = nullable };
    public static MicroflowExpressionType Object(string? entityQualifiedName) => new() { Kind = MicroflowExpressionTypeKind.Object, EntityQualifiedName = entityQualifiedName };
    public static MicroflowExpressionType List(MicroflowExpressionType? itemType = null) => new() { Kind = MicroflowExpressionTypeKind.List, ItemType = itemType ?? Unknown() };
    public static MicroflowExpressionType Enumeration(string? enumerationQualifiedName) => new() { Kind = MicroflowExpressionTypeKind.Enumeration, EnumerationQualifiedName = enumerationQualifiedName };
}

public sealed record MicroflowExpressionTypeInferenceResult
{
    [JsonPropertyName("inferredType")]
    public MicroflowExpressionType InferredType { get; init; } = MicroflowExpressionType.Unknown();

    [JsonPropertyName("confidence")]
    public string Confidence { get; init; } = "low";

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowExpressionDiagnostic>();

    [JsonPropertyName("referencedVariables")]
    public IReadOnlyList<string> ReferencedVariables { get; init; } = Array.Empty<string>();

    [JsonPropertyName("referencedMembers")]
    public IReadOnlyList<string> ReferencedMembers { get; init; } = Array.Empty<string>();

    [JsonPropertyName("referencedMetadata")]
    public IReadOnlyList<string> ReferencedMetadata { get; init; } = Array.Empty<string>();
}

public sealed record MicroflowExpressionEvaluationOptions
{
    public bool AllowUnknownVariables { get; init; }
    public bool AllowUnsupportedFunctions { get; init; }
    public bool CoerceNumericTypes { get; init; } = true;
    public bool StrictTypeCheck { get; init; } = true;
    public int MaxStringLength { get; init; } = 500;
    public int MaxEvaluationDepth { get; init; } = 64;
}

public sealed record MicroflowExpressionEvaluationContext
{
    public RuntimeExecutionContext RuntimeExecutionContext { get; init; } = null!;
    public IMicroflowVariableStore VariableStore { get; init; } = null!;
    public MicroflowMetadataCatalogDto? MetadataCatalog { get; init; }
    public MicroflowMetadataResolutionContext? MetadataResolutionContext { get; init; }
    public IMicroflowMetadataResolver? MetadataResolver { get; init; }
    public string? CurrentObjectId { get; init; }
    public string? CurrentActionId { get; init; }
    public string? CurrentFlowId { get; init; }
    public string? CurrentCollectionId { get; init; }
    public MicroflowExpressionType? ExpectedType { get; init; }
    public string Mode { get; init; } = MicroflowRuntimeExecutionMode.TestRun;
    public MicroflowExpressionEvaluationOptions Options { get; init; } = new();
}

public sealed record MicroflowExpressionValue
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = MicroflowExpressionTypeKind.Unknown;

    [JsonPropertyName("type")]
    public MicroflowExpressionType Type { get; init; } = MicroflowExpressionType.Unknown();

    [JsonPropertyName("rawValueJson")]
    public string? RawValueJson { get; init; }

    [JsonPropertyName("valuePreview")]
    public string ValuePreview { get; init; } = "null";

    [JsonPropertyName("boolValue")]
    public bool? BoolValue { get; init; }

    [JsonPropertyName("stringValue")]
    public string? StringValue { get; init; }

    [JsonPropertyName("decimalValue")]
    public decimal? DecimalValue { get; init; }

    [JsonPropertyName("integerValue")]
    public long? IntegerValue { get; init; }

    [JsonPropertyName("objectValue")]
    public JsonElement? ObjectValue { get; init; }

    [JsonPropertyName("listValue")]
    public JsonElement? ListValue { get; init; }

    [JsonPropertyName("enumValue")]
    public string? EnumValue { get; init; }
}

public sealed record MicroflowExpressionRuntimeError
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = RuntimeErrorCode.RuntimeExpressionError;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowExpressionDiagnostic>();
}

public sealed record MicroflowExpressionEvaluationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("value")]
    public MicroflowExpressionValue? Value { get; init; }

    [JsonPropertyName("valueType")]
    public MicroflowExpressionType ValueType { get; init; } = MicroflowExpressionType.Unknown();

    [JsonPropertyName("rawValueJson")]
    public string? RawValueJson { get; init; }

    [JsonPropertyName("valuePreview")]
    public string ValuePreview { get; init; } = "null";

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowExpressionDiagnostic>();

    [JsonPropertyName("error")]
    public MicroflowExpressionRuntimeError? Error { get; init; }

    [JsonPropertyName("referencedVariables")]
    public IReadOnlyList<string> ReferencedVariables { get; init; } = Array.Empty<string>();

    [JsonPropertyName("referencedMembers")]
    public IReadOnlyList<string> ReferencedMembers { get; init; } = Array.Empty<string>();

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }
}

public sealed record MicroflowExpressionParseResult
{
    [JsonPropertyName("raw")]
    public string Raw { get; init; } = string.Empty;

    [JsonPropertyName("tokens")]
    public IReadOnlyList<MicroflowExpressionToken> Tokens { get; init; } = Array.Empty<MicroflowExpressionToken>();

    [JsonPropertyName("ast")]
    public MicroflowExpressionAstNode Ast { get; init; } = new MicroflowExpressionInvalidNode();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowExpressionDiagnostic>();
}

public interface IMicroflowExpressionEvaluator
{
    MicroflowExpressionParseResult Parse(string raw);

    MicroflowExpressionTypeInferenceResult Infer(
        MicroflowExpressionAstNode ast,
        MicroflowExpressionEvaluationContext context);

    MicroflowExpressionEvaluationResult Evaluate(
        string raw,
        MicroflowExpressionEvaluationContext context);
}
