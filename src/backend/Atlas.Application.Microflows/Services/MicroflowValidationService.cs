using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowValidationService : IMicroflowValidationService
{
    private static readonly Regex VariableNameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex VariableReferenceRegex = new(@"\$[A-Za-z_][A-Za-z0-9_]*(?:/[A-Za-z_][A-Za-z0-9_]*)*", RegexOptions.Compiled);
    private static readonly HashSet<string> KnownObjectKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "startEvent", "endEvent", "errorEvent", "breakEvent", "continueEvent", "exclusiveSplit", "inheritanceSplit",
        "exclusiveMerge", "actionActivity", "loopedActivity", "parameterObject", "annotation"
    };

    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowMetadataService _metadataService;
    private readonly IMicroflowSchemaReader _schemaReader;
    private readonly IMicroflowActionSupportMatrix _actionSupportMatrix;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowClock _clock;

    public MicroflowValidationService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowMetadataService metadataService,
        IMicroflowSchemaReader schemaReader,
        IMicroflowActionSupportMatrix actionSupportMatrix,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowClock clock)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _metadataService = metadataService;
        _schemaReader = schemaReader;
        _actionSupportMatrix = actionSupportMatrix;
        _requestContextAccessor = requestContextAccessor;
        _clock = clock;
    }

    public async Task<ValidateMicroflowResponseDto> ValidateAsync(
        string id,
        ValidateMicroflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var schema = request.Schema.HasValue
            ? request.Schema.Value
            : await LoadCurrentSchemaAsync(id, cancellationToken);
        var mode = NormalizeMode(request.Mode);
        var metadata = await _metadataService.GetCatalogAsync(
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = _requestContextAccessor.Current.WorkspaceId,
                IncludeSystem = true,
                IncludeArchived = true
            },
            cancellationToken);

        var context = new MicroflowValidationContext
        {
            ResourceId = id,
            Schema = schema,
            SchemaModel = schema.ValueKind == JsonValueKind.Object ? _schemaReader.Read(schema) : new MicroflowSchemaModel { Root = schema },
            Metadata = metadata,
            Mode = mode,
            IncludeWarnings = request.IncludeWarnings ?? true,
            IncludeInfo = request.IncludeInfo ?? false
        };

        ValidateRoot(context);
        if (schema.ValueKind == JsonValueKind.Object)
        {
            var variables = BuildVariableIndex(context);
            ValidateObjectCollection(context);
            ValidateFlows(context);
            ValidateEvents(context);
            ValidateDecisions(context);
            ValidateLoop(context, variables);
            ValidateActions(context, variables);
            ValidateMetadataReferences(context);
            ValidateVariables(context, variables);
            ValidateErrorHandling(context);
            ValidateReachability(context);
        }

        var issues = context.Issues
            .Where(issue => context.IncludeWarnings || issue.Severity != "warning")
            .Where(issue => context.IncludeInfo || issue.Severity != "info")
            .ToArray();

        return new ValidateMicroflowResponseDto
        {
            Issues = issues,
            Summary = new MicroflowValidationSummaryDto
            {
                ErrorCount = issues.Count(static issue => issue.Severity == "error"),
                WarningCount = issues.Count(static issue => issue.Severity == "warning"),
                InfoCount = issues.Count(static issue => issue.Severity == "info")
            },
            ServerValidatedAt = _clock.UtcNow
        };
    }

    private async Task<JsonElement> LoadCurrentSchemaAsync(string resourceId, CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        var snapshot = !string.IsNullOrWhiteSpace(resource.CurrentSchemaSnapshotId)
            ? await _schemaSnapshotRepository.GetByIdAsync(resource.CurrentSchemaSnapshotId, cancellationToken)
            : await _schemaSnapshotRepository.GetLatestByResourceIdAsync(resource.Id, cancellationToken);

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SchemaJson))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "微流当前 Schema 不存在。", 400);
        }

        return MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson);
    }

    private static void ValidateRoot(MicroflowValidationContext context)
    {
        var schema = context.Schema;
        if (schema.ValueKind != JsonValueKind.Object)
        {
            Add(context, MicroflowValidationCodes.RootSchemaInvalid, "schema 必须是对象。", "root", "schema");
            return;
        }

        foreach (var rootField in new[] { "schemaVersion", "id", "name", "objectCollection", "flows", "parameters" })
        {
            if (!schema.TryGetProperty(rootField, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                Add(context, MicroflowValidationCodes.RootSchemaInvalid, $"schema.{rootField} 不能为空。", "root", rootField);
            }
        }

        if (!schema.TryGetProperty("returnType", out var returnType) || returnType.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            Add(context, MicroflowValidationCodes.RootReturnTypeMissing, "returnType 不能为空。", "root", "returnType");
        }

        if (schema.TryGetProperty("nodes", out _) || schema.TryGetProperty("edges", out _) || schema.TryGetProperty("workflowJson", out _) || schema.TryGetProperty("flowgram", out _))
        {
            Add(context, MicroflowValidationCodes.RootSchemaInvalid, "不允许校验 FlowGram JSON。", "root", "schema");
        }

        foreach (var group in context.SchemaModel.Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)).GroupBy(p => p.Name, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                Add(context, MicroflowValidationCodes.RootParameterDuplicated, $"参数 {group.Key} 重复。", "root", group.First().FieldPath, parameterId: group.First().Id);
            }
        }

        foreach (var parameter in context.SchemaModel.Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Name) && !VariableNameRegex.IsMatch(p.Name)))
        {
            Add(context, MicroflowValidationCodes.VariableNameInvalid, $"参数名 {parameter.Name} 格式非法。", "root", $"{parameter.FieldPath}.name", parameterId: parameter.Id);
        }
    }

    private static void ValidateObjectCollection(MicroflowValidationContext context)
    {
        foreach (var group in context.SchemaModel.Objects.Where(o => !string.IsNullOrWhiteSpace(o.Id)).GroupBy(o => o.Id, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                Add(context, MicroflowValidationCodes.ObjectIdDuplicated, $"对象 id {group.Key} 重复。", "objectCollection", group.First().FieldPath, objectId: group.Key);
            }
        }

        foreach (var obj in context.SchemaModel.Objects)
        {
            if (string.IsNullOrWhiteSpace(obj.Id))
            {
                Add(context, MicroflowValidationCodes.ObjectMissing, "对象 id 不能为空。", "objectCollection", $"{obj.FieldPath}.id", collectionId: obj.CollectionId);
            }

            if (string.IsNullOrWhiteSpace(obj.Kind) || !KnownObjectKinds.Contains(obj.Kind))
            {
                Add(context, MicroflowValidationCodes.ObjectKindUnsupported, $"对象类型不支持：{obj.Kind}", "objectCollection", $"{obj.FieldPath}.kind", objectId: obj.Id, severity: LenientSeverity(context));
            }

            if (string.IsNullOrWhiteSpace(obj.OfficialType))
            {
                Add(context, MicroflowValidationCodes.ObjectKindUnsupported, "officialType 缺失。", "objectCollection", $"{obj.FieldPath}.officialType", objectId: obj.Id, severity: "warning");
            }

            if (obj.Kind.Equals("actionActivity", StringComparison.OrdinalIgnoreCase) && obj.Action is null)
            {
                Add(context, MicroflowValidationCodes.ActionMissing, "ActionActivity 必须包含 action。", "action", $"{obj.FieldPath}.action", objectId: obj.Id);
            }

            if (obj.Kind.Equals("loopedActivity", StringComparison.OrdinalIgnoreCase)
                && !obj.Raw.TryGetProperty("objectCollection", out _)
                && !obj.Raw.TryGetProperty("containedObjectCollection", out _)
                && !obj.Raw.TryGetProperty("loopObjectCollection", out _))
            {
                Add(context, MicroflowValidationCodes.LoopSourceMissing, "LoopedActivity 必须包含内部 objectCollection。", "loop", $"{obj.FieldPath}.objectCollection", objectId: obj.Id);
            }

            if (obj.Kind.Equals("parameterObject", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(obj.ParameterId) || context.SchemaModel.Parameters.All(p => p.Id != obj.ParameterId)))
            {
                Add(context, MicroflowValidationCodes.ObjectMissing, "ParameterObject 必须引用已有参数。", "objectCollection", $"{obj.FieldPath}.parameterId", objectId: obj.Id, parameterId: obj.ParameterId);
            }

            if (obj.InsideLoop && obj.Kind.Equals("startEvent", StringComparison.OrdinalIgnoreCase))
            {
                Add(context, MicroflowValidationCodes.LoopContainsStart, "Loop 内不允许 StartEvent。", "loop", obj.FieldPath, objectId: obj.Id);
            }

            if (obj.InsideLoop && obj.Kind.Equals("endEvent", StringComparison.OrdinalIgnoreCase))
            {
                Add(context, MicroflowValidationCodes.LoopContainsEnd, "Loop 内不允许 EndEvent。", "loop", obj.FieldPath, objectId: obj.Id);
            }
        }
    }

    private static void ValidateFlows(MicroflowValidationContext context)
    {
        var objects = context.SchemaModel.Objects.ToDictionary(o => o.Id, StringComparer.Ordinal);
        foreach (var group in context.SchemaModel.Flows.Where(f => !string.IsNullOrWhiteSpace(f.Id)).GroupBy(f => f.Id, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                Add(context, MicroflowValidationCodes.FlowDuplicated, $"Flow id {group.Key} 重复。", "flow", group.First().FieldPath, flowId: group.Key);
            }
        }

        foreach (var flow in context.SchemaModel.Flows)
        {
            if (string.IsNullOrWhiteSpace(flow.OriginObjectId))
            {
                Add(context, MicroflowValidationCodes.FlowOriginMissing, "flow.originObjectId 不能为空。", "flow", $"{flow.FieldPath}.originObjectId", flowId: flow.Id);
            }
            else if (!objects.ContainsKey(flow.OriginObjectId))
            {
                Add(context, MicroflowValidationCodes.FlowInvalidSource, "flow 起点对象不存在。", "flow", $"{flow.FieldPath}.originObjectId", flowId: flow.Id, relatedObjectIds: [flow.OriginObjectId]);
            }

            if (string.IsNullOrWhiteSpace(flow.DestinationObjectId))
            {
                Add(context, MicroflowValidationCodes.FlowDestinationMissing, "flow.destinationObjectId 不能为空。", "flow", $"{flow.FieldPath}.destinationObjectId", flowId: flow.Id);
            }
            else if (!objects.ContainsKey(flow.DestinationObjectId))
            {
                Add(context, MicroflowValidationCodes.FlowInvalidTarget, "flow 终点对象不存在。", "flow", $"{flow.FieldPath}.destinationObjectId", flowId: flow.Id, relatedObjectIds: [flow.DestinationObjectId]);
            }

            if (!objects.TryGetValue(flow.OriginObjectId ?? string.Empty, out var origin)
                || !objects.TryGetValue(flow.DestinationObjectId ?? string.Empty, out var destination))
            {
                continue;
            }

            var isAnnotationFlow = flow.Kind.Equals("annotation", StringComparison.OrdinalIgnoreCase);
            if (!isAnnotationFlow && (IsKind(origin, "annotation") || IsKind(destination, "annotation")))
            {
                Add(context, MicroflowValidationCodes.SequenceConnectsAnnotation, "SequenceFlow 不允许连接 Annotation。", "flow", flow.FieldPath, flowId: flow.Id, relatedObjectIds: [origin.Id, destination.Id]);
            }

            if (!isAnnotationFlow && (IsKind(origin, "parameterObject") || IsKind(destination, "parameterObject")))
            {
                Add(context, MicroflowValidationCodes.SequenceConnectsParameter, "SequenceFlow 不允许连接 ParameterObject。", "flow", flow.FieldPath, flowId: flow.Id, relatedObjectIds: [origin.Id, destination.Id]);
            }

            if (isAnnotationFlow && !IsKind(origin, "annotation") && !IsKind(destination, "annotation"))
            {
                Add(context, MicroflowValidationCodes.AnnotationFlowRequiresAnnotation, "AnnotationFlow 至少一端必须是 Annotation。", "flow", flow.FieldPath, flowId: flow.Id);
            }

            if (!string.Equals(origin.CollectionId, destination.CollectionId, StringComparison.Ordinal))
            {
                Add(context, MicroflowValidationCodes.FlowInvalidTarget, "Flow 不允许跨 root / loop collection 连接。", "flow", flow.FieldPath, flowId: flow.Id, relatedObjectIds: [origin.Id, destination.Id]);
            }

            if ((flow.EdgeKind?.Equals("decisionCondition", StringComparison.OrdinalIgnoreCase) ?? false) && !IsKind(origin, "exclusiveSplit"))
            {
                Add(context, MicroflowValidationCodes.FlowInvalidSource, "decisionCondition 只能从 ExclusiveSplit 发出。", "flow", $"{flow.FieldPath}.editor.edgeKind", flowId: flow.Id, objectId: origin.Id);
            }

            if ((flow.EdgeKind?.Equals("objectTypeCondition", StringComparison.OrdinalIgnoreCase) ?? false) && !IsKind(origin, "inheritanceSplit"))
            {
                Add(context, MicroflowValidationCodes.FlowInvalidSource, "objectTypeCondition 只能从 InheritanceSplit 发出。", "flow", $"{flow.FieldPath}.editor.edgeKind", flowId: flow.Id, objectId: origin.Id);
            }
        }

        foreach (var group in context.SchemaModel.Flows.Where(f => f.IsErrorHandler && !string.IsNullOrWhiteSpace(f.OriginObjectId)).GroupBy(f => f.OriginObjectId, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                Add(context, MicroflowValidationCodes.ErrorHandlerDuplicated, "同一对象最多只能有一条 errorHandler flow。", "errorHandling", group.First().FieldPath, objectId: group.Key, relatedFlowIds: group.Select(f => f.Id).ToArray());
            }
        }
    }

    private static void ValidateEvents(MicroflowValidationContext context)
    {
        var rootObjects = context.SchemaModel.Objects.Where(static o => !o.InsideLoop).ToArray();
        var rootStarts = rootObjects.Where(static o => IsKind(o, "startEvent")).ToArray();
        if (rootStarts.Length == 0)
        {
            Add(context, MicroflowValidationCodes.StartMissing, "root collection 必须包含一个 StartEvent。", "event", "objectCollection.objects");
        }
        else if (rootStarts.Length > 1)
        {
            foreach (var start in rootStarts)
            {
                Add(context, MicroflowValidationCodes.StartDuplicated, "root collection 只能包含一个 StartEvent。", "event", start.FieldPath, objectId: start.Id);
            }
        }

        if (!rootObjects.Any(static o => IsKind(o, "endEvent")))
        {
            Add(context, MicroflowValidationCodes.EndMissing, "root collection 至少需要一个 EndEvent。", "event", "objectCollection.objects", severity: ModeSeverity(context, warningInEdit: true));
        }

        foreach (var obj in context.SchemaModel.Objects)
        {
            var incoming = Incoming(context, obj.Id).ToArray();
            var outgoing = Outgoing(context, obj.Id).ToArray();
            if (IsKind(obj, "startEvent"))
            {
                if (incoming.Length > 0)
                {
                    Add(context, MicroflowValidationCodes.StartHasIncoming, "StartEvent 不允许 incoming flow。", "event", obj.FieldPath, objectId: obj.Id, relatedFlowIds: incoming.Select(f => f.Id).ToArray());
                }

                if (!obj.InsideLoop && outgoing.Length == 0)
                {
                    Add(context, MicroflowValidationCodes.StartNoOutgoing, "StartEvent 必须有 outgoing flow。", "event", obj.FieldPath, objectId: obj.Id);
                }
            }

            if (IsKind(obj, "endEvent"))
            {
                if (outgoing.Length > 0)
                {
                    Add(context, MicroflowValidationCodes.EndHasOutgoing, "EndEvent 不允许 outgoing flow。", "event", obj.FieldPath, objectId: obj.Id, relatedFlowIds: outgoing.Select(f => f.Id).ToArray());
                }

                ValidateEndReturn(context, obj);
            }

            if (IsKind(obj, "breakEvent") || IsKind(obj, "continueEvent"))
            {
                if (!obj.InsideLoop)
                {
                    Add(context, IsKind(obj, "breakEvent") ? MicroflowValidationCodes.BreakOutsideLoop : MicroflowValidationCodes.ContinueOutsideLoop, "Break/Continue 只能在 Loop 内使用。", "event", obj.FieldPath, objectId: obj.Id);
                }

                if (outgoing.Length > 0)
                {
                    Add(context, IsKind(obj, "breakEvent") ? MicroflowValidationCodes.BreakHasOutgoing : MicroflowValidationCodes.ContinueHasOutgoing, "Break/Continue 不允许 outgoing flow。", "event", obj.FieldPath, objectId: obj.Id);
                }
            }

            if (IsKind(obj, "errorEvent") && outgoing.Length > 0)
            {
                Add(context, MicroflowValidationCodes.ErrorEventOutOfScope, "ErrorEvent 不允许 outgoing flow。", "event", obj.FieldPath, objectId: obj.Id);
            }
        }
    }

    private static void ValidateEndReturn(MicroflowValidationContext context, MicroflowObjectModel obj)
    {
        var returnKind = ReadKind(context.SchemaModel.ReturnType);
        var hasReturnValue = obj.Raw.TryGetProperty("returnValue", out var returnValue) && !IsEmptyExpression(returnValue);
        if (returnKind == "void" && hasReturnValue)
        {
            Add(context, MicroflowValidationCodes.EndReturnValueNotAllowed, "void 返回类型不允许 EndEvent.returnValue。", "event", $"{obj.FieldPath}.returnValue", objectId: obj.Id);
        }
        else if (returnKind != "void" && !hasReturnValue)
        {
            Add(context, MicroflowValidationCodes.EndReturnValueRequired, "非 void 返回类型必须提供 EndEvent.returnValue。", "event", $"{obj.FieldPath}.returnValue", objectId: obj.Id);
        }
    }

    private static void ValidateDecisions(MicroflowValidationContext context)
    {
        foreach (var decision in context.SchemaModel.Objects.Where(o => IsKind(o, "exclusiveSplit")))
        {
            var splitCondition = decision.Raw.TryGetProperty("splitCondition", out var condition) ? condition : default;
            var resultType = MicroflowSchemaReader.ReadStringByPath(splitCondition, "resultType") ?? MicroflowSchemaReader.ReadStringByPath(splitCondition, "type");
            if (!HasExpression(splitCondition, "expression"))
            {
                Add(context, MicroflowValidationCodes.DecisionExpressionRequired, "Decision expression 不能为空。", "decision", $"{decision.FieldPath}.splitCondition.expression", objectId: decision.Id);
            }

            var outgoing = Outgoing(context, decision.Id).Where(f => !f.IsErrorHandler).ToArray();
            if (outgoing.Length == 0)
            {
                Add(context, MicroflowValidationCodes.DecisionBranchMissing, "Decision 至少需要一个分支。", "decision", decision.FieldPath, objectId: decision.Id, severity: ModeSeverity(context, warningInEdit: true));
            }

            if (string.Equals(resultType, "boolean", StringComparison.OrdinalIgnoreCase))
            {
                var cases = outgoing.SelectMany(ReadCaseValues).ToArray();
                if (!cases.Contains("true", StringComparer.OrdinalIgnoreCase))
                {
                    Add(context, MicroflowValidationCodes.DecisionBooleanTrueMissing, "Boolean Decision 缺少 true 分支。", "decision", "caseValues", objectId: decision.Id, severity: ModeSeverity(context, warningInEdit: true));
                }

                if (!cases.Contains("false", StringComparer.OrdinalIgnoreCase))
                {
                    Add(context, MicroflowValidationCodes.DecisionBooleanFalseMissing, "Boolean Decision 缺少 false 分支。", "decision", "caseValues", objectId: decision.Id, severity: ModeSeverity(context, warningInEdit: true));
                }
            }

            var enumName = MicroflowSchemaReader.ReadStringByPath(splitCondition, "enumerationQualifiedName");
            if (!string.IsNullOrWhiteSpace(enumName))
            {
                var enumeration = context.Metadata.Enumerations.FirstOrDefault(e => e.QualifiedName == enumName);
                if (enumeration is null)
                {
                    Add(context, MicroflowValidationCodes.DecisionEnumerationMissing, $"枚举不存在：{enumName}", "decision", $"{decision.FieldPath}.splitCondition.enumerationQualifiedName", objectId: decision.Id);
                    continue;
                }

                foreach (var value in outgoing.SelectMany(ReadCaseValues).Where(v => v is not "empty" and not "noCase"))
                {
                    if (enumeration.Values.All(v => v.Key != value))
                    {
                        Add(context, MicroflowValidationCodes.DecisionEnumerationValueInvalid, $"枚举值不存在：{value}", "decision", "caseValues", objectId: decision.Id);
                    }
                }
            }
        }

        foreach (var objectType in context.SchemaModel.Objects.Where(o => IsKind(o, "inheritanceSplit")))
        {
            var input = MicroflowSchemaReader.ReadStringByPath(objectType.Raw, "inputObjectVariableName");
            var generalized = MicroflowSchemaReader.ReadStringByPath(objectType.Raw, "generalizedEntityQualifiedName");
            if (string.IsNullOrWhiteSpace(input))
            {
                Add(context, MicroflowValidationCodes.ObjectTypeInputMissing, "ObjectType Decision inputObjectVariableName 不能为空。", "decision", $"{objectType.FieldPath}.inputObjectVariableName", objectId: objectType.Id);
            }

            var entity = string.IsNullOrWhiteSpace(generalized) ? null : context.Metadata.Entities.FirstOrDefault(e => e.QualifiedName == generalized);
            if (entity is null)
            {
                Add(context, MicroflowValidationCodes.ObjectTypeEntityMissing, $"ObjectType generalized entity 不存在：{generalized}", "decision", $"{objectType.FieldPath}.generalizedEntityQualifiedName", objectId: objectType.Id);
                continue;
            }

            foreach (var value in Outgoing(context, objectType.Id).SelectMany(ReadCaseValues).Where(v => v is not "empty" and not "fallback" and not "noCase"))
            {
                if (!entity.Specializations.Contains(value, StringComparer.Ordinal) && context.Metadata.Entities.All(e => e.Generalization != entity.QualifiedName || e.QualifiedName != value))
                {
                    Add(context, MicroflowValidationCodes.ObjectTypeSpecializationInvalid, $"ObjectType specialization 不合法：{value}", "decision", "caseValues", objectId: objectType.Id);
                }
            }
        }
    }

    private static void ValidateLoop(MicroflowValidationContext context, Dictionary<string, VariableInfo> variables)
    {
        foreach (var loop in context.SchemaModel.Objects.Where(o => IsKind(o, "loopedActivity")))
        {
            if (!loop.Raw.TryGetProperty("loopSource", out var source) || source.ValueKind != JsonValueKind.Object)
            {
                Add(context, MicroflowValidationCodes.LoopSourceMissing, "LoopSource 不能为空。", "loop", $"{loop.FieldPath}.loopSource", objectId: loop.Id);
                continue;
            }

            var kind = MicroflowSchemaReader.ReadString(source, "kind");
            if (string.Equals(kind, "iterableList", StringComparison.OrdinalIgnoreCase))
            {
                var listVariable = MicroflowSchemaReader.ReadString(source, "listVariableName");
                if (string.IsNullOrWhiteSpace(listVariable))
                {
                    Add(context, MicroflowValidationCodes.LoopListVariableMissing, "Loop listVariableName 不能为空。", "loop", $"{loop.FieldPath}.loopSource.listVariableName", objectId: loop.Id);
                }
                else if (!variables.ContainsKey(listVariable))
                {
                    Add(context, MicroflowValidationCodes.VariableNotFound, $"Loop listVariableName 不存在：{listVariable}", "variables", $"{loop.FieldPath}.loopSource.listVariableName", objectId: loop.Id);
                }

                var iterator = MicroflowSchemaReader.ReadString(source, "iteratorVariableName");
                if (string.IsNullOrWhiteSpace(iterator) || !VariableNameRegex.IsMatch(iterator))
                {
                    Add(context, MicroflowValidationCodes.LoopIteratorNameMissing, "Loop iteratorVariableName 不能为空且必须合法。", "loop", $"{loop.FieldPath}.loopSource.iteratorVariableName", objectId: loop.Id);
                }
            }

            if (string.Equals(kind, "whileCondition", StringComparison.OrdinalIgnoreCase) && !HasExpression(source, "expression"))
            {
                Add(context, MicroflowValidationCodes.LoopWhileExpressionRequired, "While Loop expression 不能为空。", "loop", $"{loop.FieldPath}.loopSource.expression", objectId: loop.Id);
            }
        }
    }

    private void ValidateActions(MicroflowValidationContext context, Dictionary<string, VariableInfo> variables)
    {
        foreach (var obj in context.SchemaModel.Objects.Where(o => IsKind(o, "actionActivity")))
        {
            var action = obj.Action;
            if (action is null)
            {
                continue;
            }

            var support = _actionSupportMatrix.Resolve(
                action.Kind,
                action.OfficialType,
                new MicroflowExecutionPlanLoadOptions { Mode = context.Mode });
            if (!string.Equals(support.SupportLevel, MicroflowRuntimeSupportLevel.Supported, StringComparison.OrdinalIgnoreCase))
            {
                Add(
                    context,
                    MicroflowValidationCodes.ActionUnsupported,
                    support.Message,
                    "action",
                    $"{action.FieldPath}.kind",
                    objectId: obj.Id,
                    actionId: action.Id,
                    severity: ActionSupportSeverity(context, support.SupportLevel));
                continue;
            }

            switch (action.Kind)
            {
                case "retrieve":
                    RequireActionField(context, obj, action, "outputVariableName", MicroflowValidationCodes.RetrieveOutputMissing);
                    if (action.Raw.TryGetProperty("retrieveSource", out var retrieveSource))
                    {
                        var sourceKind = MicroflowSchemaReader.ReadString(retrieveSource, "kind");
                        if (string.Equals(sourceKind, "database", StringComparison.OrdinalIgnoreCase))
                        {
                            RequireMetadataEntity(context, obj, action, MicroflowSchemaReader.ReadString(retrieveSource, "entityQualifiedName"), $"{action.FieldPath}.retrieveSource.entityQualifiedName", MicroflowValidationCodes.RetrieveEntityMissing);
                        }
                        else if (string.Equals(sourceKind, "association", StringComparison.OrdinalIgnoreCase))
                        {
                            RequireActionField(context, obj, action, "retrieveSource.associationQualifiedName", MicroflowValidationCodes.RetrieveAssociationMissing);
                            RequireMetadataAssociation(context, obj, action, MicroflowSchemaReader.ReadString(retrieveSource, "associationQualifiedName"), $"{action.FieldPath}.retrieveSource.associationQualifiedName");
                            RequireExpressionVariable(context, obj, action, MicroflowSchemaReader.ReadString(retrieveSource, "startVariableName"), $"{action.FieldPath}.retrieveSource.startVariableName", variables);
                        }
                    }
                    else
                    {
                        Add(context, MicroflowValidationCodes.ActionRequiredFieldMissing, "retrieveSource 不能为空。", "action", $"{action.FieldPath}.retrieveSource", objectId: obj.Id, actionId: action.Id);
                    }

                    break;
                case "createObject":
                    RequireMetadataEntity(context, obj, action, MicroflowSchemaReader.ReadString(action.Raw, "entityQualifiedName"), $"{action.FieldPath}.entityQualifiedName", MicroflowValidationCodes.CreateObjectEntityMissing);
                    RequireActionField(context, obj, action, "outputVariableName", MicroflowValidationCodes.CreateObjectOutputMissing);
                    ValidateMemberChanges(context, obj, action);
                    break;
                case "changeMembers":
                    RequireExpressionVariable(context, obj, action, MicroflowSchemaReader.ReadString(action.Raw, "changeVariableName"), $"{action.FieldPath}.changeVariableName", variables, MicroflowValidationCodes.ChangeObjectTargetMissing);
                    ValidateMemberChanges(context, obj, action, requireAny: true);
                    break;
                case "commit":
                    RequireExpressionVariable(context, obj, action, MicroflowSchemaReader.ReadString(action.Raw, "objectOrListVariableName"), $"{action.FieldPath}.objectOrListVariableName", variables, MicroflowValidationCodes.CommitVariableMissing);
                    break;
                case "delete":
                    RequireExpressionVariable(context, obj, action, MicroflowSchemaReader.ReadString(action.Raw, "objectOrListVariableName"), $"{action.FieldPath}.objectOrListVariableName", variables, MicroflowValidationCodes.DeleteVariableMissing);
                    break;
                case "rollback":
                    RequireExpressionVariable(context, obj, action, MicroflowSchemaReader.ReadString(action.Raw, "objectOrListVariableName"), $"{action.FieldPath}.objectOrListVariableName", variables, MicroflowValidationCodes.RollbackVariableMissing);
                    break;
                case "createVariable":
                    RequireVariableName(context, obj, action, MicroflowSchemaReader.ReadString(action.Raw, "variableName"), $"{action.FieldPath}.variableName");
                    if (!action.Raw.TryGetProperty("dataType", out _))
                    {
                        Add(context, MicroflowValidationCodes.ActionRequiredFieldMissing, "dataType 不能为空。", "action", $"{action.FieldPath}.dataType", objectId: obj.Id, actionId: action.Id);
                    }

                    ValidateExpression(context, obj, action, ReadExpressionText(action.Raw, "initialValue"), $"{action.FieldPath}.initialValue", variables, required: false);
                    break;
                case "changeVariable":
                    RequireExpressionVariable(context, obj, action, MicroflowSchemaReader.ReadString(action.Raw, "targetVariableName"), $"{action.FieldPath}.targetVariableName", variables);
                    ValidateExpression(context, obj, action, ReadExpressionText(action.Raw, "newValueExpression"), $"{action.FieldPath}.newValueExpression", variables, required: true);
                    break;
                case "callMicroflow":
                    var targetId = MicroflowSchemaReader.ReadString(action.Raw, "targetMicroflowId");
                    if (string.IsNullOrWhiteSpace(targetId))
                    {
                        Add(context, MicroflowValidationCodes.CallMicroflowTargetMissing, "targetMicroflowId 不能为空。", "action", $"{action.FieldPath}.targetMicroflowId", objectId: obj.Id, actionId: action.Id);
                    }
                    else if (context.Metadata.Microflows.All(mf => mf.Id != targetId && mf.QualifiedName != targetId))
                    {
                        Add(context, MicroflowValidationCodes.MetadataMicroflowNotFound, $"目标微流不存在：{targetId}", "metadata", $"{action.FieldPath}.targetMicroflowId", objectId: obj.Id, actionId: action.Id);
                    }

                    ValidateParameterMappingExpressions(context, obj, action, variables);
                    break;
                case "restCall":
                    RequireActionField(context, obj, action, "request.method", MicroflowValidationCodes.RestMethodMissing);
                    ValidateExpression(context, obj, action, ReadExpressionTextByPath(action.Raw, "request", "urlExpression"), $"{action.FieldPath}.request.urlExpression", variables, required: true, code: MicroflowValidationCodes.RestUrlMissing);
                    break;
                case "logMessage":
                    var text = MicroflowSchemaReader.ReadStringByPath(action.Raw, "template", "text") ?? MicroflowSchemaReader.ReadString(action.Raw, "text");
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        Add(context, MicroflowValidationCodes.LogMessageEmpty, "LogMessage 文本不能为空。", "action", $"{action.FieldPath}.template.text", objectId: obj.Id, actionId: action.Id);
                    }

                    break;
            }
        }
    }

    private static void ValidateMetadataReferences(MicroflowValidationContext context)
    {
        foreach (var parameter in context.SchemaModel.Parameters)
        {
            ValidateDataTypeReference(context, parameter.Type, parameter.FieldPath, parameterId: parameter.Id);
        }

        if (context.SchemaModel.ReturnType.HasValue)
        {
            ValidateDataTypeReference(context, context.SchemaModel.ReturnType.Value, "returnType");
        }
    }

    private static void ValidateVariables(MicroflowValidationContext context, Dictionary<string, VariableInfo> variables)
    {
        foreach (var group in variables.Values.Where(v => !v.IsSystem).GroupBy(v => v.Name, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                var first = group.First();
                Add(context, MicroflowValidationCodes.VariableDuplicated, $"变量 {first.Name} 重复。", "variables", first.FieldPath, objectId: first.ObjectId, actionId: first.ActionId);
            }
        }

        foreach (var variable in variables.Values.Where(v => !v.IsSystem && !VariableNameRegex.IsMatch(v.Name)))
        {
            Add(context, MicroflowValidationCodes.VariableNameInvalid, $"变量名 {variable.Name} 格式非法。", "variables", variable.FieldPath, objectId: variable.ObjectId, actionId: variable.ActionId);
        }
    }

    private static void ValidateErrorHandling(MicroflowValidationContext context)
    {
        foreach (var obj in context.SchemaModel.Objects.Where(o => o.Action is not null))
        {
            var action = obj.Action!;
            var errorHandling = MicroflowSchemaReader.ReadString(action.Raw, "errorHandlingType") ?? MicroflowSchemaReader.ReadStringByPath(action.Raw, "errorHandling", "type");
            var errorFlows = Outgoing(context, obj.Id).Where(f => f.IsErrorHandler).ToArray();
            if (string.Equals(errorHandling, "rollback", StringComparison.OrdinalIgnoreCase) && errorFlows.Length > 0)
            {
                Add(context, MicroflowValidationCodes.ErrorHandlerRollbackHasFlow, "rollback error handling 不应配置 errorHandler flow。", "errorHandling", action.FieldPath, objectId: obj.Id, actionId: action.Id, relatedFlowIds: errorFlows.Select(f => f.Id).ToArray(), severity: "warning");
            }

            if ((string.Equals(errorHandling, "customWithRollback", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(errorHandling, "customWithoutRollback", StringComparison.OrdinalIgnoreCase))
                && errorFlows.Length == 0)
            {
                Add(context, MicroflowValidationCodes.ErrorHandlerWithRollbackMissingFlow, "custom error handling 必须配置 errorHandler flow。", "errorHandling", action.FieldPath, objectId: obj.Id, actionId: action.Id);
            }

            if (string.Equals(errorHandling, "continue", StringComparison.OrdinalIgnoreCase)
                && action.Kind is not ("callMicroflow" or "restCall"))
            {
                Add(context, MicroflowValidationCodes.ErrorHandlerContinueNotAllowed, "当前 action 不允许 continue error handling。", "errorHandling", action.FieldPath, objectId: obj.Id, actionId: action.Id);
            }
        }
    }

    private static void ValidateReachability(MicroflowValidationContext context)
    {
        var starts = context.SchemaModel.Objects.Where(o => !o.InsideLoop && IsKind(o, "startEvent")).ToArray();
        if (starts.Length == 0)
        {
            return;
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(starts.Select(s => s.Id));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!reachable.Add(id))
            {
                continue;
            }

            foreach (var flow in context.SchemaModel.Flows.Where(f => !f.IsErrorHandler && f.OriginObjectId == id && !string.IsNullOrWhiteSpace(f.DestinationObjectId)))
            {
                queue.Enqueue(flow.DestinationObjectId!);
            }
        }

        foreach (var obj in context.SchemaModel.Objects.Where(IsExecutableObject))
        {
            if (!reachable.Contains(obj.Id) && !obj.InsideLoop)
            {
                Add(context, MicroflowValidationCodes.ObjectUnreachable, "对象不可从 StartEvent 到达。", "reachability", obj.FieldPath, objectId: obj.Id, severity: ModeSeverity(context, warningInEdit: true));
            }

            if (!IsTerminal(obj) && !Outgoing(context, obj.Id).Any(f => !f.IsErrorHandler) && !obj.InsideLoop)
            {
                Add(context, MicroflowValidationCodes.FlowDeadEnd, "可执行对象没有 normal outgoing flow。", "reachability", obj.FieldPath, objectId: obj.Id, severity: ModeSeverity(context, warningInEdit: true));
            }
        }
    }

    private static Dictionary<string, VariableInfo> BuildVariableIndex(MicroflowValidationContext context)
    {
        var variables = new Dictionary<string, VariableInfo>(StringComparer.Ordinal);
        foreach (var parameter in context.SchemaModel.Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
        {
            variables[parameter.Name] = new VariableInfo(parameter.Name, parameter.Type, parameter.FieldPath, null, null, false);
        }

        variables["currentUser"] = new VariableInfo("currentUser", MicroflowSeedMetadataCatalog.Type("object"), "system.$currentUser", null, null, true);
        variables["currentIndex"] = new VariableInfo("currentIndex", MicroflowSeedMetadataCatalog.Type("integer"), "system.$currentIndex", null, null, true);
        variables["latestError"] = new VariableInfo("latestError", MicroflowSeedMetadataCatalog.Type("object"), "system.$latestError", null, null, true);

        foreach (var obj in context.SchemaModel.Objects.Where(o => o.Action is not null))
        {
            var action = obj.Action!;
            foreach (var name in OutputVariableNames(action))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    variables[name] = new VariableInfo(name, InferOutputType(context, action), $"{action.FieldPath}.outputVariableName", obj.Id, action.Id, false);
                }
            }
        }

        return variables;
    }

    private static IEnumerable<string?> OutputVariableNames(MicroflowActionModel action)
    {
        yield return MicroflowSchemaReader.ReadString(action.Raw, "outputVariableName");
        yield return MicroflowSchemaReader.ReadString(action.Raw, "variableName");
        yield return MicroflowSchemaReader.ReadStringByPath(action.Raw, "returnValue", "outputVariableName");
        yield return MicroflowSchemaReader.ReadStringByPath(action.Raw, "response", "handling", "outputVariableName");
        yield return MicroflowSchemaReader.ReadString(action.Raw, "statusCodeVariableName");
        yield return MicroflowSchemaReader.ReadString(action.Raw, "headersVariableName");
    }

    private static JsonElement InferOutputType(MicroflowValidationContext context, MicroflowActionModel action)
    {
        if (action.Kind == "createVariable" && action.Raw.TryGetProperty("dataType", out var dataType))
        {
            return dataType.Clone();
        }

        if (action.Kind == "createObject")
        {
            var entity = MicroflowSchemaReader.ReadString(action.Raw, "entityQualifiedName");
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["kind"] = "object", ["entityQualifiedName"] = entity ?? string.Empty }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        if (action.Kind == "callMicroflow")
        {
            var target = MicroflowSchemaReader.ReadString(action.Raw, "targetMicroflowId");
            var microflow = context.Metadata.Microflows.FirstOrDefault(m => m.Id == target || m.QualifiedName == target);
            return microflow?.ReturnType ?? MicroflowSeedMetadataCatalog.UnknownType("call microflow return type unknown");
        }

        return action.Kind == "retrieve"
            ? JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["kind"] = "list", ["itemType"] = new { kind = "object" } }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            : MicroflowSeedMetadataCatalog.UnknownType("action output type unknown");
    }

    private static void ValidateMemberChanges(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, bool requireAny = false)
    {
        if (!action.Raw.TryGetProperty("memberChanges", out var changes) || changes.ValueKind != JsonValueKind.Array || changes.GetArrayLength() == 0)
        {
            if (requireAny)
            {
                Add(context, MicroflowValidationCodes.ChangeObjectNoMembers, "memberChanges 至少需要一项。", "action", $"{action.FieldPath}.memberChanges", objectId: obj.Id, actionId: action.Id);
            }

            return;
        }

        var index = 0;
        foreach (var change in changes.EnumerateArray())
        {
            var member = MicroflowSchemaReader.ReadString(change, "memberQualifiedName");
            if (string.IsNullOrWhiteSpace(member))
            {
                Add(context, MicroflowValidationCodes.MetadataAttributeNotFound, "memberQualifiedName 不能为空。", "metadata", $"{action.FieldPath}.memberChanges.{index}.memberQualifiedName", objectId: obj.Id, actionId: action.Id);
            }
            else if (!AttributeExists(context.Metadata, member))
            {
                Add(context, MicroflowValidationCodes.MetadataAttributeNotFound, $"属性不存在：{member}", "metadata", $"{action.FieldPath}.memberChanges.{index}.memberQualifiedName", objectId: obj.Id, actionId: action.Id);
            }

            if (!string.Equals(MicroflowSchemaReader.ReadString(change, "assignmentKind"), "clear", StringComparison.OrdinalIgnoreCase))
            {
                ValidateExpression(context, obj, action, ReadExpressionText(change, "valueExpression"), $"{action.FieldPath}.memberChanges.{index}.valueExpression", BuildVariableIndex(context), required: true);
            }

            index++;
        }
    }

    private static void ValidateParameterMappingExpressions(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, Dictionary<string, VariableInfo> variables)
    {
        if (!action.Raw.TryGetProperty("parameterMappings", out var mappings) || mappings.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var mapping in mappings.EnumerateArray())
        {
            ValidateExpression(context, obj, action, ReadExpressionText(mapping, "argumentExpression"), $"{action.FieldPath}.parameterMappings.{index}.argumentExpression", variables, required: true);
            index++;
        }
    }

    private static void RequireActionField(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, string path, string code)
    {
        var value = ReadStringByDottedPath(action.Raw, path);
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(context, code, $"{path} 不能为空。", "action", $"{action.FieldPath}.{path}", objectId: obj.Id, actionId: action.Id);
        }
    }

    private static void RequireMetadataEntity(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, string? qualifiedName, string fieldPath, string missingCode = MicroflowValidationCodes.MetadataEntityNotFound)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            Add(context, missingCode, "entityQualifiedName 不能为空。", "metadata", fieldPath, objectId: obj.Id, actionId: action.Id);
        }
        else if (context.Metadata.Entities.All(e => e.QualifiedName != qualifiedName))
        {
            Add(context, MicroflowValidationCodes.MetadataEntityNotFound, $"实体不存在：{qualifiedName}", "metadata", fieldPath, objectId: obj.Id, actionId: action.Id);
        }
    }

    private static void RequireMetadataAssociation(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, string? qualifiedName, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName) || context.Metadata.Associations.All(a => a.QualifiedName != qualifiedName))
        {
            Add(context, MicroflowValidationCodes.MetadataAssociationNotFound, $"关联不存在：{qualifiedName}", "metadata", fieldPath, objectId: obj.Id, actionId: action.Id);
        }
    }

    private static void RequireVariableName(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, string? name, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(name) || !VariableNameRegex.IsMatch(name))
        {
            Add(context, MicroflowValidationCodes.VariableNameInvalid, "变量名不能为空且必须合法。", "variables", fieldPath, objectId: obj.Id, actionId: action.Id);
        }
    }

    private static void RequireExpressionVariable(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, string? name, string fieldPath, Dictionary<string, VariableInfo> variables, string code = MicroflowValidationCodes.VariableNotFound)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Add(context, code, "变量名不能为空。", "variables", fieldPath, objectId: obj.Id, actionId: action.Id);
        }
        else if (!variables.ContainsKey(name))
        {
            Add(context, MicroflowValidationCodes.VariableNotFound, $"变量不存在：{name}", "variables", fieldPath, objectId: obj.Id, actionId: action.Id);
        }
    }

    private static void ValidateExpression(MicroflowValidationContext context, MicroflowObjectModel obj, MicroflowActionModel action, string? expression, string fieldPath, Dictionary<string, VariableInfo> variables, bool required, string code = MicroflowValidationCodes.ExprRequired)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            if (required)
            {
                Add(context, code, "表达式不能为空。", "expression", fieldPath, objectId: obj.Id, actionId: action.Id);
            }

            return;
        }

        if (expression == "$" || expression.EndsWith("/", StringComparison.Ordinal) || expression.Count(c => c == '\'') % 2 != 0 || expression.Count(c => c == '"') % 2 != 0)
        {
            Add(context, MicroflowValidationCodes.ExprParseError, "表达式格式不完整。", "expression", fieldPath, objectId: obj.Id, actionId: action.Id);
        }

        foreach (Match match in VariableReferenceRegex.Matches(expression))
        {
            var token = match.Value.TrimStart('$');
            var parts = token.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            if (!variables.TryGetValue(parts[0], out var variable))
            {
                Add(context, MicroflowValidationCodes.ExprUnknownVariable, $"表达式引用未知变量：${parts[0]}", "expression", fieldPath, objectId: obj.Id, actionId: action.Id);
                continue;
            }

            if (parts.Length > 1 && !MemberExists(context.Metadata, variable.Type, parts[1]))
            {
                Add(context, MicroflowValidationCodes.ExprMemberNotFound, $"成员不存在：{parts[1]}", "expression", fieldPath, objectId: obj.Id, actionId: action.Id);
            }
        }
    }

    private static void ValidateDataTypeReference(MicroflowValidationContext context, JsonElement dataType, string fieldPath, string? parameterId = null)
    {
        var kind = ReadKind(dataType);
        if (kind == "object")
        {
            var entity = MicroflowSchemaReader.ReadString(dataType, "entityQualifiedName");
            if (!string.IsNullOrWhiteSpace(entity) && context.Metadata.Entities.All(e => e.QualifiedName != entity))
            {
                Add(context, MicroflowValidationCodes.MetadataEntityNotFound, $"实体不存在：{entity}", "metadata", fieldPath, parameterId: parameterId);
            }
        }
        else if (kind == "enumeration")
        {
            var enumeration = MicroflowSchemaReader.ReadString(dataType, "enumerationQualifiedName");
            if (!string.IsNullOrWhiteSpace(enumeration) && context.Metadata.Enumerations.All(e => e.QualifiedName != enumeration))
            {
                Add(context, MicroflowValidationCodes.MetadataEnumerationNotFound, $"枚举不存在：{enumeration}", "metadata", fieldPath, parameterId: parameterId);
            }
        }
        else if (kind == "list" && dataType.TryGetProperty("itemType", out var itemType))
        {
            ValidateDataTypeReference(context, itemType, $"{fieldPath}.itemType", parameterId);
        }
    }

    private static bool MemberExists(MicroflowMetadataCatalogDto metadata, JsonElement variableType, string member)
    {
        var entityName = MicroflowSchemaReader.ReadString(variableType, "entityQualifiedName");
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return true;
        }

        var entity = metadata.Entities.FirstOrDefault(e => e.QualifiedName == entityName);
        return entity is null || entity.Attributes.Any(a => a.Name == member || a.QualifiedName.EndsWith($".{member}", StringComparison.Ordinal));
    }

    private static bool AttributeExists(MicroflowMetadataCatalogDto metadata, string qualifiedName)
        => metadata.Entities.SelectMany(entity => entity.Attributes).Any(attribute => attribute.QualifiedName == qualifiedName || attribute.Name == qualifiedName);

    private static IEnumerable<string> ReadCaseValues(MicroflowFlowModel flow)
    {
        foreach (var caseValue in flow.CaseValues)
        {
            var value = MicroflowSchemaReader.ReadString(caseValue, "value")
                ?? MicroflowSchemaReader.ReadString(caseValue, "persistedValue")
                ?? MicroflowSchemaReader.ReadString(caseValue, "entityQualifiedName")
                ?? MicroflowSchemaReader.ReadString(caseValue, "kind");
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static bool HasExpression(JsonElement element, string propertyName)
        => !string.IsNullOrWhiteSpace(ReadExpressionText(element, propertyName));

    private static string? ReadExpressionText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return MicroflowSchemaReader.ReadString(value, "raw")
            ?? MicroflowSchemaReader.ReadString(value, "text")
            ?? MicroflowSchemaReader.ReadString(value, "expression");
    }

    private static string? ReadExpressionTextByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path[..^1])
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return ReadExpressionText(current, path[^1]);
    }

    private static string? ReadStringByDottedPath(JsonElement element, string path)
    {
        var current = element;
        var parts = path.Split('.');
        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
    }

    private static bool IsEmptyExpression(JsonElement element)
        => element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString()))
            || (element.ValueKind == JsonValueKind.Object
                && string.IsNullOrWhiteSpace(MicroflowSchemaReader.ReadString(element, "raw"))
                && string.IsNullOrWhiteSpace(MicroflowSchemaReader.ReadString(element, "text")));

    private static string ReadKind(JsonElement? element)
    {
        if (!element.HasValue)
        {
            return "void";
        }

        return MicroflowSchemaReader.ReadString(element.Value, "kind") ?? "unknown";
    }

    private static bool IsKind(MicroflowObjectModel obj, string kind)
        => string.Equals(obj.Kind, kind, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<MicroflowFlowModel> Incoming(MicroflowValidationContext context, string objectId)
        => context.SchemaModel.Flows.Where(flow => flow.DestinationObjectId == objectId);

    private static IEnumerable<MicroflowFlowModel> Outgoing(MicroflowValidationContext context, string objectId)
        => context.SchemaModel.Flows.Where(flow => flow.OriginObjectId == objectId);

    private static bool IsExecutableObject(MicroflowObjectModel obj)
        => !IsKind(obj, "annotation") && !IsKind(obj, "parameterObject");

    private static bool IsTerminal(MicroflowObjectModel obj)
        => IsKind(obj, "endEvent") || IsKind(obj, "errorEvent") || IsKind(obj, "breakEvent") || IsKind(obj, "continueEvent");

    private static string NormalizeMode(string? mode)
        => mode is "save" or "publish" or "testRun" ? mode : "edit";

    private static string ModeSeverity(MicroflowValidationContext context, bool warningInEdit)
        => warningInEdit && context.Mode == "edit" ? "warning" : "error";

    private static string LenientSeverity(MicroflowValidationContext context)
        => context.Mode is "publish" or "testRun" ? "error" : "warning";

    private static string ActionSupportSeverity(MicroflowValidationContext context, string supportLevel)
    {
        if (string.Equals(supportLevel, MicroflowRuntimeSupportLevel.Deprecated, StringComparison.OrdinalIgnoreCase))
        {
            return "warning";
        }

        if (string.Equals(supportLevel, MicroflowRuntimeSupportLevel.RequiresConnector, StringComparison.OrdinalIgnoreCase))
        {
            return context.Mode is "publish" or "testRun" ? "error" : "warning";
        }

        return context.Mode is "publish" or "testRun" ? "error" : "warning";
    }

    private static void Add(
        MicroflowValidationContext context,
        string code,
        string message,
        string source,
        string? fieldPath,
        string severity = "error",
        string? objectId = null,
        string? flowId = null,
        string? actionId = null,
        string? parameterId = null,
        string? collectionId = null,
        IReadOnlyList<string>? relatedObjectIds = null,
        IReadOnlyList<string>? relatedFlowIds = null,
        string? details = null)
    {
        context.Issues.Add(new MicroflowValidationIssueDto
        {
            Id = StableId(code, objectId, flowId, actionId, parameterId, collectionId, fieldPath),
            Severity = severity,
            Code = code,
            Message = message,
            ObjectId = objectId,
            FlowId = flowId,
            ActionId = actionId,
            ParameterId = parameterId,
            CollectionId = collectionId,
            FieldPath = fieldPath,
            Source = source,
            RelatedObjectIds = relatedObjectIds ?? Array.Empty<string>(),
            RelatedFlowIds = relatedFlowIds ?? Array.Empty<string>(),
            Details = details
        });
    }

    private static string StableId(params string?[] parts)
    {
        var raw = string.Join("|", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private sealed record VariableInfo(string Name, JsonElement Type, string FieldPath, string? ObjectId, string? ActionId, bool IsSystem);
}
