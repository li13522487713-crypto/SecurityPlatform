using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowActionSupportMatrix : IMicroflowActionSupportMatrix
{
    private static readonly IReadOnlyDictionary<string, MicroflowActionExecutorDescriptor> Descriptors =
        MicroflowActionExecutorRegistry.BuiltInDescriptors().ToDictionary(descriptor => descriptor.ActionKind, StringComparer.OrdinalIgnoreCase);

    public MicroflowActionSupportDescriptor Resolve(string? actionKind, string? officialType, MicroflowExecutionPlanLoadOptions options)
    {
        if (string.IsNullOrWhiteSpace(actionKind))
        {
            return new MicroflowActionSupportDescriptor
            {
                SupportLevel = MicroflowRuntimeSupportLevel.Unsupported,
                Reason = "unsupported",
                Message = "Action kind is missing."
            };
        }

        if (Descriptors.TryGetValue(actionKind, out var descriptor))
        {
            var supportLevel = ToRuntimeSupportLevel(descriptor, options);
            var reason = string.Equals(supportLevel, MicroflowRuntimeSupportLevel.Supported, StringComparison.OrdinalIgnoreCase)
                ? descriptor.SupportLevel
                : descriptor.Reason;
            return new MicroflowActionSupportDescriptor
            {
                SupportLevel = supportLevel,
                Reason = reason,
                Message = descriptor.Reason
            };
        }

        if (Contains(officialType, "Nanoflow"))
        {
            return new MicroflowActionSupportDescriptor
            {
                SupportLevel = MicroflowRuntimeSupportLevel.NanoflowOnly,
                Reason = "nanoflowOnly",
                Message = "Nanoflow-only action is unsupported in Microflow runtime."
            };
        }

        if (Contains(officialType, "GenericAction"))
        {
            return new MicroflowActionSupportDescriptor
            {
                SupportLevel = MicroflowRuntimeSupportLevel.Unsupported,
                Reason = "genericUnsupported",
                Message = $"Generic action kind '{actionKind}' has no registered executor strategy."
            };
        }

        return new MicroflowActionSupportDescriptor
        {
            SupportLevel = MicroflowRuntimeSupportLevel.Unsupported,
            Reason = "unsupported",
            Message = $"Unsupported action kind: {actionKind}."
        };
    }

    private static string ToRuntimeSupportLevel(MicroflowActionExecutorDescriptor descriptor, MicroflowExecutionPlanLoadOptions options)
    {
        if (descriptor.RuntimeCategory == MicroflowActionRuntimeCategory.ConnectorBacked
            && !string.IsNullOrWhiteSpace(descriptor.ConnectorCapability)
            && !options.ConnectorCapabilities.Contains(descriptor.ConnectorCapability, StringComparer.OrdinalIgnoreCase))
        {
            return MicroflowRuntimeSupportLevel.RequiresConnector;
        }

        if (descriptor.RuntimeCategory == MicroflowActionRuntimeCategory.ConnectorBacked)
        {
            return descriptor.SupportLevel == MicroflowActionSupportLevel.Deprecated
                ? MicroflowRuntimeSupportLevel.Deprecated
                : MicroflowRuntimeSupportLevel.Supported;
        }

        return descriptor.SupportLevel switch
        {
            MicroflowActionSupportLevel.Supported or MicroflowActionSupportLevel.ModeledOnlyConverted => MicroflowRuntimeSupportLevel.Supported,
            MicroflowActionSupportLevel.RequiresConnector => MicroflowRuntimeSupportLevel.RequiresConnector,
            MicroflowActionSupportLevel.NanoflowOnly => MicroflowRuntimeSupportLevel.NanoflowOnly,
            MicroflowActionSupportLevel.Deprecated => MicroflowRuntimeSupportLevel.Deprecated,
            _ => MicroflowRuntimeSupportLevel.Unsupported
        };
    }

    private static bool Contains(string? value, string token)
        => value?.Contains(token, StringComparison.OrdinalIgnoreCase) == true;
}

public sealed class MicroflowRuntimeDtoBuilder : IMicroflowRuntimeDtoBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowSchemaReader _schemaReader;
    private readonly IMicroflowActionSupportMatrix _supportMatrix;
    private readonly IMicroflowClock _clock;

    public MicroflowRuntimeDtoBuilder(
        IMicroflowSchemaReader schemaReader,
        IMicroflowActionSupportMatrix supportMatrix,
        IMicroflowClock clock)
    {
        _schemaReader = schemaReader;
        _supportMatrix = supportMatrix;
        _clock = clock;
    }

    public MicroflowRuntimeDto Build(JsonElement schema, MicroflowExecutionPlanLoadOptions options)
    {
        var model = _schemaReader.Read(schema);
        var diagnostics = new List<MicroflowExecutionDiagnosticDto>();
        var metadataRefs = new List<MicroflowExecutionMetadataRef>();
        var variables = new List<MicroflowExecutionVariableDeclaration>();
        var unsupported = new List<MicroflowUnsupportedActionDescriptor>();

        variables.Add(new MicroflowExecutionVariableDeclaration
        {
            Name = "currentUser",
            DataTypeJson = Type("object"),
            SourceKind = "system",
            Readonly = true,
            ScopeKind = "system"
        });

        var parameters = model.Parameters.Select(parameter =>
        {
            variables.Add(new MicroflowExecutionVariableDeclaration
            {
                Name = parameter.Name,
                DataTypeJson = parameter.Type.Clone(),
                SourceKind = "parameter",
                Readonly = true,
                ScopeKind = "global",
                Diagnostics = string.IsNullOrWhiteSpace(parameter.Name)
                    ? [Diagnostic("RUNTIME_PARAMETER_NAME_MISSING", "error", "Parameter name is missing.", fieldPath: parameter.FieldPath)]
                    : Array.Empty<MicroflowExecutionDiagnosticDto>()
            });
            return new MicroflowExecutionParameter
            {
                Id = parameter.Id,
                Name = parameter.Name,
                DataTypeJson = parameter.Type.Clone(),
                Required = ReadBool(parameter.Raw, "required"),
                Documentation = ReadString(parameter.Raw, "documentation") ?? ReadString(parameter.Raw, "description")
            };
        }).ToArray();

        var nodes = model.Objects.Select(obj =>
        {
            var nodeDiagnostics = new List<MicroflowExecutionDiagnosticDto>();
            var actionRefs = new List<MicroflowExecutionMetadataRef>();
            var support = obj.Action is null
                ? new MicroflowActionSupportDescriptor { SupportLevel = MicroflowRuntimeSupportLevel.Supported, Reason = "supported", Message = "Supported control object." }
                : _supportMatrix.Resolve(obj.Action.Kind, obj.Action.OfficialType, options);

            if (obj.Action is not null)
            {
                ExtractActionMetadataRefs(obj, actionRefs);
                ExtractActionVariables(obj, variables, nodeDiagnostics);
                if (!string.Equals(support.SupportLevel, MicroflowRuntimeSupportLevel.Supported, StringComparison.OrdinalIgnoreCase))
                {
                    unsupported.Add(new MicroflowUnsupportedActionDescriptor
                    {
                        ObjectId = obj.Id,
                        ActionId = obj.Action.Id,
                        ActionKind = obj.Action.Kind,
                        OfficialType = obj.Action.OfficialType,
                        SupportLevel = support.SupportLevel,
                        Reason = support.Reason,
                        Message = support.Message,
                        FieldPath = obj.Action.FieldPath
                    });
                    nodeDiagnostics.Add(Diagnostic("RUNTIME_ACTION_UNSUPPORTED", options.FailOnUnsupported ? "error" : "warning", support.Message, obj.Id, actionId: obj.Action.Id, collectionId: obj.CollectionId, fieldPath: obj.Action.FieldPath));
                }
                else if (obj.Action.Raw.ValueKind == JsonValueKind.Undefined || obj.Action.Raw.ValueKind == JsonValueKind.Null)
                {
                    nodeDiagnostics.Add(Diagnostic("RUNTIME_P0_CONFIG_MISSING", "error", "Supported action is missing config.", obj.Id, actionId: obj.Action.Id, collectionId: obj.CollectionId, fieldPath: obj.Action.FieldPath));
                }
            }

            metadataRefs.AddRange(actionRefs);
            return new MicroflowExecutionNode
            {
                ObjectId = obj.Id,
                ActionId = obj.Action?.Id,
                CollectionId = obj.CollectionId,
                ParentLoopObjectId = obj.ParentLoopObjectId,
                Kind = obj.Kind,
                OfficialType = obj.OfficialType,
                Caption = obj.Caption,
                ActionKind = obj.Action?.Kind,
                ActionOfficialType = obj.Action?.OfficialType,
                SupportLevel = support.SupportLevel,
                RuntimeBehavior = RuntimeBehavior(obj.Kind, support.SupportLevel),
                ConfigJson = BuildNodeConfig(obj),
                ErrorHandling = ReadErrorHandling(obj),
                InputVariableNames = ExtractInputVariableNames(obj.Action?.Raw),
                OutputVariableNames = ExtractOutputVariableNames(obj.Action?.Raw),
                MetadataRefs = actionRefs,
                Diagnostics = nodeDiagnostics
            };
        }).ToArray();

        var flows = model.Flows.Select(flow => new MicroflowExecutionFlow
        {
            FlowId = flow.Id,
            CollectionId = flow.CollectionId,
            EdgeKind = EdgeKind(flow),
            ControlFlow = ControlFlow(flow),
            OriginObjectId = flow.OriginObjectId,
            DestinationObjectId = flow.DestinationObjectId,
            OriginConnectionIndex = ReadInt(flow.Raw, "originConnectionIndex"),
            DestinationConnectionIndex = ReadInt(flow.Raw, "destinationConnectionIndex"),
            CaseValues = flow.CaseValues.Select(value => value.Clone()).ToArray(),
            IsErrorHandler = flow.IsErrorHandler,
            BranchOrder = ReadIntByPath(flow.Raw, "editor", "branchOrder") ?? ReadInt(flow.Raw, "branchOrder"),
            Diagnostics = Array.Empty<MicroflowExecutionDiagnosticDto>()
        }).ToArray();

        var loopCollections = model.Objects
            .Where(obj => IsKind(obj, "loopedActivity"))
            .Select(loop => BuildLoopCollection(loop, model))
            .ToArray();

        ExtractDataTypeMetadataRefs(parameters.Select(parameter => parameter.DataTypeJson), metadataRefs);
        ExtractDataTypeMetadataRefs(variables.Select(variable => variable.DataTypeJson), metadataRefs);

        return new MicroflowRuntimeDto
        {
            Id = model.Id ?? options.ResourceId ?? "inline-schema",
            SchemaId = model.Id ?? options.ResourceId ?? "inline-schema",
            ResourceId = options.ResourceId,
            Version = options.Version,
            SchemaVersion = model.SchemaVersion,
            Parameters = parameters,
            Nodes = nodes,
            Flows = flows,
            Variables = variables,
            MetadataRefs = DistinctRefs(metadataRefs),
            UnsupportedActions = unsupported,
            LoopCollections = loopCollections,
            StartNodeId = model.Objects.FirstOrDefault(obj => !obj.InsideLoop && IsKind(obj, "startEvent"))?.Id ?? string.Empty,
            EndNodeIds = model.Objects.Where(obj => IsKind(obj, "endEvent")).Select(obj => obj.Id).ToArray(),
            Diagnostics = diagnostics,
            CreatedAt = _clock.UtcNow
        };
    }

    private static MicroflowExecutionLoopCollection BuildLoopCollection(MicroflowObjectModel loop, MicroflowSchemaModel model)
    {
        var nodes = model.Objects.Where(obj => string.Equals(obj.ParentLoopObjectId, loop.Id, StringComparison.Ordinal)).ToArray();
        var flows = model.Flows.Where(flow => nodes.Any(node => string.Equals(node.CollectionId, flow.CollectionId, StringComparison.Ordinal))).ToArray();
        return new MicroflowExecutionLoopCollection
        {
            LoopObjectId = loop.Id,
            CollectionId = nodes.FirstOrDefault()?.CollectionId ?? loop.Id,
            ParentCollectionId = loop.CollectionId,
            Nodes = nodes.Select(node => node.Id).ToArray(),
            Flows = flows.Select(flow => flow.Id).ToArray(),
            StartLikeNodeIds = nodes.Where(node => IsKind(node, "startEvent") || (!IsKind(node, "annotation") && !IsKind(node, "parameterObject"))).Select(node => node.Id).Take(1).ToArray(),
            TerminalNodeIds = nodes.Where(node => IsTerminal(node.Kind)).Select(node => node.Id).ToArray()
        };
    }

    private static void ExtractActionMetadataRefs(MicroflowObjectModel obj, List<MicroflowExecutionMetadataRef> refs)
    {
        var action = obj.Action;
        if (action is null)
        {
            return;
        }

        AddRef(refs, "entity", ReadStringByPath(action.Raw, "retrieveSource", "entityQualifiedName"), obj, $"{action.FieldPath}.retrieveSource.entityQualifiedName");
        AddRef(refs, "association", ReadStringByPath(action.Raw, "retrieveSource", "associationQualifiedName"), obj, $"{action.FieldPath}.retrieveSource.associationQualifiedName");
        AddRef(refs, "entity", ReadString(action.Raw, "entityQualifiedName"), obj, $"{action.FieldPath}.entityQualifiedName");
        AddRef(refs, "microflow", ReadString(action.Raw, "targetMicroflowId") ?? ReadString(action.Raw, "targetMicroflowQualifiedName"), obj, $"{action.FieldPath}.targetMicroflowId");
        AddRef(refs, "connector", ReadString(action.Raw, "connectorId") ?? ReadString(action.Raw, "connectorQualifiedName"), obj, $"{action.FieldPath}.connectorId", required: false);
        AddRef(refs, "unknown", ReadString(action.Raw, "exportMappingQualifiedName"), obj, $"{action.FieldPath}.exportMappingQualifiedName", required: false);
        AddRef(refs, "unknown", ReadString(action.Raw, "importMappingQualifiedName"), obj, $"{action.FieldPath}.importMappingQualifiedName", required: false);

        foreach (var member in EnumerateArray(action.Raw, "memberChanges"))
        {
            AddRef(refs, "attribute", ReadString(member, "memberQualifiedName") ?? ReadString(member, "attributeQualifiedName"), obj, $"{action.FieldPath}.memberChanges.memberQualifiedName");
        }

        foreach (var sort in EnumerateArray(action.Raw, "sortItems"))
        {
            AddRef(refs, "attribute", ReadString(sort, "attributeQualifiedName"), obj, $"{action.FieldPath}.sortItems.attributeQualifiedName", required: false);
        }
    }

    private static void ExtractDataTypeMetadataRefs(IEnumerable<JsonElement> dataTypes, List<MicroflowExecutionMetadataRef> refs)
    {
        foreach (var type in dataTypes)
        {
            AddRef(refs, "entity", ReadString(type, "entityQualifiedName"), null, "dataType.entityQualifiedName");
            AddRef(refs, "enumeration", ReadString(type, "enumerationQualifiedName"), null, "dataType.enumerationQualifiedName");
            if (type.TryGetProperty("itemType", out var itemType))
            {
                AddRef(refs, "entity", ReadString(itemType, "entityQualifiedName"), null, "dataType.itemType.entityQualifiedName");
            }
        }
    }

    private static void ExtractActionVariables(MicroflowObjectModel obj, List<MicroflowExecutionVariableDeclaration> variables, List<MicroflowExecutionDiagnosticDto> diagnostics)
    {
        var action = obj.Action;
        if (action is null)
        {
            return;
        }

        var outputName = ReadString(action.Raw, "outputVariableName")
            ?? ReadString(action.Raw, "resultVariableName")
            ?? ReadString(action.Raw, "targetVariableName")
            ?? ReadStringByPath(action.Raw, "response", "handling", "outputVariableName");
        if (!string.IsNullOrWhiteSpace(outputName))
        {
            variables.Add(new MicroflowExecutionVariableDeclaration
            {
                Name = outputName!,
                DataTypeJson = GuessActionOutputType(action.Raw, action.Kind),
                SourceKind = ActionSourceKind(action.Kind),
                SourceObjectId = obj.Id,
                SourceActionId = action.Id,
                CollectionId = obj.CollectionId,
                Readonly = false,
                ScopeKind = obj.ParentLoopObjectId is null ? "downstream" : "loop"
            });
        }

        if (string.Equals(action.Kind, "restCall", StringComparison.OrdinalIgnoreCase))
        {
            variables.Add(new MicroflowExecutionVariableDeclaration
            {
                Name = "latestHttpResponse",
                DataTypeJson = Type("object"),
                SourceKind = "errorHandler",
                SourceObjectId = obj.Id,
                SourceActionId = action.Id,
                CollectionId = obj.CollectionId,
                Readonly = true,
                ScopeKind = "errorHandler"
            });
        }
    }

    private static JsonElement GuessActionOutputType(JsonElement action, string actionKind)
    {
        if (action.TryGetProperty("dataType", out var dataType))
        {
            return dataType.Clone();
        }
        if (action.TryGetProperty("type", out var type))
        {
            return type.Clone();
        }
        if (string.Equals(actionKind, "createObject", StringComparison.OrdinalIgnoreCase))
        {
            var entity = ReadString(action, "entityQualifiedName");
            return JsonSerializer.SerializeToElement(new { kind = "object", entityQualifiedName = entity }, JsonOptions);
        }
        if (string.Equals(actionKind, "retrieve", StringComparison.OrdinalIgnoreCase))
        {
            var entity = ReadStringByPath(action, "retrieveSource", "entityQualifiedName");
            return JsonSerializer.SerializeToElement(new { kind = "list", itemType = new { kind = "object", entityQualifiedName = entity } }, JsonOptions);
        }
        return Type("unknown");
    }

    private static string ActionSourceKind(string actionKind)
        => actionKind switch
        {
            "retrieve" => "actionOutput",
            "createObject" => "actionOutput",
            "createVariable" => "createVariable",
            "callMicroflow" => "microflowReturn",
            "restCall" => "restResponse",
            _ => "modeledOnly"
        };

    private static MicroflowRuntimeErrorHandlingDto? ReadErrorHandling(MicroflowObjectModel obj)
    {
        var raw = obj.Action?.Raw ?? obj.Raw;
        var handling = ReadString(raw, "errorHandlingType") ?? ReadString(raw, "errorHandling");
        return string.IsNullOrWhiteSpace(handling)
            ? null
            : new MicroflowRuntimeErrorHandlingDto { ErrorHandlingType = handling!, ScopeObjectId = obj.Id };
    }

    private static JsonElement BuildNodeConfig(MicroflowObjectModel obj)
    {
        var value = new
        {
            objectKind = obj.Kind,
            officialType = obj.OfficialType,
            caption = obj.Caption,
            parameterId = obj.ParameterId,
            actionKind = obj.Action?.Kind,
            actionOfficialType = obj.Action?.OfficialType,
            raw = obj.Raw,
            action = obj.Action?.Raw
        };
        return JsonSerializer.SerializeToElement(value, JsonOptions);
    }

    private static string RuntimeBehavior(string kind, string supportLevel)
    {
        if (!string.Equals(supportLevel, MicroflowRuntimeSupportLevel.Supported, StringComparison.OrdinalIgnoreCase))
        {
            return "unsupported";
        }
        if (IsKind(kind, "annotation") || IsKind(kind, "parameterObject"))
        {
            return "ignored";
        }
        return IsTerminal(kind) ? "terminal" : "executable";
    }

    private static string EdgeKind(MicroflowFlowModel flow)
    {
        if (IsAnnotationFlow(flow))
        {
            return "annotation";
        }
        if (flow.IsErrorHandler)
        {
            return "errorHandler";
        }
        if (string.Equals(flow.EdgeKind, "decisionCondition", StringComparison.OrdinalIgnoreCase))
        {
            return "decisionCondition";
        }
        if (string.Equals(flow.EdgeKind, "objectTypeCondition", StringComparison.OrdinalIgnoreCase))
        {
            return "objectTypeCondition";
        }
        return "sequence";
    }

    private static string ControlFlow(MicroflowFlowModel flow)
        => EdgeKind(flow) switch
        {
            "annotation" => "ignored",
            "errorHandler" => "errorHandler",
            "decisionCondition" => "decision",
            "objectTypeCondition" => "objectType",
            _ => "normal"
        };

    private static IReadOnlyList<string> ExtractInputVariableNames(JsonElement? action)
        => ExtractVariableNames(action, "inputVariableNames", "inputVariableName", "targetVariableName");

    private static IReadOnlyList<string> ExtractOutputVariableNames(JsonElement? action)
        => ExtractVariableNames(action, "outputVariableNames", "outputVariableName", "resultVariableName");

    private static IReadOnlyList<string> ExtractVariableNames(JsonElement? action, string arrayName, params string[] names)
    {
        if (!action.HasValue)
        {
            return Array.Empty<string>();
        }
        var result = new List<string>();
        foreach (var value in EnumerateArray(action.Value, arrayName))
        {
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                result.Add(value.GetString()!);
            }
        }
        foreach (var name in names)
        {
            var value = ReadString(action.Value, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value!);
            }
        }
        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<MicroflowExecutionMetadataRef> DistinctRefs(IEnumerable<MicroflowExecutionMetadataRef> refs)
        => refs
            .Where(refItem => !string.IsNullOrWhiteSpace(refItem.QualifiedName))
            .GroupBy(refItem => $"{refItem.Kind}|{refItem.QualifiedName}|{refItem.SourceObjectId}|{refItem.SourceActionId}|{refItem.FieldPath}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    private static void AddRef(List<MicroflowExecutionMetadataRef> refs, string kind, string? qualifiedName, MicroflowObjectModel? obj, string fieldPath, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            return;
        }
        refs.Add(new MicroflowExecutionMetadataRef
        {
            Kind = kind,
            QualifiedName = qualifiedName!,
            SourceObjectId = obj?.Id,
            SourceActionId = obj?.Action?.Id,
            FieldPath = fieldPath,
            Required = required
        });
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.Clone())
            : Array.Empty<JsonElement>();

    private static JsonElement Type(string kind)
        => JsonSerializer.SerializeToElement(new { kind }, JsonOptions);

    private static MicroflowExecutionDiagnosticDto Diagnostic(string code, string severity, string message, string? objectId = null, string? flowId = null, string? actionId = null, string? collectionId = null, string? fieldPath = null)
        => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            ObjectId = objectId,
            FlowId = flowId,
            ActionId = actionId,
            CollectionId = collectionId,
            FieldPath = fieldPath
        };

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static int? ReadInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;

    private static int? ReadIntByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var result) ? result : null;
    }

    private static bool IsTerminal(string kind)
        => IsKind(kind, "endEvent") || IsKind(kind, "errorEvent") || IsKind(kind, "breakEvent") || IsKind(kind, "continueEvent");

    private static bool IsAnnotationFlow(MicroflowFlowModel flow)
        => string.Equals(flow.Kind, "annotation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flow.EdgeKind, "annotation", StringComparison.OrdinalIgnoreCase);

    private static bool IsKind(MicroflowObjectModel obj, string kind)
        => IsKind(obj.Kind, kind);

    private static bool IsKind(string value, string kind)
        => string.Equals(value, kind, StringComparison.OrdinalIgnoreCase);
}

public sealed class MicroflowExecutionPlanBuilder : IMicroflowExecutionPlanBuilder
{
    private readonly IMicroflowExecutionPlanValidator _validator;
    private readonly IMicroflowClock _clock;

    public MicroflowExecutionPlanBuilder(IMicroflowExecutionPlanValidator validator, IMicroflowClock clock)
    {
        _validator = validator;
        _clock = clock;
    }

    public MicroflowExecutionPlan Build(MicroflowRuntimeDto runtimeDto, MicroflowExecutionPlanLoadOptions options)
    {
        var normalFlows = runtimeDto.Flows.Where(flow => flow.ControlFlow == "normal").ToArray();
        var decisionFlows = runtimeDto.Flows.Where(flow => flow.ControlFlow == "decision").ToArray();
        var objectTypeFlows = runtimeDto.Flows.Where(flow => flow.ControlFlow == "objectType").ToArray();
        var errorHandlerFlows = runtimeDto.Flows.Where(flow => flow.ControlFlow == "errorHandler").ToArray();
        var ignoredFlows = runtimeDto.Flows.Where(flow => flow.ControlFlow == "ignored").ToArray();
        var preliminary = new MicroflowExecutionPlan
        {
            Id = $"plan-{runtimeDto.SchemaId}-{Guid.NewGuid():N}",
            SchemaId = runtimeDto.SchemaId,
            ResourceId = runtimeDto.ResourceId,
            Version = runtimeDto.Version,
            SchemaVersion = runtimeDto.SchemaVersion,
            StartNodeId = runtimeDto.StartNodeId,
            EndNodeIds = runtimeDto.EndNodeIds,
            Parameters = runtimeDto.Parameters,
            Nodes = runtimeDto.Nodes,
            Flows = runtimeDto.Flows,
            NormalFlows = normalFlows,
            DecisionFlows = decisionFlows,
            ObjectTypeFlows = objectTypeFlows,
            ErrorHandlerFlows = errorHandlerFlows,
            IgnoredFlows = ignoredFlows,
            LoopCollections = runtimeDto.LoopCollections,
            VariableDeclarations = runtimeDto.Variables,
            MetadataRefs = runtimeDto.MetadataRefs,
            UnsupportedActions = runtimeDto.UnsupportedActions,
            Diagnostics = runtimeDto.Diagnostics,
            CreatedAt = _clock.UtcNow
        };
        var validation = _validator.Validate(preliminary, options);
        return preliminary with
        {
            Diagnostics = options.IncludeDiagnostics
                ? preliminary.Diagnostics.Concat(validation.Diagnostics).ToArray()
                : Array.Empty<MicroflowExecutionDiagnosticDto>(),
            Validation = validation
        };
    }
}

public sealed class MicroflowExecutionPlanValidator : IMicroflowExecutionPlanValidator
{
    public MicroflowExecutionPlanValidationResult Validate(MicroflowExecutionPlan plan, MicroflowExecutionPlanLoadOptions options)
    {
        var diagnostics = new List<MicroflowExecutionDiagnosticDto>();
        var nodes = plan.Nodes.ToDictionary(node => node.ObjectId, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(plan.StartNodeId) || !nodes.ContainsKey(plan.StartNodeId))
        {
            diagnostics.Add(Diagnostic("RUNTIME_START_NOT_FOUND", "error", "ExecutionPlan startNodeId is missing or does not reference a node.", objectId: plan.StartNodeId));
        }

        if (plan.EndNodeIds.Count == 0)
        {
            diagnostics.Add(Diagnostic("RUNTIME_END_NOT_FOUND", "error", "ExecutionPlan has no EndEvent node."));
        }

        foreach (var flow in plan.Flows)
        {
            if (string.IsNullOrWhiteSpace(flow.OriginObjectId) || !nodes.ContainsKey(flow.OriginObjectId))
            {
                diagnostics.Add(Diagnostic("RUNTIME_FLOW_ORIGIN_NOT_FOUND", "error", "Flow originObjectId does not reference a node.", flowId: flow.FlowId, objectId: flow.OriginObjectId, collectionId: flow.CollectionId));
            }
            if (string.IsNullOrWhiteSpace(flow.DestinationObjectId) || !nodes.ContainsKey(flow.DestinationObjectId))
            {
                diagnostics.Add(Diagnostic("RUNTIME_FLOW_DESTINATION_NOT_FOUND", "error", "Flow destinationObjectId does not reference a node.", flowId: flow.FlowId, objectId: flow.DestinationObjectId, collectionId: flow.CollectionId));
            }
            if (flow.ControlFlow != "ignored" && string.Equals(flow.EdgeKind, "annotation", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Diagnostic("RUNTIME_ANNOTATION_FLOW_IN_CONTROL", "error", "AnnotationFlow must not participate in control flow.", flowId: flow.FlowId, collectionId: flow.CollectionId));
            }
            if (flow.ControlFlow == "decision" && (!nodes.TryGetValue(flow.OriginObjectId ?? string.Empty, out var decisionSource) || !string.Equals(decisionSource.Kind, "exclusiveSplit", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(Diagnostic("RUNTIME_DECISION_FLOW_SOURCE_INVALID", "error", "Decision flow source must be ExclusiveSplit.", flowId: flow.FlowId, objectId: flow.OriginObjectId, collectionId: flow.CollectionId));
            }
            if (flow.ControlFlow == "objectType" && (!nodes.TryGetValue(flow.OriginObjectId ?? string.Empty, out var objectTypeSource) || !string.Equals(objectTypeSource.Kind, "inheritanceSplit", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(Diagnostic("RUNTIME_OBJECT_TYPE_FLOW_SOURCE_INVALID", "error", "ObjectType flow source must be InheritanceSplit.", flowId: flow.FlowId, objectId: flow.OriginObjectId, collectionId: flow.CollectionId));
            }
        }

        foreach (var group in plan.ErrorHandlerFlows.Where(flow => !string.IsNullOrWhiteSpace(flow.OriginObjectId)).GroupBy(flow => flow.OriginObjectId, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                diagnostics.Add(Diagnostic("RUNTIME_ERROR_HANDLER_DUPLICATED", "error", "A source object can have at most one error handler flow.", objectId: group.Key));
            }
        }

        foreach (var node in plan.Nodes)
        {
            var incoming = plan.Flows.Any(flow => flow.DestinationObjectId == node.ObjectId && flow.ControlFlow != "ignored");
            var outgoing = plan.Flows.Any(flow => flow.OriginObjectId == node.ObjectId && flow.ControlFlow != "ignored");
            if (node.ObjectId == plan.StartNodeId && incoming)
            {
                diagnostics.Add(Diagnostic("RUNTIME_START_HAS_INCOMING", "error", "StartEvent must not have incoming control flow.", objectId: node.ObjectId, collectionId: node.CollectionId));
            }
            if (plan.EndNodeIds.Contains(node.ObjectId, StringComparer.Ordinal) && outgoing)
            {
                diagnostics.Add(Diagnostic("RUNTIME_END_HAS_OUTGOING", "error", "EndEvent must not have outgoing control flow.", objectId: node.ObjectId, collectionId: node.CollectionId));
            }
            if (node.RuntimeBehavior == "terminal" && outgoing)
            {
                diagnostics.Add(Diagnostic("RUNTIME_TERMINAL_HAS_OUTGOING", "error", "Terminal node must not have outgoing control flow.", objectId: node.ObjectId, collectionId: node.CollectionId));
            }
            if (node.ActionKind is not null && node.SupportLevel == MicroflowRuntimeSupportLevel.Supported && node.ConfigJson is null)
            {
                diagnostics.Add(Diagnostic("RUNTIME_P0_CONFIG_MISSING", "error", "Supported P0 action is missing config.", objectId: node.ObjectId, actionId: node.ActionId, collectionId: node.CollectionId));
            }
        }

        foreach (var loop in plan.LoopCollections)
        {
            foreach (var flowId in loop.Flows)
            {
                var flow = plan.Flows.FirstOrDefault(item => item.FlowId == flowId);
                if (flow is null || string.IsNullOrWhiteSpace(flow.CollectionId))
                {
                    diagnostics.Add(Diagnostic("RUNTIME_LOOP_FLOW_COLLECTION_MISSING", "error", "Loop internal flow must have collectionId.", flowId: flowId, objectId: loop.LoopObjectId, collectionId: loop.CollectionId));
                }
            }
        }

        foreach (var unsupported in plan.UnsupportedActions)
        {
            diagnostics.Add(Diagnostic(
                "RUNTIME_ACTION_UNSUPPORTED",
                options.FailOnUnsupported ? "error" : "warning",
                unsupported.Message,
                unsupported.ObjectId,
                actionId: unsupported.ActionId,
                fieldPath: unsupported.FieldPath));
        }

        var reachable = CollectReachable(plan);
        foreach (var node in plan.Nodes.Where(node => node.RuntimeBehavior != "ignored" && !reachable.Contains(node.ObjectId)))
        {
            diagnostics.Add(Diagnostic("RUNTIME_NODE_UNREACHABLE", "warning", "Node is not reachable from startNodeId.", objectId: node.ObjectId, collectionId: node.CollectionId));
        }

        var errors = diagnostics.Count(item => item.Severity == "error");
        var warnings = diagnostics.Count(item => item.Severity == "warning");
        return new MicroflowExecutionPlanValidationResult
        {
            Valid = errors == 0,
            Diagnostics = diagnostics,
            ErrorCount = errors,
            WarningCount = warnings,
            UnsupportedActionCount = plan.UnsupportedActions.Count
        };
    }

    private static HashSet<string> CollectReachable(MicroflowExecutionPlan plan)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(plan.StartNodeId))
        {
            return result;
        }
        var stack = new Stack<string>();
        stack.Push(plan.StartNodeId);
        var flows = plan.Flows.Where(flow => flow.ControlFlow != "ignored").GroupBy(flow => flow.OriginObjectId ?? string.Empty, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current))
            {
                continue;
            }
            if (!flows.TryGetValue(current, out var outgoing))
            {
                continue;
            }
            foreach (var flow in outgoing)
            {
                if (!string.IsNullOrWhiteSpace(flow.DestinationObjectId))
                {
                    stack.Push(flow.DestinationObjectId!);
                }
            }
        }
        return result;
    }

    private static MicroflowExecutionDiagnosticDto Diagnostic(string code, string severity, string message, string? objectId = null, string? flowId = null, string? actionId = null, string? collectionId = null, string? fieldPath = null)
        => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            ObjectId = objectId,
            FlowId = flowId,
            ActionId = actionId,
            CollectionId = collectionId,
            FieldPath = fieldPath
        };
}

public sealed class MicroflowExecutionPlanLoader : IMicroflowExecutionPlanLoader
{
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowVersionRepository _versionRepository;
    private readonly IMicroflowRuntimeDtoBuilder _runtimeDtoBuilder;
    private readonly IMicroflowExecutionPlanBuilder _planBuilder;

    public MicroflowExecutionPlanLoader(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowVersionRepository versionRepository,
        IMicroflowRuntimeDtoBuilder runtimeDtoBuilder,
        IMicroflowExecutionPlanBuilder planBuilder)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _versionRepository = versionRepository;
        _runtimeDtoBuilder = runtimeDtoBuilder;
        _planBuilder = planBuilder;
    }

    public async Task<MicroflowExecutionPlan> LoadCurrentAsync(string resourceId, MicroflowExecutionPlanLoadOptions options, CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        var snapshot = await LoadSnapshotAsync(resource.CurrentSchemaSnapshotId ?? resource.SchemaId, cancellationToken);
        return BuildPlan(MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson), options with
        {
            ResourceId = resource.Id,
            Version = string.IsNullOrWhiteSpace(options.Version) ? resource.Version : options.Version
        });
    }

    public async Task<MicroflowExecutionPlan> LoadVersionAsync(string resourceId, string versionId, MicroflowExecutionPlanLoadOptions options, CancellationToken cancellationToken)
    {
        _ = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        var version = await _versionRepository.GetByIdAsync(versionId, cancellationToken);
        if (version is null || !string.Equals(version.ResourceId, resourceId, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流版本不存在。", 404);
        }
        var snapshot = await LoadSnapshotAsync(version.SchemaSnapshotId, cancellationToken);
        return BuildPlan(MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson), options with
        {
            ResourceId = resourceId,
            Version = version.Version
        });
    }

    public Task<MicroflowExecutionPlan> LoadFromSchemaAsync(JsonElement schema, MicroflowExecutionPlanLoadOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "ExecutionPlan inline schema 必须是对象。", 400);
        }
        return Task.FromResult(BuildPlan(schema.Clone(), options));
    }

    private MicroflowExecutionPlan BuildPlan(JsonElement schema, MicroflowExecutionPlanLoadOptions options)
    {
        var runtimeDto = _runtimeDtoBuilder.Build(schema, options);
        var plan = _planBuilder.Build(runtimeDto, options);
        if (options.FailOnUnsupported && plan.Validation.ErrorCount > 0)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowValidationFailed,
                "ExecutionPlan 校验失败。",
                422,
                validationIssues: plan.Diagnostics.Select(ToValidationIssue).ToArray());
        }
        return plan;
    }

    private async Task<MicroflowSchemaSnapshotEntity> LoadSnapshotAsync(string? snapshotId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "微流当前 Schema 快照不存在。", 400);
        }
        return await _schemaSnapshotRepository.GetByIdAsync(snapshotId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "微流 Schema 快照不存在。", 400);
    }

    private static MicroflowValidationIssueDto ToValidationIssue(MicroflowExecutionDiagnosticDto diagnostic)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Severity = diagnostic.Severity,
            Code = diagnostic.Code,
            Message = diagnostic.Message,
            ObjectId = diagnostic.ObjectId,
            FlowId = diagnostic.FlowId,
            ActionId = diagnostic.ActionId,
            CollectionId = diagnostic.CollectionId,
            FieldPath = diagnostic.FieldPath,
            Source = "runtimePlan"
        };
}
