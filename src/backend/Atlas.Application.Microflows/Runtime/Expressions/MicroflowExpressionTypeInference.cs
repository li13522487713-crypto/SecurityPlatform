using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Metadata;

namespace Atlas.Application.Microflows.Runtime.Expressions;

public sealed class MicroflowExpressionTypeInference
{
    private readonly List<MicroflowExpressionDiagnostic> _diagnostics = [];
    private readonly HashSet<string> _variables = new(StringComparer.Ordinal);
    private readonly HashSet<string> _members = new(StringComparer.Ordinal);
    private readonly HashSet<string> _metadata = new(StringComparer.Ordinal);
    private MicroflowExpressionEvaluationContext _context = null!;

    public MicroflowExpressionTypeInferenceResult Infer(
        MicroflowExpressionAstNode ast,
        MicroflowExpressionEvaluationContext context)
    {
        _diagnostics.Clear();
        _variables.Clear();
        _members.Clear();
        _metadata.Clear();
        _context = context;

        var inferred = InferNode(ast, context.ExpectedType);
        if (context.ExpectedType is not null)
        {
            CheckExpectedType(inferred, context.ExpectedType, ast.Start, ast.End);
        }

        return new MicroflowExpressionTypeInferenceResult
        {
            InferredType = inferred,
            Confidence = Confidence(inferred),
            Diagnostics = _diagnostics.Concat(ast.Diagnostics).ToArray(),
            ReferencedVariables = _variables.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ReferencedMembers = _members.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ReferencedMetadata = _metadata.OrderBy(item => item, StringComparer.Ordinal).ToArray()
        };
    }

    private MicroflowExpressionType InferNode(MicroflowExpressionAstNode node, MicroflowExpressionType? expectedType = null)
    {
        switch (node)
        {
            case MicroflowExpressionLiteralNode literal:
                return literal.LiteralKind switch
                {
                    MicroflowExpressionLiteralKind.String => MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String, nullable: false),
                    MicroflowExpressionLiteralKind.Boolean => MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean, nullable: false),
                    MicroflowExpressionLiteralKind.Integer => MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Integer, nullable: false),
                    MicroflowExpressionLiteralKind.Decimal => MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal, nullable: false),
                    MicroflowExpressionLiteralKind.Null or MicroflowExpressionLiteralKind.Empty => MicroflowExpressionType.Unknown(),
                    _ => MicroflowExpressionType.Unknown()
                };

            case MicroflowExpressionVariableReferenceNode variable:
                return InferVariable(variable.NormalizedName, variable.Start, variable.End);

            case MicroflowExpressionMemberAccessNode member:
                return InferMember(member);

            case MicroflowExpressionUnaryNode unary:
                return InferUnary(unary);

            case MicroflowExpressionBinaryNode binary:
                return InferBinary(binary);

            case MicroflowExpressionFunctionCallNode function:
                return InferFunction(function);

            case MicroflowExpressionIfNode ifNode:
                return InferIf(ifNode);

            case MicroflowExpressionEnumerationValueNode enumeration:
                return InferEnumeration(enumeration, expectedType);

            default:
                Add(MicroflowExpressionDiagnosticCode.ParseError, MicroflowExpressionDiagnosticSeverity.Error, "表达式 AST 无效。", node.Start, node.End);
                return MicroflowExpressionType.Unknown();
        }
    }

    private MicroflowExpressionType InferVariable(string variableName, int start, int end)
    {
        _variables.Add(variableName);
        if (!TryGetVariable(variableName, out var value) || value is null)
        {
            Add(MicroflowExpressionDiagnosticCode.VariableNotFound, MicroflowExpressionDiagnosticSeverity.Error, $"变量不存在或不在当前作用域：${variableName}", start, end, variableName: variableName);
            return MicroflowExpressionType.Unknown();
        }

        return MicroflowExpressionTypeHelper.FromDataTypeJson(value.DataTypeJson);
    }

    private MicroflowExpressionType InferMember(MicroflowExpressionMemberAccessNode member)
    {
        _variables.Add(member.RootVariableName);
        _members.Add($"{member.RootVariableName}/{member.RawPath}");
        if (!TryGetVariable(member.RootVariableName, out var value) || value is null)
        {
            Add(MicroflowExpressionDiagnosticCode.VariableNotFound, MicroflowExpressionDiagnosticSeverity.Error, $"变量不存在或不在当前作用域：${member.RootVariableName}", member.Start, member.End, variableName: member.RootVariableName);
            return MicroflowExpressionType.Unknown();
        }

        var rootType = MicroflowExpressionTypeHelper.FromDataTypeJson(value.DataTypeJson);
        if (_context.MetadataResolver is not null && _context.MetadataResolutionContext is not null)
        {
            var resolved = _context.MetadataResolver.ResolveMemberPath(
                _context.MetadataResolutionContext,
                ToResolvedDataType(rootType),
                member.MemberPath,
                _context.CurrentObjectId,
                _context.CurrentActionId);
            foreach (var diagnostic in resolved.Diagnostics)
            {
                Add(
                    MapDiagnosticCode(diagnostic.Code),
                    diagnostic.Severity,
                    diagnostic.Message,
                    member.Start,
                    member.End,
                    memberPath: member.RawPath);
            }

            if (resolved.Found)
            {
                return FromResolvedDataType(resolved.FinalType);
            }
        }

        if (MicroflowExpressionTypeHelper.IsList(rootType))
        {
            Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Warning, $"list<object> 直接成员访问暂不精确支持：{member.RawPath}", member.Start, member.End, memberPath: member.RawPath);
            return MicroflowExpressionType.Unknown();
        }

        if (!MicroflowExpressionTypeHelper.IsObject(rootType) && !MicroflowExpressionTypeHelper.IsUnknown(rootType) && !MicroflowExpressionTypeHelper.IsJson(rootType))
        {
            Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, $"变量 ${member.RootVariableName} 不是对象，不能访问成员。", member.Start, member.End, memberPath: member.RawPath);
            return MicroflowExpressionType.Unknown();
        }

        var current = rootType;
        for (var index = 0; index < member.MemberPath.Count; index++)
        {
            var segment = member.MemberPath[index];
            if (MicroflowExpressionTypeHelper.IsUnknown(current) || MicroflowExpressionTypeHelper.IsJson(current))
            {
                Add(MicroflowExpressionDiagnosticCode.MetadataNotFound, MicroflowExpressionDiagnosticSeverity.Warning, $"成员 {segment} 缺少可用 metadata，类型降级为 unknown。", member.Start, member.End, memberPath: member.RawPath);
                return MicroflowExpressionType.Unknown();
            }

            var next = ResolveMember(current, segment, member.Start, member.End, member.RawPath);
            if (next is null)
            {
                return MicroflowExpressionType.Unknown();
            }

            current = next;
            if (index < member.MemberPath.Count - 1)
            {
                Add(MicroflowExpressionDiagnosticCode.MetadataNotFound, MicroflowExpressionDiagnosticSeverity.Warning, $"多层成员访问 {member.RawPath} 第一版降级为 unknown。", member.Start, member.End, memberPath: member.RawPath);
                return MicroflowExpressionType.Unknown();
            }
        }

        return current;
    }

    private MicroflowExpressionType? ResolveMember(MicroflowExpressionType rootType, string segment, int start, int end, string rawPath)
    {
        if (_context.MetadataCatalog is null)
        {
            Add(MicroflowExpressionDiagnosticCode.MetadataNotFound, MicroflowExpressionDiagnosticSeverity.Warning, "MetadataCatalog 缺失，成员类型降级为 unknown。", start, end, memberPath: rawPath);
            return MicroflowExpressionType.Unknown();
        }

        if (string.IsNullOrWhiteSpace(rootType.EntityQualifiedName))
        {
            Add(MicroflowExpressionDiagnosticCode.MetadataNotFound, MicroflowExpressionDiagnosticSeverity.Warning, "对象类型缺少 entityQualifiedName，成员类型降级为 unknown。", start, end, memberPath: rawPath);
            return MicroflowExpressionType.Unknown();
        }

        _metadata.Add(rootType.EntityQualifiedName);
        var entity = _context.MetadataCatalog.Entities.FirstOrDefault(item => string.Equals(item.QualifiedName, rootType.EntityQualifiedName, StringComparison.Ordinal));
        if (entity is null)
        {
            Add(MicroflowExpressionDiagnosticCode.MetadataNotFound, MicroflowExpressionDiagnosticSeverity.Error, $"实体 metadata 不存在：{rootType.EntityQualifiedName}", start, end, memberPath: rawPath);
            return null;
        }

        var attribute = entity.Attributes.FirstOrDefault(item =>
            string.Equals(item.Name, segment, StringComparison.Ordinal)
            || item.QualifiedName.EndsWith($".{segment}", StringComparison.Ordinal));
        if (attribute is not null)
        {
            if (!string.IsNullOrWhiteSpace(attribute.EnumQualifiedName))
            {
                _metadata.Add(attribute.EnumQualifiedName!);
                return MicroflowExpressionType.Enumeration(attribute.EnumQualifiedName);
            }

            var type = MicroflowExpressionTypeHelper.FromDataTypeJson(attribute.Type.GetRawText());
            _metadata.Add(attribute.QualifiedName);
            return type;
        }

        var associationRef = entity.Associations.FirstOrDefault(item =>
            item.AssociationQualifiedName.EndsWith($".{segment}", StringComparison.Ordinal)
            || string.Equals(item.AssociationQualifiedName.Split('.').LastOrDefault(), segment, StringComparison.Ordinal)
            || string.Equals(item.TargetEntityQualifiedName.Split('.').LastOrDefault(), segment, StringComparison.Ordinal));
        if (associationRef is not null)
        {
            _metadata.Add(associationRef.AssociationQualifiedName);
            var target = MicroflowExpressionType.Object(associationRef.TargetEntityQualifiedName);
            return associationRef.Multiplicity.Contains("Many", StringComparison.OrdinalIgnoreCase)
                ? MicroflowExpressionType.List(target)
                : target;
        }

        Add(MicroflowExpressionDiagnosticCode.MemberNotFound, MicroflowExpressionDiagnosticSeverity.Error, $"成员不存在：{rootType.EntityQualifiedName}/{segment}", start, end, memberPath: rawPath);
        return null;
    }

    private MicroflowExpressionType InferUnary(MicroflowExpressionUnaryNode unary)
    {
        var operand = InferNode(unary.Operand);
        if (string.Equals(unary.Operator, "not", StringComparison.OrdinalIgnoreCase))
        {
            if (!MicroflowExpressionTypeHelper.IsBoolean(operand) && !MicroflowExpressionTypeHelper.IsUnknown(operand))
            {
                Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "not 操作数必须是 boolean。", unary.Start, unary.End);
            }

            return MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean, nullable: false);
        }

        if (unary.Operator == "-")
        {
            if (!MicroflowExpressionTypeHelper.IsNumeric(operand) && !MicroflowExpressionTypeHelper.IsUnknown(operand))
            {
                Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "- 操作数必须是数字。", unary.Start, unary.End);
                return MicroflowExpressionType.Unknown();
            }

            return operand;
        }

        return MicroflowExpressionType.Unknown();
    }

    private MicroflowExpressionType InferBinary(MicroflowExpressionBinaryNode binary)
    {
        var left = InferNode(binary.Left);
        var right = InferNode(binary.Right, left.Kind == MicroflowExpressionTypeKind.Enumeration ? left : null);

        if (binary.Operator is "=" or "!=" or ">" or "<" or ">=" or "<=")
        {
            if (binary.Operator is ">" or "<" or ">=" or "<=" && !MicroflowExpressionTypeHelper.IsNumeric(left) && !MicroflowExpressionTypeHelper.IsNumeric(right) && !MicroflowExpressionTypeHelper.IsUnknown(left) && !MicroflowExpressionTypeHelper.IsUnknown(right))
            {
                Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, $"比较操作符 {binary.Operator} 仅支持数字大小比较。", binary.Start, binary.End);
            }

            if (!MicroflowExpressionTypeHelper.IsCompatible(left, right) && !MicroflowExpressionTypeHelper.IsUnknown(left) && !MicroflowExpressionTypeHelper.IsUnknown(right))
            {
                Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Warning, $"比较左右类型不一致：{left.Kind} vs {right.Kind}。", binary.Start, binary.End);
            }

            return MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean, nullable: false);
        }

        if (binary.Operator is "and" or "or")
        {
            ExpectBoolean(left, binary.Left);
            ExpectBoolean(right, binary.Right);
            return MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean, nullable: false);
        }

        if (binary.Operator == "+")
        {
            if (MicroflowExpressionTypeHelper.IsString(left) || MicroflowExpressionTypeHelper.IsString(right))
            {
                return MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String, nullable: false);
            }

            if (MicroflowExpressionTypeHelper.IsNumeric(left) && MicroflowExpressionTypeHelper.IsNumeric(right))
            {
                return left.Kind == MicroflowExpressionTypeKind.Decimal || right.Kind == MicroflowExpressionTypeKind.Decimal
                    ? MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal, nullable: false)
                    : MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Integer, nullable: false);
            }
        }

        if (binary.Operator is "-" or "*" or "/")
        {
            if (MicroflowExpressionTypeHelper.IsNumeric(left) && MicroflowExpressionTypeHelper.IsNumeric(right))
            {
                return binary.Operator == "/" || left.Kind == MicroflowExpressionTypeKind.Decimal || right.Kind == MicroflowExpressionTypeKind.Decimal
                    ? MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal, nullable: false)
                    : MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Integer, nullable: false);
            }
        }

        if (!MicroflowExpressionTypeHelper.IsUnknown(left) && !MicroflowExpressionTypeHelper.IsUnknown(right))
        {
            Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, $"操作符 {binary.Operator} 不支持类型 {left.Kind} 与 {right.Kind}。", binary.Start, binary.End);
        }

        return MicroflowExpressionType.Unknown();
    }

    private MicroflowExpressionType InferFunction(MicroflowExpressionFunctionCallNode function)
    {
        if (string.Equals(function.FunctionName, "empty", StringComparison.OrdinalIgnoreCase))
        {
            if (function.Arguments.Count != 1)
            {
                Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "empty() 必须且只能有一个参数。", function.Start, function.End);
            }
            else
            {
                var argumentType = InferNode(function.Arguments[0]);
                if (!MicroflowExpressionTypeHelper.IsObject(argumentType)
                    && !MicroflowExpressionTypeHelper.IsList(argumentType)
                    && !MicroflowExpressionTypeHelper.IsString(argumentType)
                    && !MicroflowExpressionTypeHelper.IsUnknown(argumentType))
                {
                    Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Warning, $"empty() 参数类型 {argumentType.Kind} 可能不受支持。", function.Start, function.End);
                }
            }

            return MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean, nullable: false);
        }

        foreach (var argument in function.Arguments)
        {
            InferNode(argument);
        }

        Add(MicroflowExpressionDiagnosticCode.UnsupportedFunction, MicroflowExpressionDiagnosticSeverity.Error, $"不支持的函数：{function.FunctionName}", function.Start, function.End);
        return MicroflowExpressionType.Unknown();
    }

    private MicroflowExpressionType InferIf(MicroflowExpressionIfNode ifNode)
    {
        var condition = InferNode(ifNode.Condition, MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Boolean));
        ExpectBoolean(condition, ifNode.Condition);
        var thenType = InferNode(ifNode.ThenExpression);
        var elseType = InferNode(ifNode.ElseExpression);
        if (MicroflowExpressionTypeHelper.IsCompatible(thenType, elseType))
        {
            return MicroflowExpressionTypeHelper.CommonType(thenType, elseType);
        }

        Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Warning, $"if then/else 类型不一致：{thenType.Kind} vs {elseType.Kind}。", ifNode.Start, ifNode.End);
        return MicroflowExpressionType.Unknown();
    }

    private MicroflowExpressionType InferEnumeration(MicroflowExpressionEnumerationValueNode enumeration, MicroflowExpressionType? expectedType)
    {
        if (_context.MetadataCatalog is not null)
        {
            var enumType = _context.MetadataCatalog.Enumerations.FirstOrDefault(item =>
                string.Equals(item.QualifiedName, enumeration.EnumQualifiedName, StringComparison.Ordinal)
                && item.Values.Any(value => string.Equals(value.Key, enumeration.ValueName, StringComparison.Ordinal)));
            if (enumType is not null)
            {
                _metadata.Add(enumType.QualifiedName);
                return MicroflowExpressionType.Enumeration(enumType.QualifiedName);
            }
        }

        if (expectedType is not null && MicroflowExpressionTypeHelper.IsEnumeration(expectedType))
        {
            return expectedType;
        }

        Add(MicroflowExpressionDiagnosticCode.MetadataNotFound, MicroflowExpressionDiagnosticSeverity.Warning, $"枚举值无法从 metadata 确认：{enumeration.QualifiedName}", enumeration.Start, enumeration.End);
        return MicroflowExpressionType.Unknown();
    }

    private void ExpectBoolean(MicroflowExpressionType type, MicroflowExpressionAstNode node)
    {
        if (!MicroflowExpressionTypeHelper.IsBoolean(type) && !MicroflowExpressionTypeHelper.IsUnknown(type))
        {
            Add(MicroflowExpressionDiagnosticCode.TypeMismatch, MicroflowExpressionDiagnosticSeverity.Error, "表达式需要 boolean 类型。", node.Start, node.End);
        }
    }

    private void CheckExpectedType(MicroflowExpressionType actual, MicroflowExpressionType expected, int start, int end)
    {
        if (!MicroflowExpressionTypeHelper.IsAssignableTo(actual, expected))
        {
            var severity = MicroflowExpressionTypeHelper.IsUnknown(actual)
                ? MicroflowExpressionDiagnosticSeverity.Warning
                : MicroflowExpressionDiagnosticSeverity.Error;
            Add(MicroflowExpressionDiagnosticCode.ExpectedTypeMismatch, severity, $"表达式类型 {actual.Kind} 不能赋给期望类型 {expected.Kind}。", start, end);
        }
    }

    private bool TryGetVariable(string variableName, out MicroflowRuntimeVariableValue? value)
        => _context.VariableStore.TryGet(variableName, out value)
            || _context.VariableStore.TryGet("$" + variableName, out value);

    private void Add(string code, string severity, string message, int start, int end, string? variableName = null, string? memberPath = null)
        => _diagnostics.Add(new MicroflowExpressionDiagnostic
        {
            Code = code,
            Severity = severity,
            Message = message,
            Start = start,
            End = end,
            VariableName = variableName,
            MemberPath = memberPath
        });

    private static string Confidence(MicroflowExpressionType type)
        => MicroflowExpressionTypeHelper.IsUnknown(type) ? "low" : "high";

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
}

public static class MicroflowExpressionTypeHelper
{
    public static MicroflowExpressionType FromDataTypeJson(string? dataTypeJson)
    {
        if (string.IsNullOrWhiteSpace(dataTypeJson))
        {
            return MicroflowExpressionType.Unknown();
        }

        try
        {
            using var document = JsonDocument.Parse(dataTypeJson);
            return FromDataType(document.RootElement, dataTypeJson);
        }
        catch (JsonException)
        {
            return MicroflowExpressionType.Unknown(dataTypeJson);
        }
    }

    public static MicroflowExpressionType FromDataType(JsonElement element, string? raw = null)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return MicroflowExpressionType.Unknown(raw ?? element.GetRawText());
        }

        var kind = ReadString(element, "kind") ?? MicroflowExpressionTypeKind.Unknown;
        return kind switch
        {
            MicroflowExpressionTypeKind.Object => MicroflowExpressionType.Object(ReadString(element, "entityQualifiedName")) with { RawDataTypeJson = raw ?? element.GetRawText() },
            MicroflowExpressionTypeKind.List => MicroflowExpressionType.List(element.TryGetProperty("itemType", out var itemType) ? FromDataType(itemType, itemType.GetRawText()) : MicroflowExpressionType.Unknown()) with { RawDataTypeJson = raw ?? element.GetRawText() },
            MicroflowExpressionTypeKind.Enumeration => MicroflowExpressionType.Enumeration(ReadString(element, "enumerationQualifiedName") ?? ReadString(element, "enumQualifiedName")) with { RawDataTypeJson = raw ?? element.GetRawText() },
            MicroflowExpressionTypeKind.Boolean or MicroflowExpressionTypeKind.String or MicroflowExpressionTypeKind.Integer or MicroflowExpressionTypeKind.Long or MicroflowExpressionTypeKind.Decimal or MicroflowExpressionTypeKind.DateTime or MicroflowExpressionTypeKind.Void or MicroflowExpressionTypeKind.Json or MicroflowExpressionTypeKind.Error or MicroflowExpressionTypeKind.HttpResponse => MicroflowExpressionType.Simple(kind) with { RawDataTypeJson = raw ?? element.GetRawText() },
            _ => MicroflowExpressionType.Unknown(raw ?? element.GetRawText())
        };
    }

    public static bool IsAssignableTo(MicroflowExpressionType actual, MicroflowExpressionType expected)
    {
        if (IsUnknown(expected) || IsUnknown(actual))
        {
            return true;
        }

        if (string.Equals(expected.Kind, MicroflowExpressionTypeKind.Json, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsNumeric(actual) && IsNumeric(expected))
        {
            return actual.Kind != MicroflowExpressionTypeKind.Decimal || expected.Kind == MicroflowExpressionTypeKind.Decimal;
        }

        if (IsEnumeration(actual) && IsEnumeration(expected))
        {
            return string.IsNullOrWhiteSpace(expected.EnumerationQualifiedName)
                || string.Equals(actual.EnumerationQualifiedName, expected.EnumerationQualifiedName, StringComparison.Ordinal);
        }

        if (IsObject(actual) && IsObject(expected))
        {
            return string.IsNullOrWhiteSpace(expected.EntityQualifiedName)
                || string.IsNullOrWhiteSpace(actual.EntityQualifiedName)
                || string.Equals(actual.EntityQualifiedName, expected.EntityQualifiedName, StringComparison.Ordinal);
        }

        if (IsList(actual) && IsList(expected))
        {
            return expected.ItemType is null || actual.ItemType is null || IsAssignableTo(actual.ItemType, expected.ItemType);
        }

        return string.Equals(actual.Kind, expected.Kind, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCompatible(MicroflowExpressionType left, MicroflowExpressionType right)
        => IsAssignableTo(left, right) || IsAssignableTo(right, left);

    public static MicroflowExpressionType CommonType(MicroflowExpressionType left, MicroflowExpressionType right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return left.Kind == MicroflowExpressionTypeKind.Decimal || right.Kind == MicroflowExpressionTypeKind.Decimal
                ? MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal)
                : left;
        }

        return IsAssignableTo(left, right) ? right : left;
    }

    public static bool IsNumeric(MicroflowExpressionType type)
        => type.Kind is MicroflowExpressionTypeKind.Integer or MicroflowExpressionTypeKind.Long or MicroflowExpressionTypeKind.Decimal;

    public static bool IsBoolean(MicroflowExpressionType type) => string.Equals(type.Kind, MicroflowExpressionTypeKind.Boolean, StringComparison.OrdinalIgnoreCase);
    public static bool IsString(MicroflowExpressionType type) => string.Equals(type.Kind, MicroflowExpressionTypeKind.String, StringComparison.OrdinalIgnoreCase);
    public static bool IsObject(MicroflowExpressionType type) => string.Equals(type.Kind, MicroflowExpressionTypeKind.Object, StringComparison.OrdinalIgnoreCase);
    public static bool IsList(MicroflowExpressionType type) => string.Equals(type.Kind, MicroflowExpressionTypeKind.List, StringComparison.OrdinalIgnoreCase);
    public static bool IsEnumeration(MicroflowExpressionType type) => string.Equals(type.Kind, MicroflowExpressionTypeKind.Enumeration, StringComparison.OrdinalIgnoreCase);
    public static bool IsUnknown(MicroflowExpressionType type) => string.Equals(type.Kind, MicroflowExpressionTypeKind.Unknown, StringComparison.OrdinalIgnoreCase);
    public static bool IsJson(MicroflowExpressionType type) => string.Equals(type.Kind, MicroflowExpressionTypeKind.Json, StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
