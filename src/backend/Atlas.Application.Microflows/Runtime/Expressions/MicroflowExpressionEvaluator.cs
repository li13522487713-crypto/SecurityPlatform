using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Metadata;

namespace Atlas.Application.Microflows.Runtime.Expressions;

public sealed class MicroflowExpressionEvaluator : IMicroflowExpressionEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MicroflowExpressionParser _parser = new();
    private readonly MicroflowExpressionTypeInference _typeInference = new();

    public MicroflowExpressionParseResult Parse(string raw)
        => _parser.Parse(raw);

    public MicroflowExpressionTypeInferenceResult Infer(
        MicroflowExpressionAstNode ast,
        MicroflowExpressionEvaluationContext context)
        => _typeInference.Infer(ast, context);

    public MicroflowExpressionEvaluationResult Evaluate(
        string raw,
        MicroflowExpressionEvaluationContext context)
    {
        var started = Stopwatch.StartNew();
        var parse = Parse(raw);
        var parseErrors = parse.Diagnostics
            .Where(item => string.Equals(item.Severity, MicroflowExpressionDiagnosticSeverity.Error, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (parseErrors.Length > 0 || parse.Ast.Kind == MicroflowExpressionAstKind.Invalid)
        {
            return Failed(RuntimeErrorCode.RuntimeExpressionError, "表达式解析失败。", started, parse.Diagnostics, null);
        }

        var inference = Infer(parse.Ast, context);
        var state = new EvaluationState(context, inference.Diagnostics.ToList());
        MicroflowExpressionValue? value;
        try
        {
            value = EvaluateNode(parse.Ast, state, depth: 0);
        }
        catch (MicroflowExpressionEvaluationException ex)
        {
            state.Diagnostics.AddRange(ex.Diagnostics);
            return Failed(ex.Code, ex.Message, started, state.Diagnostics, inference);
        }

        CheckExpectedType(value, context, state, parse.Ast);
        var errors = state.Diagnostics
            .Where(item => string.Equals(item.Severity, MicroflowExpressionDiagnosticSeverity.Error, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (errors.Length > 0)
        {
            return Failed(RuntimeErrorCode.RuntimeExpressionError, "表达式求值失败。", started, state.Diagnostics, inference);
        }

        started.Stop();
        return new MicroflowExpressionEvaluationResult
        {
            Success = true,
            Value = value,
            ValueType = value.Type,
            RawValueJson = value.RawValueJson,
            ValuePreview = value.ValuePreview,
            Diagnostics = state.Diagnostics,
            ReferencedVariables = inference.ReferencedVariables,
            ReferencedMembers = inference.ReferencedMembers,
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }

    private MicroflowExpressionValue EvaluateNode(
        MicroflowExpressionAstNode node,
        EvaluationState state,
        int depth)
    {
        if (depth > state.Context.Options.MaxEvaluationDepth)
        {
            Throw(
                RuntimeErrorCode.RuntimeExpressionError,
                "表达式求值深度超过限制。",
                Diagnostic(MicroflowExpressionDiagnosticCode.MaxDepthExceeded, MicroflowExpressionDiagnosticSeverity.Error, "表达式求值深度超过限制。", node.Start, node.End));
        }

        return node switch
        {
            MicroflowExpressionLiteralNode literal => EvaluateLiteral(literal, state),
            MicroflowExpressionVariableReferenceNode variable => EvaluateVariable(variable, state),
            MicroflowExpressionMemberAccessNode member => EvaluateMember(member, state, depth),
            MicroflowExpressionUnaryNode unary => EvaluateUnary(unary, state, depth),
            MicroflowExpressionBinaryNode binary => EvaluateBinary(binary, state, depth),
            MicroflowExpressionFunctionCallNode function => EvaluateFunction(function, state, depth),
            MicroflowExpressionIfNode ifNode => EvaluateIf(ifNode, state, depth),
            MicroflowExpressionEnumerationValueNode enumeration => EvaluateEnumeration(enumeration, state),
            _ => ThrowValue(RuntimeErrorCode.RuntimeExpressionError, "表达式 AST 无效。", Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, MicroflowExpressionDiagnosticSeverity.Error, "表达式 AST 无效。", node.Start, node.End))
        };
    }

    private static MicroflowExpressionValue EvaluateLiteral(MicroflowExpressionLiteralNode literal, EvaluationState state)
    {
        return literal.LiteralKind switch
        {
            MicroflowExpressionLiteralKind.String => Value(
                MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String, nullable: false),
                JsonSerializer.Serialize(literal.StringValue ?? string.Empty, JsonOptions),
                Trim(literal.StringValue ?? string.Empty, state.Context.Options.MaxStringLength),
                stringValue: literal.StringValue ?? string.Empty),
            MicroflowExpressionLiteralKind.Boolean => Value(
                MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean, nullable: false),
                literal.BoolValue == true ? "true" : "false",
                literal.BoolValue == true ? "true" : "false",
                boolValue: literal.BoolValue == true),
            MicroflowExpressionLiteralKind.Integer => Value(
                MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Integer, nullable: false),
                literal.IntegerValue.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                literal.IntegerValue.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                decimalValue: literal.IntegerValue.GetValueOrDefault(),
                integerValue: literal.IntegerValue.GetValueOrDefault()),
            MicroflowExpressionLiteralKind.Decimal => Value(
                MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal, nullable: false),
                literal.DecimalValue.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                literal.DecimalValue.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                decimalValue: literal.DecimalValue.GetValueOrDefault()),
            MicroflowExpressionLiteralKind.Null or MicroflowExpressionLiteralKind.Empty => NullValue(),
            _ => NullValue()
        };
    }

    private MicroflowExpressionValue EvaluateVariable(MicroflowExpressionVariableReferenceNode variable, EvaluationState state)
    {
        state.ReferencedVariables.Add(variable.NormalizedName);
        if (!TryGetVariable(state.Context.VariableStore, variable.NormalizedName, out var runtimeValue) || runtimeValue is null)
        {
            if (state.Context.Options.AllowUnknownVariables)
            {
                state.Diagnostics.Add(Diagnostic(MicroflowExpressionDiagnosticCode.VariableNotFound, MicroflowExpressionDiagnosticSeverity.Warning, $"变量不存在：${variable.NormalizedName}", variable.Start, variable.End, variableName: variable.NormalizedName));
                return UnknownValue();
            }

            Throw(
                RuntimeErrorCode.RuntimeVariableNotFound,
                $"变量不存在：${variable.NormalizedName}",
                Diagnostic(MicroflowExpressionDiagnosticCode.VariableNotFound, MicroflowExpressionDiagnosticSeverity.Error, $"变量不存在：${variable.NormalizedName}", variable.Start, variable.End, variableName: variable.NormalizedName));
        }

        return FromRuntimeVariable(runtimeValue!, state.Context.Options.MaxStringLength);
    }

    private MicroflowExpressionValue EvaluateMember(MicroflowExpressionMemberAccessNode member, EvaluationState state, int depth)
    {
        state.ReferencedVariables.Add(member.RootVariableName);
        state.ReferencedMembers.Add($"{member.RootVariableName}/{member.RawPath}");
        if (!TryGetVariable(state.Context.VariableStore, member.RootVariableName, out var runtimeValue) || runtimeValue is null)
        {
            Throw(
                RuntimeErrorCode.RuntimeVariableNotFound,
                $"变量不存在：${member.RootVariableName}",
                Diagnostic(MicroflowExpressionDiagnosticCode.VariableNotFound, MicroflowExpressionDiagnosticSeverity.Error, $"变量不存在：${member.RootVariableName}", member.Start, member.End, variableName: member.RootVariableName));
        }

        runtimeValue ??= UnknownRuntimeValue(member.RootVariableName);
        var rootType = MicroflowExpressionTypeHelper.FromDataTypeJson(runtimeValue.DataTypeJson);
        var resolvedPath = ResolveMemberPathWithMetadata(member, state, rootType);
        if (resolvedPath is { Found: false })
        {
            Throw(
                RuntimeErrorCode.RuntimeExpressionError,
                $"成员路径无法从 metadata 确认：{member.RawPath}",
                resolvedPath.Diagnostics.Select(diagnostic => Diagnostic(
                    MapDiagnosticCode(diagnostic.Code),
                    diagnostic.Severity,
                    diagnostic.Message,
                    member.Start,
                    member.End,
                    memberPath: member.RawPath)).ToArray());
        }

        if (string.IsNullOrWhiteSpace(runtimeValue.RawValueJson) || runtimeValue.RawValueJson == "null")
        {
            return NullValue();
        }

        JsonElement current = default;
        try
        {
            using var document = JsonDocument.Parse(runtimeValue.RawValueJson);
            current = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            Throw(
                RuntimeErrorCode.RuntimeExpressionError,
                $"变量 ${member.RootVariableName} 的 rawValueJson 不是可读取 JSON。",
                Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, $"变量 ${member.RootVariableName} 的 rawValueJson 不是可读取 JSON。", member.Start, member.End, memberPath: member.RawPath));
        }

        foreach (var segment in member.MemberPath)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                Throw(
                    RuntimeErrorCode.RuntimeExpressionError,
                    $"成员访问目标不是对象：{segment}",
                    Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, $"成员访问目标不是对象：{segment}", member.Start, member.End, memberPath: member.RawPath));
            }

            if (!TryGetProperty(current, segment, out var next))
            {
                if (MetadataMemberExists(state.Context.MetadataCatalog, rootType, segment))
                {
                    state.Diagnostics.Add(Diagnostic(MicroflowExpressionDiagnosticCode.MemberNotFound, MicroflowExpressionDiagnosticSeverity.Warning, $"对象值缺少成员 {segment}，按 null 返回。", member.Start, member.End, memberPath: member.RawPath));
                    return NullValue();
                }

                Throw(
                    RuntimeErrorCode.RuntimeExpressionError,
                    $"成员不存在：{segment}",
                    Diagnostic(MicroflowExpressionDiagnosticCode.MemberNotFound, MicroflowExpressionDiagnosticSeverity.Error, $"成员不存在：{segment}", member.Start, member.End, memberPath: member.RawPath));
            }

            current = next.Clone();
        }

        var inferred = resolvedPath is { Found: true }
            ? FromResolvedDataType(resolvedPath.FinalType)
            : new MicroflowExpressionTypeInference().Infer(member, state.Context).InferredType;
        return FromJsonElement(current, inferred, state.Context.Options.MaxStringLength);
    }

    private MicroflowExpressionValue EvaluateUnary(MicroflowExpressionUnaryNode unary, EvaluationState state, int depth)
    {
        var operand = EvaluateNode(unary.Operand, state, depth + 1);
        if (string.Equals(unary.Operator, "not", StringComparison.OrdinalIgnoreCase))
        {
            if (operand.BoolValue is null)
            {
                Throw(
                    RuntimeErrorCode.RuntimeVariableTypeMismatch,
                    "not 操作数必须是 boolean。",
                    Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "not 操作数必须是 boolean。", unary.Start, unary.End));
            }

            return Bool(!operand.BoolValue.GetValueOrDefault());
        }

        if (unary.Operator == "-")
        {
            var number = RequireNumber(operand, unary.Start, unary.End);
            return DecimalValue(-number);
        }

        return UnknownValue();
    }

    private MicroflowExpressionValue EvaluateBinary(MicroflowExpressionBinaryNode binary, EvaluationState state, int depth)
    {
        if (binary.Operator == "and")
        {
            var left = RequireBool(EvaluateNode(binary.Left, state, depth + 1), binary.Left.Start, binary.Left.End);
            return !left ? Bool(false) : Bool(RequireBool(EvaluateNode(binary.Right, state, depth + 1), binary.Right.Start, binary.Right.End));
        }

        if (binary.Operator == "or")
        {
            var left = RequireBool(EvaluateNode(binary.Left, state, depth + 1), binary.Left.Start, binary.Left.End);
            return left ? Bool(true) : Bool(RequireBool(EvaluateNode(binary.Right, state, depth + 1), binary.Right.Start, binary.Right.End));
        }

        var leftValue = EvaluateNode(binary.Left, state, depth + 1);
        var rightValue = EvaluateNode(binary.Right, state, depth + 1);

        if (binary.Operator is "=" or "!=")
        {
            var equals = ValueEquals(leftValue, rightValue);
            return Bool(binary.Operator == "=" ? equals : !equals);
        }

        if (binary.Operator is ">" or "<" or ">=" or "<=")
        {
            var leftNumber = RequireNumber(leftValue, binary.Left.Start, binary.Left.End);
            var rightNumber = RequireNumber(rightValue, binary.Right.Start, binary.Right.End);
            return Bool(binary.Operator switch
            {
                ">" => leftNumber > rightNumber,
                "<" => leftNumber < rightNumber,
                ">=" => leftNumber >= rightNumber,
                "<=" => leftNumber <= rightNumber,
                _ => false
            });
        }

        if (binary.Operator == "+")
        {
            if (leftValue.Type.Kind == MicroflowExpressionTypeKind.String || rightValue.Type.Kind == MicroflowExpressionTypeKind.String)
            {
                if (leftValue.Type.Kind != MicroflowExpressionTypeKind.String || rightValue.Type.Kind != MicroflowExpressionTypeKind.String)
                {
                    state.Diagnostics.Add(Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Warning, "string + non-string 已按 valuePreview 拼接。", binary.Start, binary.End));
                }

                var concatenated = (leftValue.StringValue ?? leftValue.ValuePreview) + (rightValue.StringValue ?? rightValue.ValuePreview);
                return Value(
                    MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String, nullable: false),
                    JsonSerializer.Serialize(concatenated, JsonOptions),
                    Trim(concatenated, state.Context.Options.MaxStringLength),
                    stringValue: concatenated);
            }

            return DecimalValue(RequireNumber(leftValue, binary.Left.Start, binary.Left.End) + RequireNumber(rightValue, binary.Right.Start, binary.Right.End));
        }

        if (binary.Operator == "-")
        {
            return DecimalValue(RequireNumber(leftValue, binary.Left.Start, binary.Left.End) - RequireNumber(rightValue, binary.Right.Start, binary.Right.End));
        }

        if (binary.Operator == "*")
        {
            return DecimalValue(RequireNumber(leftValue, binary.Left.Start, binary.Left.End) * RequireNumber(rightValue, binary.Right.Start, binary.Right.End));
        }

        if (binary.Operator == "/")
        {
            var divisor = RequireNumber(rightValue, binary.Right.Start, binary.Right.End);
            if (divisor == 0)
            {
                Throw(
                    RuntimeErrorCode.RuntimeExpressionError,
                    "表达式除零错误。",
                    Diagnostic(MicroflowExpressionDiagnosticCode.DivideByZero, MicroflowExpressionDiagnosticSeverity.Error, "表达式除零错误。", binary.Start, binary.End));
            }

            return DecimalValue(RequireNumber(leftValue, binary.Left.Start, binary.Left.End) / divisor);
        }

        Throw(RuntimeErrorCode.RuntimeExpressionError, $"不支持的二元操作符：{binary.Operator}", Diagnostic(MicroflowExpressionDiagnosticCode.ParseError, MicroflowExpressionDiagnosticSeverity.Error, $"不支持的二元操作符：{binary.Operator}", binary.Start, binary.End));
        return UnknownValue();
    }

    private MicroflowExpressionValue EvaluateFunction(MicroflowExpressionFunctionCallNode function, EvaluationState state, int depth)
    {
        if (string.Equals(function.FunctionName, "empty", StringComparison.OrdinalIgnoreCase))
        {
            if (function.Arguments.Count != 1)
            {
                Throw(
                    RuntimeErrorCode.RuntimeExpressionError,
                    "empty() 必须且只能有一个参数。",
                    Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "empty() 必须且只能有一个参数。", function.Start, function.End));
            }

            var value = EvaluateNode(function.Arguments[0], state, depth + 1);
            return Bool(IsEmpty(value, state, function));
        }

        var diagnostic = Diagnostic(MicroflowExpressionDiagnosticCode.UnsupportedFunction, state.Context.Options.AllowUnsupportedFunctions ? MicroflowExpressionDiagnosticSeverity.Warning : MicroflowExpressionDiagnosticSeverity.Error, $"不支持的函数：{function.FunctionName}", function.Start, function.End);
        state.Diagnostics.Add(diagnostic);
        if (!state.Context.Options.AllowUnsupportedFunctions)
        {
            Throw(RuntimeErrorCode.RuntimeExpressionError, $"不支持的函数：{function.FunctionName}", diagnostic);
        }

        return UnknownValue();
    }

    private MicroflowExpressionValue EvaluateIf(MicroflowExpressionIfNode ifNode, EvaluationState state, int depth)
    {
        var condition = RequireBool(EvaluateNode(ifNode.Condition, state, depth + 1), ifNode.Condition.Start, ifNode.Condition.End);
        return condition
            ? EvaluateNode(ifNode.ThenExpression, state, depth + 1)
            : EvaluateNode(ifNode.ElseExpression, state, depth + 1);
    }

    private MicroflowExpressionValue EvaluateEnumeration(MicroflowExpressionEnumerationValueNode enumeration, EvaluationState state)
    {
        var enumQualifiedName = enumeration.EnumQualifiedName ?? state.Context.ExpectedType?.EnumerationQualifiedName;
        var type = MicroflowExpressionType.Enumeration(enumQualifiedName);
        if (state.Context.MetadataResolver is not null
            && state.Context.MetadataResolutionContext is not null
            && !string.IsNullOrWhiteSpace(enumQualifiedName))
        {
            var resolved = state.Context.MetadataResolver.ResolveEnumerationValue(
                state.Context.MetadataResolutionContext,
                enumQualifiedName!,
                enumeration.ValueName,
                state.Context.CurrentObjectId,
                state.Context.CurrentActionId);
            foreach (var diagnostic in resolved.Diagnostics)
            {
                state.Diagnostics.Add(Diagnostic(MicroflowExpressionDiagnosticCode.MetadataNotFound, diagnostic.Severity, diagnostic.Message, enumeration.Start, enumeration.End));
            }

            if (resolved.Found)
            {
                return Value(
                    type,
                    JsonSerializer.Serialize(new { qualifiedName = $"{resolved.EnumerationQualifiedName}.{resolved.Value}", enumQualifiedName = resolved.EnumerationQualifiedName, value = resolved.Value }, JsonOptions),
                    string.IsNullOrWhiteSpace(resolved.Caption) ? $"{resolved.EnumerationQualifiedName}.{resolved.Value}" : resolved.Caption!,
                    enumValue: resolved.Value);
            }
        }

        if (state.Context.MetadataCatalog is not null && !string.IsNullOrWhiteSpace(enumQualifiedName))
        {
            var metadata = state.Context.MetadataCatalog.Enumerations.FirstOrDefault(item => string.Equals(item.QualifiedName, enumQualifiedName, StringComparison.Ordinal));
            if (metadata is null || metadata.Values.All(item => !string.Equals(item.Key, enumeration.ValueName, StringComparison.Ordinal)))
            {
                state.Diagnostics.Add(Diagnostic(MicroflowExpressionDiagnosticCode.MetadataNotFound, MicroflowExpressionDiagnosticSeverity.Warning, $"枚举值无法从 metadata 确认：{enumeration.QualifiedName}", enumeration.Start, enumeration.End));
            }
        }

        return Value(
            type,
            JsonSerializer.Serialize(new { qualifiedName = enumeration.QualifiedName, enumQualifiedName, value = enumeration.ValueName }, JsonOptions),
            enumeration.QualifiedName,
            enumValue: enumeration.ValueName);
    }

    private static bool IsEmpty(MicroflowExpressionValue value, EvaluationState state, MicroflowExpressionFunctionCallNode function)
    {
        if (string.IsNullOrWhiteSpace(value.RawValueJson) || value.RawValueJson == "null")
        {
            return true;
        }

        return value.Type.Kind switch
        {
            MicroflowExpressionTypeKind.String => string.IsNullOrEmpty(value.StringValue),
            MicroflowExpressionTypeKind.List => value.ListValue.HasValue && value.ListValue.Value.ValueKind == JsonValueKind.Array && value.ListValue.Value.GetArrayLength() == 0,
            MicroflowExpressionTypeKind.Object => false,
            MicroflowExpressionTypeKind.Unknown => WarnUnknownEmpty(state, function),
            _ => false
        };
    }

    private static bool WarnUnknownEmpty(EvaluationState state, MicroflowExpressionFunctionCallNode function)
    {
        state.Diagnostics.Add(Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Warning, "empty() 遇到 unknown 类型，按 false 返回。", function.Start, function.End));
        return false;
    }

    private static MicroflowExpressionValue FromRuntimeVariable(MicroflowRuntimeVariableValue value, int maxStringLength)
    {
        var type = MicroflowExpressionTypeHelper.FromDataTypeJson(value.DataTypeJson);
        if (string.IsNullOrWhiteSpace(value.RawValueJson))
        {
            return NullValue(type);
        }

        try
        {
            using var document = JsonDocument.Parse(value.RawValueJson);
            return FromJsonElement(document.RootElement.Clone(), type, maxStringLength);
        }
        catch (JsonException)
        {
            return Value(type, JsonSerializer.Serialize(value.RawValueJson, JsonOptions), Trim(value.RawValueJson, maxStringLength), stringValue: value.RawValueJson);
        }
    }

    private static MicroflowRuntimeVariableValue UnknownRuntimeValue(string name)
        => new()
        {
            Name = name,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "unknown" }, JsonOptions),
            RawValueJson = "null",
            ValuePreview = "null",
            Kind = MicroflowRuntimeVariableKind.Unknown
        };

    private static MicroflowExpressionValue FromJsonElement(JsonElement element, MicroflowExpressionType type, int maxStringLength)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return NullValue(type);
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => Value(type.Kind == MicroflowExpressionTypeKind.Unknown ? MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String) : type, element.GetRawText(), Trim(element.GetString() ?? string.Empty, maxStringLength), stringValue: element.GetString()),
            JsonValueKind.True => Bool(true),
            JsonValueKind.False => Bool(false),
            JsonValueKind.Number => element.TryGetInt64(out var integer)
                ? Value(type.Kind == MicroflowExpressionTypeKind.Unknown ? MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Integer) : type, element.GetRawText(), element.GetRawText(), decimalValue: integer, integerValue: integer)
                : Value(type.Kind == MicroflowExpressionTypeKind.Unknown ? MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal) : type, element.GetRawText(), element.GetRawText(), decimalValue: element.GetDecimal()),
            JsonValueKind.Array => Value(type.Kind == MicroflowExpressionTypeKind.Unknown ? MicroflowExpressionType.List() : type, element.GetRawText(), Trim(element.GetRawText(), maxStringLength), listValue: element.Clone()),
            JsonValueKind.Object => Value(type.Kind == MicroflowExpressionTypeKind.Unknown ? MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Json) : type, element.GetRawText(), Trim(element.GetRawText(), maxStringLength), objectValue: element.Clone()),
            _ => UnknownValue()
        };
    }

    private static void CheckExpectedType(
        MicroflowExpressionValue value,
        MicroflowExpressionEvaluationContext context,
        EvaluationState state,
        MicroflowExpressionAstNode ast)
    {
        if (context.ExpectedType is null || MicroflowExpressionTypeHelper.IsAssignableTo(value.Type, context.ExpectedType))
        {
            return;
        }

        var severity = MicroflowExpressionTypeHelper.IsUnknown(value.Type) && !context.Options.StrictTypeCheck
            ? MicroflowExpressionDiagnosticSeverity.Warning
            : MicroflowExpressionDiagnosticSeverity.Error;
        state.Diagnostics.Add(Diagnostic(MicroflowExpressionDiagnosticCode.ExpectedTypeMismatch, severity, $"表达式结果类型 {value.Type.Kind} 不能赋给期望类型 {context.ExpectedType.Kind}。", ast.Start, ast.End));
    }

    private static bool ValueEquals(MicroflowExpressionValue left, MicroflowExpressionValue right)
    {
        if ((left.RawValueJson is null or "null") || (right.RawValueJson is null or "null"))
        {
            return (left.RawValueJson is null or "null") && (right.RawValueJson is null or "null");
        }

        if (left.DecimalValue.HasValue && right.DecimalValue.HasValue)
        {
            return left.DecimalValue.Value == right.DecimalValue.Value;
        }

        if (left.BoolValue.HasValue && right.BoolValue.HasValue)
        {
            return left.BoolValue.Value == right.BoolValue.Value;
        }

        if (left.EnumValue is not null || right.EnumValue is not null)
        {
            return string.Equals(left.EnumValue ?? left.ValuePreview, right.EnumValue ?? right.ValuePreview, StringComparison.Ordinal);
        }

        return string.Equals(left.StringValue ?? left.ValuePreview, right.StringValue ?? right.ValuePreview, StringComparison.Ordinal);
    }

    private static decimal RequireNumber(MicroflowExpressionValue value, int start, int end)
    {
        if (value.DecimalValue.HasValue)
        {
            return value.DecimalValue.Value;
        }

        Throw(RuntimeErrorCode.RuntimeVariableTypeMismatch, "表达式操作数必须是数字。", Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "表达式操作数必须是数字。", start, end));
        return 0;
    }

    private static bool RequireBool(MicroflowExpressionValue value, int start, int end)
    {
        if (value.BoolValue.HasValue)
        {
            return value.BoolValue.Value;
        }

        Throw(RuntimeErrorCode.RuntimeVariableTypeMismatch, "表达式操作数必须是 boolean。", Diagnostic(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "表达式操作数必须是 boolean。", start, end));
        return false;
    }

    private static MicroflowExpressionValue Bool(bool value)
        => Value(
            MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean, nullable: false),
            value ? "true" : "false",
            value ? "true" : "false",
            boolValue: value);

    private static MicroflowExpressionValue DecimalValue(decimal value)
        => Value(
            MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal, nullable: false),
            value.ToString(CultureInfo.InvariantCulture),
            value.ToString(CultureInfo.InvariantCulture),
            decimalValue: value);

    private static MicroflowExpressionValue NullValue(MicroflowExpressionType? type = null)
        => Value(type ?? MicroflowExpressionType.Unknown(), "null", "null");

    private static MicroflowExpressionValue UnknownValue()
        => Value(MicroflowExpressionType.Unknown(), null, "unknown");

    private static MicroflowExpressionValue Value(
        MicroflowExpressionType type,
        string? rawValueJson,
        string valuePreview,
        bool? boolValue = null,
        string? stringValue = null,
        decimal? decimalValue = null,
        long? integerValue = null,
        JsonElement? objectValue = null,
        JsonElement? listValue = null,
        string? enumValue = null)
        => new()
        {
            Kind = type.Kind,
            Type = type,
            RawValueJson = rawValueJson,
            ValuePreview = valuePreview,
            BoolValue = boolValue,
            StringValue = stringValue,
            DecimalValue = decimalValue,
            IntegerValue = integerValue,
            ObjectValue = objectValue,
            ListValue = listValue,
            EnumValue = enumValue
        };

    private static bool TryGetVariable(IMicroflowVariableStore store, string variableName, out MicroflowRuntimeVariableValue? value)
        => store.TryGet(variableName, out value) || store.TryGet("$" + variableName, out value);

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool MetadataMemberExists(MicroflowMetadataCatalogDto? metadata, MicroflowExpressionType rootType, string segment)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(rootType.EntityQualifiedName))
        {
            return false;
        }

        var entity = metadata.Entities.FirstOrDefault(item => string.Equals(item.QualifiedName, rootType.EntityQualifiedName, StringComparison.Ordinal));
        return entity is not null
               && (entity.Attributes.Any(item => string.Equals(item.Name, segment, StringComparison.Ordinal) || item.QualifiedName.EndsWith($".{segment}", StringComparison.Ordinal))
                   || entity.Associations.Any(item => item.AssociationQualifiedName.EndsWith($".{segment}", StringComparison.Ordinal)));
    }

    private static MicroflowResolvedMemberPath? ResolveMemberPathWithMetadata(
        MicroflowExpressionMemberAccessNode member,
        EvaluationState state,
        MicroflowExpressionType rootType)
    {
        if (state.Context.MetadataResolver is null || state.Context.MetadataResolutionContext is null)
        {
            return null;
        }

        return state.Context.MetadataResolver.ResolveMemberPath(
            state.Context.MetadataResolutionContext,
            ToResolvedDataType(rootType),
            member.MemberPath,
            state.Context.CurrentObjectId,
            state.Context.CurrentActionId);
    }

    private static MicroflowResolvedDataType ToResolvedDataType(MicroflowExpressionType type)
        => new()
        {
            Found = !MicroflowExpressionTypeHelper.IsUnknown(type),
            Kind = type.Kind,
            EntityQualifiedName = type.EntityQualifiedName,
            EnumerationQualifiedName = type.EnumerationQualifiedName,
            ItemType = type.ItemType is null ? null : ToResolvedDataType(type.ItemType),
            RawDataTypeJson = type.RawDataTypeJson
        };

    private static MicroflowExpressionType FromResolvedDataType(MicroflowResolvedDataType type)
        => type.Kind switch
        {
            MicroflowResolvedDataTypeKind.Object => MicroflowExpressionType.Object(type.EntityQualifiedName) with { RawDataTypeJson = type.RawDataTypeJson },
            MicroflowResolvedDataTypeKind.List => MicroflowExpressionType.List(type.ItemType is null ? MicroflowExpressionType.Unknown() : FromResolvedDataType(type.ItemType)) with { RawDataTypeJson = type.RawDataTypeJson },
            MicroflowResolvedDataTypeKind.Enumeration => MicroflowExpressionType.Enumeration(type.EnumerationQualifiedName) with { RawDataTypeJson = type.RawDataTypeJson },
            MicroflowResolvedDataTypeKind.Unknown => MicroflowExpressionType.Unknown(type.RawDataTypeJson),
            _ => MicroflowExpressionType.Simple(type.Kind) with { RawDataTypeJson = type.RawDataTypeJson }
        };

    private static string MapDiagnosticCode(string code)
        => code.Contains("MEMBER", StringComparison.OrdinalIgnoreCase)
            ? MicroflowExpressionDiagnosticCode.MemberNotFound
            : MicroflowExpressionDiagnosticCode.MetadataNotFound;

    private static MicroflowExpressionEvaluationResult Failed(
        string code,
        string message,
        Stopwatch stopwatch,
        IReadOnlyList<MicroflowExpressionDiagnostic> diagnostics,
        MicroflowExpressionTypeInferenceResult? inference)
    {
        stopwatch.Stop();
        return new MicroflowExpressionEvaluationResult
        {
            Success = false,
            ValueType = MicroflowExpressionType.Unknown(),
            Diagnostics = diagnostics,
            Error = new MicroflowExpressionRuntimeError
            {
                Code = code,
                Message = message,
                Diagnostics = diagnostics
            },
            ReferencedVariables = inference?.ReferencedVariables ?? Array.Empty<string>(),
            ReferencedMembers = inference?.ReferencedMembers ?? Array.Empty<string>(),
            DurationMs = (int)stopwatch.ElapsedMilliseconds
        };
    }

    private static MicroflowExpressionDiagnostic Diagnostic(
        string code,
        string severity,
        string message,
        int start,
        int end,
        string? variableName = null,
        string? memberPath = null)
        => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            Start = start,
            End = end,
            VariableName = variableName,
            MemberPath = memberPath
        };

    private static string Trim(string? value, int maxLength)
    {
        var preview = value ?? "null";
        var limit = maxLength <= 0 ? 500 : maxLength;
        return preview.Length <= limit ? preview : preview[..limit] + "...";
    }

    private static MicroflowExpressionValue ThrowValue(string code, string message, params MicroflowExpressionDiagnostic[] diagnostics)
    {
        Throw(code, message, diagnostics);
        return UnknownValue();
    }

    private static void Throw(string code, string message, params MicroflowExpressionDiagnostic[] diagnostics)
        => throw new MicroflowExpressionEvaluationException(code, message, diagnostics);

    private sealed class EvaluationState
    {
        public EvaluationState(MicroflowExpressionEvaluationContext context, List<MicroflowExpressionDiagnostic> diagnostics)
        {
            Context = context;
            Diagnostics = diagnostics;
        }

        public MicroflowExpressionEvaluationContext Context { get; }
        public List<MicroflowExpressionDiagnostic> Diagnostics { get; }
        public HashSet<string> ReferencedVariables { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ReferencedMembers { get; } = new(StringComparer.Ordinal);
    }

    private sealed class MicroflowExpressionEvaluationException : Exception
    {
        public MicroflowExpressionEvaluationException(string code, string message, IReadOnlyList<MicroflowExpressionDiagnostic> diagnostics)
            : base(message)
        {
            Code = code;
            Diagnostics = diagnostics;
        }

        public string Code { get; }
        public IReadOnlyList<MicroflowExpressionDiagnostic> Diagnostics { get; }
    }
}
