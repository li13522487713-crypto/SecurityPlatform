using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Metadata;

public sealed class MicroflowMetadataResolver : IMicroflowMetadataResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowMetadataService _metadataService;

    public MicroflowMetadataResolver(IMicroflowMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    public async Task<MicroflowMetadataResolutionContext> CreateContextAsync(
        MicroflowExecutionPlan plan,
        MicroflowRuntimeSecurityContext securityContext,
        CancellationToken ct)
    {
        var catalog = await _metadataService.GetCatalogAsync(
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = securityContext.WorkspaceId,
                IncludeSystem = true,
                IncludeArchived = true
            },
            ct);

        var entities = catalog.Entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.QualifiedName))
            .GroupBy(entity => entity.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, MetadataAttributeDto>(StringComparer.OrdinalIgnoreCase);
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in catalog.Entities)
        {
            foreach (var attribute in entity.Attributes.Where(attribute => !string.IsNullOrWhiteSpace(attribute.QualifiedName)))
            {
                attributes.TryAdd(attribute.QualifiedName, attribute);
                owners.TryAdd(attribute.QualifiedName, entity.QualifiedName);
            }
        }

        var context = new MicroflowMetadataResolutionContext
        {
            Catalog = catalog,
            CatalogVersion = catalog.Version,
            UpdatedAt = catalog.UpdatedAt,
            EntitiesByQualifiedName = entities,
            AttributesByQualifiedName = attributes,
            AttributeOwnersByQualifiedName = owners,
            AssociationsByQualifiedName = catalog.Associations
                .Where(association => !string.IsNullOrWhiteSpace(association.QualifiedName))
                .GroupBy(association => association.QualifiedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase),
            EnumerationsByQualifiedName = catalog.Enumerations
                .Where(enumeration => !string.IsNullOrWhiteSpace(enumeration.QualifiedName))
                .GroupBy(enumeration => enumeration.QualifiedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase),
            MicroflowsById = catalog.Microflows
                .Where(microflow => !string.IsNullOrWhiteSpace(microflow.Id))
                .GroupBy(microflow => microflow.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase),
            MicroflowsByQualifiedName = catalog.Microflows
                .Where(microflow => !string.IsNullOrWhiteSpace(microflow.QualifiedName))
                .GroupBy(microflow => microflow.QualifiedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase),
            PagesById = catalog.Pages
                .Where(page => !string.IsNullOrWhiteSpace(page.Id))
                .GroupBy(page => page.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase),
            WorkflowsById = catalog.Workflows
                .Where(workflow => !string.IsNullOrWhiteSpace(workflow.Id))
                .GroupBy(workflow => workflow.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase),
            ConnectorsById = catalog.Connectors
                .Where(connector => !string.IsNullOrWhiteSpace(connector.Id))
                .GroupBy(connector => connector.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase),
            ExecutionPlan = plan,
            MetadataRefs = plan.MetadataRefs,
            SecurityContext = securityContext
        };

        return context with { Diagnostics = ResolvePlanMetadataRefs(context).Diagnostics };
    }

    public MicroflowResolvedEntity ResolveEntity(
        MicroflowMetadataResolutionContext context,
        string qualifiedName,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        if (!string.IsNullOrWhiteSpace(qualifiedName)
            && context.EntitiesByQualifiedName.TryGetValue(qualifiedName, out var entity))
        {
            return new MicroflowResolvedEntity
            {
                Found = true,
                QualifiedName = entity.QualifiedName,
                Entity = entity,
                IsSystemEntity = entity.IsSystemEntity,
                IsPersistable = entity.IsPersistable,
                Generalization = entity.Generalization,
                Specializations = entity.Specializations
            };
        }

        return new MicroflowResolvedEntity
        {
            Found = false,
            QualifiedName = qualifiedName ?? string.Empty,
            Diagnostics = [Diagnostic("RUNTIME_METADATA_ENTITY_NOT_FOUND", "entity", qualifiedName, $"实体 metadata 不存在：{qualifiedName}", sourceObjectId, fieldPath)]
        };
    }

    public MicroflowResolvedAttribute ResolveAttribute(
        MicroflowMetadataResolutionContext context,
        string attributeQualifiedName,
        string? entityQualifiedName = null,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        MetadataAttributeDto? attribute = null;
        string? owner = null;
        if (!string.IsNullOrWhiteSpace(attributeQualifiedName)
            && context.AttributesByQualifiedName.TryGetValue(attributeQualifiedName, out var direct))
        {
            attribute = direct;
            owner = context.AttributeOwnersByQualifiedName.GetValueOrDefault(attribute.QualifiedName);
        }

        if (attribute is null && !string.IsNullOrWhiteSpace(entityQualifiedName))
        {
            (attribute, owner) = FindAttributeInEntityOrGeneralization(context, entityQualifiedName!, attributeQualifiedName);
        }

        if (attribute is not null)
        {
            return new MicroflowResolvedAttribute
            {
                Found = true,
                QualifiedName = attribute.QualifiedName,
                Attribute = attribute,
                OwnerEntityQualifiedName = owner,
                DataType = ResolveDataType(context, attribute.Type, sourceObjectId, fieldPath),
                IsReadonly = attribute.IsReadonly,
                IsRequired = attribute.Required
            };
        }

        return new MicroflowResolvedAttribute
        {
            Found = false,
            QualifiedName = attributeQualifiedName ?? string.Empty,
            OwnerEntityQualifiedName = entityQualifiedName,
            Diagnostics = [Diagnostic("RUNTIME_METADATA_ATTRIBUTE_NOT_FOUND", "attribute", attributeQualifiedName, $"属性 metadata 不存在：{AttributeMessageName(entityQualifiedName, attributeQualifiedName)}", sourceObjectId, fieldPath)]
        };
    }

    public MicroflowResolvedAssociation ResolveAssociation(
        MicroflowMetadataResolutionContext context,
        string associationQualifiedName,
        string? startEntityQualifiedName = null,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        if (!string.IsNullOrWhiteSpace(associationQualifiedName)
            && context.AssociationsByQualifiedName.TryGetValue(associationQualifiedName, out var association))
        {
            return ToResolvedAssociation(association, null);
        }

        if (!string.IsNullOrWhiteSpace(startEntityQualifiedName)
            && TryFindAssociationRef(context, startEntityQualifiedName!, associationQualifiedName, out var sourceEntity, out var associationRef))
        {
            if (context.AssociationsByQualifiedName.TryGetValue(associationRef.AssociationQualifiedName, out var full))
            {
                return ToResolvedAssociation(full, associationRef);
            }

            return new MicroflowResolvedAssociation
            {
                Found = true,
                QualifiedName = associationRef.AssociationQualifiedName,
                SourceEntityQualifiedName = sourceEntity.QualifiedName,
                TargetEntityQualifiedName = associationRef.TargetEntityQualifiedName,
                Multiplicity = associationRef.Multiplicity,
                Direction = associationRef.Direction,
                ReturnsList = ReturnsList(associationRef.Multiplicity)
            };
        }

        return new MicroflowResolvedAssociation
        {
            Found = false,
            QualifiedName = associationQualifiedName ?? string.Empty,
            SourceEntityQualifiedName = startEntityQualifiedName,
            Diagnostics = [Diagnostic("RUNTIME_METADATA_ASSOCIATION_NOT_FOUND", "association", associationQualifiedName, $"关联 metadata 不存在：{AttributeMessageName(startEntityQualifiedName, associationQualifiedName)}", sourceObjectId, fieldPath)]
        };
    }

    public MicroflowResolvedEnumeration ResolveEnumeration(
        MicroflowMetadataResolutionContext context,
        string enumerationQualifiedName,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        if (!string.IsNullOrWhiteSpace(enumerationQualifiedName)
            && context.EnumerationsByQualifiedName.TryGetValue(enumerationQualifiedName, out var enumeration))
        {
            return new MicroflowResolvedEnumeration
            {
                Found = true,
                QualifiedName = enumeration.QualifiedName,
                Enumeration = enumeration,
                Values = enumeration.Values
            };
        }

        return new MicroflowResolvedEnumeration
        {
            Found = false,
            QualifiedName = enumerationQualifiedName ?? string.Empty,
            Diagnostics = [Diagnostic("RUNTIME_METADATA_ENUMERATION_NOT_FOUND", "enumeration", enumerationQualifiedName, $"枚举 metadata 不存在：{enumerationQualifiedName}", sourceObjectId, fieldPath)]
        };
    }

    public MicroflowResolvedEnumerationValue ResolveEnumerationValue(
        MicroflowMetadataResolutionContext context,
        string enumerationQualifiedName,
        string value,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        var enumeration = ResolveEnumeration(context, enumerationQualifiedName, sourceObjectId, fieldPath);
        if (!enumeration.Found)
        {
            return new MicroflowResolvedEnumerationValue
            {
                Found = false,
                EnumerationQualifiedName = enumerationQualifiedName,
                Value = value,
                Diagnostics = enumeration.Diagnostics
            };
        }

        var enumValue = enumeration.Values.FirstOrDefault(item => string.Equals(item.Key, value, StringComparison.OrdinalIgnoreCase));
        if (enumValue is not null)
        {
            return new MicroflowResolvedEnumerationValue
            {
                Found = true,
                EnumerationQualifiedName = enumeration.QualifiedName,
                Value = enumValue.Key,
                Caption = enumValue.Caption
            };
        }

        return new MicroflowResolvedEnumerationValue
        {
            Found = false,
            EnumerationQualifiedName = enumeration.QualifiedName,
            Value = value,
            Diagnostics = [Diagnostic("RUNTIME_METADATA_ENUMERATION_VALUE_NOT_FOUND", "enumerationValue", $"{enumeration.QualifiedName}.{value}", $"枚举值 metadata 不存在：{enumeration.QualifiedName}.{value}", sourceObjectId, fieldPath)]
        };
    }

    public MicroflowResolvedMicroflowRef ResolveMicroflowRef(
        MicroflowMetadataResolutionContext context,
        string? id,
        string? qualifiedName,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        MetadataMicroflowRefDto? microflow = null;
        if (!string.IsNullOrWhiteSpace(id))
        {
            context.MicroflowsById.TryGetValue(id!, out microflow);
        }

        if (microflow is null && !string.IsNullOrWhiteSpace(qualifiedName))
        {
            context.MicroflowsByQualifiedName.TryGetValue(qualifiedName!, out microflow);
        }

        if (microflow is null)
        {
            var name = id ?? qualifiedName ?? string.Empty;
            return new MicroflowResolvedMicroflowRef
            {
                Found = false,
                Id = id,
                QualifiedName = qualifiedName,
                Diagnostics = [Diagnostic("RUNTIME_METADATA_MICROFLOW_NOT_FOUND", "microflow", name, $"微流引用不存在：{name}", sourceObjectId, fieldPath)]
            };
        }

        var diagnostics = new List<MicroflowMetadataResolutionDiagnostic>();
        if (string.Equals(microflow.Status, "archived", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Diagnostic("RUNTIME_METADATA_MICROFLOW_ARCHIVED", "microflow", microflow.QualifiedName, $"微流引用已归档：{microflow.QualifiedName}", sourceObjectId, fieldPath, MicroflowMetadataResolutionSeverity.Warning));
        }

        return new MicroflowResolvedMicroflowRef
        {
            Found = true,
            Id = microflow.Id,
            QualifiedName = microflow.QualifiedName,
            Microflow = microflow,
            Parameters = microflow.Parameters,
            ReturnType = ResolveDataType(context, microflow.ReturnType, sourceObjectId, fieldPath),
            Status = microflow.Status,
            Diagnostics = diagnostics
        };
    }

    public MicroflowResolvedDataType ResolveDataType(
        MicroflowMetadataResolutionContext context,
        JsonElement dataTypeJson,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        if (dataTypeJson.ValueKind != JsonValueKind.Object)
        {
            return MicroflowResolvedDataType.Unknown(dataTypeJson.GetRawText(), [Diagnostic("RUNTIME_METADATA_DATATYPE_INVALID", "dataType", null, "DataType 必须是 JSON object。", sourceObjectId, fieldPath)]);
        }

        var kind = ReadString(dataTypeJson, "kind") ?? ReadString(dataTypeJson, "type") ?? MicroflowResolvedDataTypeKind.Unknown;
        if (ReadBool(dataTypeJson, "isList") && !string.Equals(kind, MicroflowResolvedDataTypeKind.List, StringComparison.OrdinalIgnoreCase))
        {
            kind = MicroflowResolvedDataTypeKind.List;
        }

        kind = NormalizeKind(kind);
        var diagnostics = new List<MicroflowMetadataResolutionDiagnostic>();
        if (kind is MicroflowResolvedDataTypeKind.Void or MicroflowResolvedDataTypeKind.Boolean or MicroflowResolvedDataTypeKind.String
            or MicroflowResolvedDataTypeKind.Integer or MicroflowResolvedDataTypeKind.Long or MicroflowResolvedDataTypeKind.Decimal
            or MicroflowResolvedDataTypeKind.DateTime or MicroflowResolvedDataTypeKind.Json or MicroflowResolvedDataTypeKind.Binary)
        {
            return new MicroflowResolvedDataType { Found = true, Kind = kind, RawDataTypeJson = dataTypeJson.GetRawText() };
        }

        if (string.Equals(kind, MicroflowResolvedDataTypeKind.Object, StringComparison.OrdinalIgnoreCase))
        {
            var entityQualifiedName = ReadString(dataTypeJson, "entityQualifiedName") ?? ReadString(dataTypeJson, "qualifiedName");
            if (string.IsNullOrWhiteSpace(entityQualifiedName))
            {
                diagnostics.Add(Diagnostic("RUNTIME_METADATA_DATATYPE_ENTITY_MISSING", "dataType", null, "object dataType 缺少 entityQualifiedName。", sourceObjectId, fieldPath));
                return MicroflowResolvedDataType.Unknown(dataTypeJson.GetRawText(), diagnostics);
            }

            var entity = ResolveEntity(context, entityQualifiedName!, sourceObjectId, fieldPath);
            diagnostics.AddRange(entity.Diagnostics);
            return new MicroflowResolvedDataType
            {
                Found = entity.Found,
                Kind = MicroflowResolvedDataTypeKind.Object,
                EntityQualifiedName = entityQualifiedName,
                RawDataTypeJson = dataTypeJson.GetRawText(),
                Diagnostics = diagnostics
            };
        }

        if (string.Equals(kind, MicroflowResolvedDataTypeKind.Enumeration, StringComparison.OrdinalIgnoreCase))
        {
            var enumerationQualifiedName = ReadString(dataTypeJson, "enumerationQualifiedName") ?? ReadString(dataTypeJson, "enumQualifiedName") ?? ReadString(dataTypeJson, "qualifiedName");
            if (string.IsNullOrWhiteSpace(enumerationQualifiedName))
            {
                diagnostics.Add(Diagnostic("RUNTIME_METADATA_DATATYPE_ENUMERATION_MISSING", "dataType", null, "enumeration dataType 缺少 enumerationQualifiedName。", sourceObjectId, fieldPath));
                return MicroflowResolvedDataType.Unknown(dataTypeJson.GetRawText(), diagnostics);
            }

            var enumeration = ResolveEnumeration(context, enumerationQualifiedName!, sourceObjectId, fieldPath);
            diagnostics.AddRange(enumeration.Diagnostics);
            return new MicroflowResolvedDataType
            {
                Found = enumeration.Found,
                Kind = MicroflowResolvedDataTypeKind.Enumeration,
                EnumerationQualifiedName = enumerationQualifiedName,
                RawDataTypeJson = dataTypeJson.GetRawText(),
                Diagnostics = diagnostics
            };
        }

        if (string.Equals(kind, MicroflowResolvedDataTypeKind.List, StringComparison.OrdinalIgnoreCase))
        {
            var itemType = dataTypeJson.TryGetProperty("itemType", out var itemTypeElement)
                ? ResolveDataType(context, itemTypeElement, sourceObjectId, AppendPath(fieldPath, "itemType"))
                : ResolveDataType(context, JsonSerializer.SerializeToElement(new
                {
                    kind = ReadString(dataTypeJson, "entityQualifiedName") is null ? MicroflowResolvedDataTypeKind.Unknown : MicroflowResolvedDataTypeKind.Object,
                    entityQualifiedName = ReadString(dataTypeJson, "entityQualifiedName")
                }, JsonOptions), sourceObjectId, AppendPath(fieldPath, "itemType"));
            diagnostics.AddRange(itemType.Diagnostics);
            if (itemType.Kind == MicroflowResolvedDataTypeKind.Unknown)
            {
                diagnostics.Add(Diagnostic("RUNTIME_METADATA_DATATYPE_ITEM_MISSING", "dataType", null, "list dataType 缺少可解析 itemType。", sourceObjectId, fieldPath));
            }

            return new MicroflowResolvedDataType
            {
                Found = itemType.Found,
                Kind = MicroflowResolvedDataTypeKind.List,
                ItemType = itemType,
                RawDataTypeJson = dataTypeJson.GetRawText(),
                Diagnostics = diagnostics
            };
        }

        return MicroflowResolvedDataType.Unknown(dataTypeJson.GetRawText(), [Diagnostic("RUNTIME_METADATA_DATATYPE_UNKNOWN", "dataType", null, $"未知 dataType kind：{kind}", sourceObjectId, fieldPath)]);
    }

    public MicroflowResolvedMemberPath ResolveMemberPath(
        MicroflowMetadataResolutionContext context,
        MicroflowResolvedDataType rootType,
        IReadOnlyList<string> memberPath,
        string? sourceObjectId = null,
        string? fieldPath = null)
    {
        var diagnostics = new List<MicroflowMetadataResolutionDiagnostic>();
        var traversed = new List<MicroflowResolvedMemberPathSegment>();
        if (!string.Equals(rootType.Kind, MicroflowResolvedDataTypeKind.Object, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rootType.EntityQualifiedName))
        {
            diagnostics.Add(Diagnostic("RUNTIME_METADATA_MEMBER_ROOT_NOT_OBJECT", "memberPath", string.Join("/", memberPath), "member path 根类型必须是 object 且包含 entityQualifiedName。", sourceObjectId, fieldPath));
            return MemberPathResult(false, rootType, memberPath, MicroflowResolvedDataType.Unknown(rootType.RawDataTypeJson), traversed, diagnostics);
        }

        var currentEntity = rootType.EntityQualifiedName!;
        MicroflowResolvedDataType currentType = rootType;
        string? finalAttribute = null;
        string? finalAssociation = null;
        foreach (var segment in memberPath.Where(segment => !string.IsNullOrWhiteSpace(segment)))
        {
            var attribute = ResolveAttribute(context, segment, currentEntity, sourceObjectId, fieldPath);
            if (attribute.Found)
            {
                traversed.Add(new MicroflowResolvedMemberPathSegment
                {
                    Member = segment,
                    Kind = "attribute",
                    QualifiedName = attribute.QualifiedName,
                    SourceEntityQualifiedName = currentEntity
                });
                currentType = attribute.DataType;
                finalAttribute = attribute.QualifiedName;
                finalAssociation = null;
                if (!string.Equals(segment, memberPath.Last(), StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic("RUNTIME_METADATA_MEMBER_ATTRIBUTE_NOT_TERMINAL", "memberPath", string.Join("/", memberPath), $"属性成员必须位于路径末尾：{segment}", sourceObjectId, fieldPath));
                    return MemberPathResult(false, rootType, memberPath, currentType, traversed, diagnostics);
                }

                continue;
            }

            var association = ResolveAssociation(context, segment, currentEntity, sourceObjectId, fieldPath);
            if (association.Found)
            {
                traversed.Add(new MicroflowResolvedMemberPathSegment
                {
                    Member = segment,
                    Kind = "association",
                    QualifiedName = association.QualifiedName,
                    SourceEntityQualifiedName = currentEntity,
                    TargetEntityQualifiedName = association.TargetEntityQualifiedName,
                    ReturnsList = association.ReturnsList
                });
                finalAssociation = association.QualifiedName;
                finalAttribute = null;
                currentEntity = association.TargetEntityQualifiedName ?? string.Empty;
                var target = new MicroflowResolvedDataType
                {
                    Found = !string.IsNullOrWhiteSpace(currentEntity) && context.EntitiesByQualifiedName.ContainsKey(currentEntity),
                    Kind = MicroflowResolvedDataTypeKind.Object,
                    EntityQualifiedName = currentEntity
                };
                currentType = association.ReturnsList
                    ? new MicroflowResolvedDataType { Found = target.Found, Kind = MicroflowResolvedDataTypeKind.List, ItemType = target }
                    : target;
                if (association.ReturnsList && !string.Equals(segment, memberPath.Last(), StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic("RUNTIME_METADATA_MEMBER_LIST_TRAVERSAL_REQUIRES_LOOP", "memberPath", string.Join("/", memberPath), $"关联 {association.QualifiedName} 返回 list<object>，后续成员访问需要 loop。", sourceObjectId, fieldPath, MicroflowMetadataResolutionSeverity.Warning));
                    currentType = target;
                }

                continue;
            }

            diagnostics.AddRange(attribute.Diagnostics);
            diagnostics.AddRange(association.Diagnostics);
            diagnostics.Add(Diagnostic("RUNTIME_METADATA_MEMBER_NOT_FOUND", "memberPath", string.Join("/", memberPath), $"成员不存在：{currentEntity}/{segment}", sourceObjectId, fieldPath));
            return MemberPathResult(false, rootType, memberPath, MicroflowResolvedDataType.Unknown(), traversed, diagnostics);
        }

        return new MicroflowResolvedMemberPath
        {
            Found = diagnostics.All(item => !string.Equals(item.Severity, MicroflowMetadataResolutionSeverity.Error, StringComparison.OrdinalIgnoreCase)),
            RootType = rootType,
            MemberPath = memberPath,
            FinalType = currentType,
            FinalEntityQualifiedName = currentType.Kind == MicroflowResolvedDataTypeKind.Object ? currentType.EntityQualifiedName : currentType.ItemType?.EntityQualifiedName,
            FinalAttributeQualifiedName = finalAttribute,
            FinalAssociationQualifiedName = finalAssociation,
            TraversedMembers = traversed,
            Diagnostics = diagnostics
        };
    }

    public bool IsEntitySpecializationOf(
        MicroflowMetadataResolutionContext context,
        string childEntityQualifiedName,
        string parentEntityQualifiedName)
    {
        var current = childEntityQualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            if (string.Equals(current, parentEntityQualifiedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = context.EntitiesByQualifiedName.TryGetValue(current, out var entity) ? entity.Generalization : null;
        }

        return false;
    }

    public MicroflowMetadataResolutionReport ResolvePlanMetadataRefs(MicroflowMetadataResolutionContext context)
    {
        var diagnostics = new List<MicroflowMetadataResolutionDiagnostic>();
        var missingEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingAssociations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingEnumerations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingMicroflows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unsupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in context.MetadataRefs.Where(reference => !string.IsNullOrWhiteSpace(reference.QualifiedName))
                     .GroupBy(reference => $"{reference.Kind}:{reference.QualifiedName}", StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            var severity = reference.Required ? MicroflowMetadataResolutionSeverity.Error : MicroflowMetadataResolutionSeverity.Warning;
            var result = reference.Kind switch
            {
                "entity" => ResolveEntity(context, reference.QualifiedName, reference.SourceObjectId, reference.FieldPath).Diagnostics,
                "attribute" => ResolveAttribute(context, reference.QualifiedName, sourceObjectId: reference.SourceObjectId, fieldPath: reference.FieldPath).Diagnostics,
                "association" => ResolveAssociation(context, reference.QualifiedName, sourceObjectId: reference.SourceObjectId, fieldPath: reference.FieldPath).Diagnostics,
                "enumeration" => ResolveEnumeration(context, reference.QualifiedName, reference.SourceObjectId, reference.FieldPath).Diagnostics,
                "microflow" => ResolveMicroflowRef(context, reference.QualifiedName, reference.QualifiedName, reference.SourceObjectId, reference.FieldPath).Diagnostics,
                _ => [Diagnostic("RUNTIME_METADATA_REF_UNSUPPORTED", reference.Kind, reference.QualifiedName, $"暂不支持的 metadataRef：{reference.Kind}:{reference.QualifiedName}", reference.SourceObjectId, reference.FieldPath, severity)]
            };

            foreach (var diagnostic in result)
            {
                diagnostics.Add(diagnostic with
                {
                    Severity = string.Equals(diagnostic.Severity, MicroflowMetadataResolutionSeverity.Warning, StringComparison.OrdinalIgnoreCase)
                        ? diagnostic.Severity
                        : severity,
                    SourceActionId = diagnostic.SourceActionId ?? reference.SourceActionId
                });
                AddMissing(reference, missingEntities, missingAttributes, missingAssociations, missingEnumerations, missingMicroflows, unsupported);
            }
        }

        return new MicroflowMetadataResolutionReport
        {
            AllResolved = diagnostics.All(item => !string.Equals(item.Severity, MicroflowMetadataResolutionSeverity.Error, StringComparison.OrdinalIgnoreCase)),
            Diagnostics = diagnostics,
            MissingEntities = missingEntities.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
            MissingAttributes = missingAttributes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
            MissingAssociations = missingAssociations.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
            MissingEnumerations = missingEnumerations.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
            MissingMicroflows = missingMicroflows.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
            UnsupportedRefs = unsupported.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static MicroflowResolvedAssociation ToResolvedAssociation(MetadataAssociationDto association, MetadataAssociationRefDto? reference)
        => new()
        {
            Found = true,
            QualifiedName = association.QualifiedName,
            Association = association,
            SourceEntityQualifiedName = association.SourceEntityQualifiedName,
            TargetEntityQualifiedName = reference?.TargetEntityQualifiedName ?? association.TargetEntityQualifiedName,
            Multiplicity = reference?.Multiplicity ?? association.Multiplicity,
            Direction = reference?.Direction ?? association.Direction,
            ReturnsList = ReturnsList(reference?.Multiplicity ?? association.Multiplicity)
        };

    private static MicroflowResolvedMemberPath MemberPathResult(
        bool found,
        MicroflowResolvedDataType rootType,
        IReadOnlyList<string> memberPath,
        MicroflowResolvedDataType finalType,
        IReadOnlyList<MicroflowResolvedMemberPathSegment> traversed,
        IReadOnlyList<MicroflowMetadataResolutionDiagnostic> diagnostics)
        => new()
        {
            Found = found,
            RootType = rootType,
            MemberPath = memberPath,
            FinalType = finalType,
            TraversedMembers = traversed,
            Diagnostics = diagnostics
        };

    private static void AddMissing(
        MicroflowExecutionMetadataRef reference,
        ISet<string> entities,
        ISet<string> attributes,
        ISet<string> associations,
        ISet<string> enumerations,
        ISet<string> microflows,
        ISet<string> unsupported)
    {
        _ = reference.Kind switch
        {
            "entity" => entities.Add(reference.QualifiedName),
            "attribute" => attributes.Add(reference.QualifiedName),
            "association" => associations.Add(reference.QualifiedName),
            "enumeration" => enumerations.Add(reference.QualifiedName),
            "microflow" => microflows.Add(reference.QualifiedName),
            _ => unsupported.Add($"{reference.Kind}:{reference.QualifiedName}")
        };
    }

    private (MetadataAttributeDto? Attribute, string? Owner) FindAttributeInEntityOrGeneralization(
        MicroflowMetadataResolutionContext context,
        string entityQualifiedName,
        string segment)
    {
        var current = entityQualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            if (!context.EntitiesByQualifiedName.TryGetValue(current, out var entity))
            {
                return (null, null);
            }

            var attribute = entity.Attributes.FirstOrDefault(item =>
                string.Equals(item.Name, segment, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.QualifiedName, segment, StringComparison.OrdinalIgnoreCase)
                || item.QualifiedName.EndsWith($".{segment}", StringComparison.OrdinalIgnoreCase));
            if (attribute is not null)
            {
                return (attribute, entity.QualifiedName);
            }

            current = entity.Generalization;
        }

        return (null, null);
    }

    private static bool TryFindAssociationRef(
        MicroflowMetadataResolutionContext context,
        string entityQualifiedName,
        string segment,
        out MetadataEntityDto sourceEntity,
        out MetadataAssociationRefDto association)
    {
        var current = entityQualifiedName;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            if (!context.EntitiesByQualifiedName.TryGetValue(current, out sourceEntity!))
            {
                break;
            }

            association = sourceEntity.Associations.FirstOrDefault(item =>
                string.Equals(item.AssociationQualifiedName, segment, StringComparison.OrdinalIgnoreCase)
                || item.AssociationQualifiedName.EndsWith($".{segment}", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.AssociationQualifiedName.Split('.').LastOrDefault(), segment, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.TargetEntityQualifiedName.Split('.').LastOrDefault(), segment, StringComparison.OrdinalIgnoreCase))!;
            if (association is not null)
            {
                return true;
            }

            current = sourceEntity.Generalization;
        }

        sourceEntity = null!;
        association = null!;
        return false;
    }

    private static bool ReturnsList(string? multiplicity)
        => multiplicity?.Contains("many", StringComparison.OrdinalIgnoreCase) == true
           && !multiplicity.Contains("ToOne", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeKind(string kind)
        => kind switch
        {
            "bool" => MicroflowResolvedDataTypeKind.Boolean,
            "datetime" => MicroflowResolvedDataTypeKind.DateTime,
            "date" => MicroflowResolvedDataTypeKind.DateTime,
            "bytes" => MicroflowResolvedDataTypeKind.Binary,
            _ => kind
        };

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var property)
           && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var property)
           && property.ValueKind is JsonValueKind.True or JsonValueKind.False
           && property.GetBoolean();

    private static string? AppendPath(string? fieldPath, string segment)
        => string.IsNullOrWhiteSpace(fieldPath) ? segment : $"{fieldPath}.{segment}";

    private static string AttributeMessageName(string? entityQualifiedName, string? member)
        => string.IsNullOrWhiteSpace(entityQualifiedName) ? member ?? string.Empty : $"{entityQualifiedName}/{member}";

    private static MicroflowMetadataResolutionDiagnostic Diagnostic(
        string code,
        string? kind,
        string? qualifiedName,
        string message,
        string? sourceObjectId,
        string? fieldPath,
        string severity = MicroflowMetadataResolutionSeverity.Error)
        => new()
        {
            Code = code,
            Severity = severity,
            Kind = kind,
            QualifiedName = qualifiedName,
            Message = message,
            SourceObjectId = sourceObjectId,
            FieldPath = fieldPath
        };
}
