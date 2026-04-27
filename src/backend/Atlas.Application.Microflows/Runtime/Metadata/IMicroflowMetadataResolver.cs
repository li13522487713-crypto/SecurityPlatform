using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Metadata;

public interface IMicroflowMetadataResolver
{
    Task<MicroflowMetadataResolutionContext> CreateContextAsync(
        MicroflowExecutionPlan plan,
        MicroflowRuntimeSecurityContext securityContext,
        CancellationToken ct);

    MicroflowResolvedEntity ResolveEntity(
        MicroflowMetadataResolutionContext context,
        string qualifiedName,
        string? sourceObjectId = null,
        string? fieldPath = null);

    MicroflowResolvedAttribute ResolveAttribute(
        MicroflowMetadataResolutionContext context,
        string attributeQualifiedName,
        string? entityQualifiedName = null,
        string? sourceObjectId = null,
        string? fieldPath = null);

    MicroflowResolvedAssociation ResolveAssociation(
        MicroflowMetadataResolutionContext context,
        string associationQualifiedName,
        string? startEntityQualifiedName = null,
        string? sourceObjectId = null,
        string? fieldPath = null);

    MicroflowResolvedEnumeration ResolveEnumeration(
        MicroflowMetadataResolutionContext context,
        string enumerationQualifiedName,
        string? sourceObjectId = null,
        string? fieldPath = null);

    MicroflowResolvedEnumerationValue ResolveEnumerationValue(
        MicroflowMetadataResolutionContext context,
        string enumerationQualifiedName,
        string value,
        string? sourceObjectId = null,
        string? fieldPath = null);

    MicroflowResolvedMicroflowRef ResolveMicroflowRef(
        MicroflowMetadataResolutionContext context,
        string? id,
        string? qualifiedName,
        string? sourceObjectId = null,
        string? fieldPath = null);

    MicroflowResolvedDataType ResolveDataType(
        MicroflowMetadataResolutionContext context,
        JsonElement dataTypeJson,
        string? sourceObjectId = null,
        string? fieldPath = null);

    MicroflowResolvedMemberPath ResolveMemberPath(
        MicroflowMetadataResolutionContext context,
        MicroflowResolvedDataType rootType,
        IReadOnlyList<string> memberPath,
        string? sourceObjectId = null,
        string? fieldPath = null);

    bool IsEntitySpecializationOf(
        MicroflowMetadataResolutionContext context,
        string childEntityQualifiedName,
        string parentEntityQualifiedName);

    MicroflowMetadataResolutionReport ResolvePlanMetadataRefs(MicroflowMetadataResolutionContext context);
}
