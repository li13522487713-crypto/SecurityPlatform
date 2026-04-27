using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Objects;

public sealed class MicroflowRuntimeObjectMetadataService : IMicroflowRuntimeObjectMetadataService
{
    private readonly IMicroflowMetadataResolver _metadataResolver;
    private readonly IMicroflowEntityAccessService _entityAccessService;

    public MicroflowRuntimeObjectMetadataService(
        IMicroflowMetadataResolver metadataResolver,
        IMicroflowEntityAccessService entityAccessService)
    {
        _metadataResolver = metadataResolver;
        _entityAccessService = entityAccessService;
    }

    public Task<MicroflowObjectOperationPlan> BuildRetrievePlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct)
        => BuildAsync(request, MicroflowObjectOperationKind.Retrieve, MicroflowEntityAccessOperation.Read, ct);

    public Task<MicroflowObjectOperationPlan> BuildCreatePlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct)
        => BuildAsync(request, MicroflowObjectOperationKind.Create, MicroflowEntityAccessOperation.Create, ct);

    public Task<MicroflowObjectOperationPlan> BuildChangeMembersPlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct)
        => BuildAsync(request, MicroflowObjectOperationKind.ChangeMembers, MicroflowEntityAccessOperation.Update, ct);

    public Task<MicroflowObjectOperationPlan> BuildCommitPlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct)
        => BuildAsync(request, MicroflowObjectOperationKind.Commit, MicroflowEntityAccessOperation.Update, ct);

    public Task<MicroflowObjectOperationPlan> BuildDeletePlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct)
        => BuildAsync(request, MicroflowObjectOperationKind.Delete, MicroflowEntityAccessOperation.Delete, ct);

    private async Task<MicroflowObjectOperationPlan> BuildAsync(
        MicroflowObjectOperationRequest request,
        string operation,
        string accessOperation,
        CancellationToken ct)
    {
        var entity = _metadataResolver.ResolveEntity(request.MetadataContext, request.EntityQualifiedName, request.SourceObjectId, request.FieldPath);
        var attributes = new List<MicroflowResolvedAttribute>();
        var associations = new List<MicroflowResolvedAssociation>();
        foreach (var member in request.Members)
        {
            if (member.IsAssociation)
            {
                associations.Add(_metadataResolver.ResolveAssociation(request.MetadataContext, member.QualifiedName, entity.QualifiedName, request.SourceObjectId, request.FieldPath));
            }
            else
            {
                attributes.Add(_metadataResolver.ResolveAttribute(request.MetadataContext, member.QualifiedName, entity.QualifiedName, request.SourceObjectId, request.FieldPath));
            }
        }

        var access = accessOperation switch
        {
            MicroflowEntityAccessOperation.Create => await _entityAccessService.CanCreateAsync(request.MetadataContext.SecurityContext, entity, ct),
            MicroflowEntityAccessOperation.Update => await _entityAccessService.CanUpdateAsync(request.MetadataContext.SecurityContext, entity, ct),
            MicroflowEntityAccessOperation.Delete => await _entityAccessService.CanDeleteAsync(request.MetadataContext.SecurityContext, entity, ct),
            _ => await _entityAccessService.CanReadAsync(request.MetadataContext.SecurityContext, entity, ct)
        };

        return new MicroflowObjectOperationPlan
        {
            Operation = operation,
            Entity = entity,
            Attributes = attributes,
            Associations = associations,
            AccessDecision = access,
            Diagnostics = entity.Diagnostics
                .Concat(attributes.SelectMany(attribute => attribute.Diagnostics))
                .Concat(associations.SelectMany(association => association.Diagnostics))
                .ToArray()
        };
    }
}
