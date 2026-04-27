using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/microflows/runtime/metadata")]
[AllowAnonymous]
public sealed class MicroflowRuntimeMetadataController : MicroflowApiControllerBase
{
    private readonly IMicroflowExecutionPlanLoader _executionPlanLoader;
    private readonly IMicroflowMetadataResolver _metadataResolver;
    private readonly IMicroflowEntityAccessService _entityAccessService;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowRuntimeMetadataController(
        IMicroflowExecutionPlanLoader executionPlanLoader,
        IMicroflowMetadataResolver metadataResolver,
        IMicroflowEntityAccessService entityAccessService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _executionPlanLoader = executionPlanLoader;
        _metadataResolver = metadataResolver;
        _entityAccessService = entityAccessService;
        _requestContextAccessor = requestContextAccessor;
    }

    [HttpPost("resolve")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowRuntimeMetadataResolveResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowRuntimeMetadataResolveResponse>>> Resolve(
        [FromBody] MicroflowRuntimeMetadataResolveRequest request,
        CancellationToken cancellationToken)
    {
        var security = request.SecurityContext
            ?? MicroflowRuntimeSecurityContext.FromRequestContext(_requestContextAccessor.Current);
        var plan = await BuildPlanAsync(request, cancellationToken);
        var context = await _metadataResolver.CreateContextAsync(plan, security, cancellationToken);
        var report = _metadataResolver.ResolvePlanMetadataRefs(context);

        var entities = request.Entities
            .Select(entity => _metadataResolver.ResolveEntity(context, entity))
            .ToArray();
        var attributes = request.Attributes
            .Select(attribute => _metadataResolver.ResolveAttribute(context, attribute.QualifiedName, attribute.EntityQualifiedName))
            .ToArray();
        var associations = request.Associations
            .Select(association => _metadataResolver.ResolveAssociation(context, association.QualifiedName, association.EntityQualifiedName))
            .ToArray();
        var enumerations = request.Enumerations
            .Select(enumeration => _metadataResolver.ResolveEnumeration(context, enumeration))
            .ToArray();
        var enumerationValues = request.EnumerationValues
            .Select(value => _metadataResolver.ResolveEnumerationValue(context, value.EnumerationQualifiedName, value.Value))
            .ToArray();
        var microflows = request.Microflows
            .Select(microflow => _metadataResolver.ResolveMicroflowRef(context, microflow.Id, microflow.QualifiedName))
            .ToArray();
        var dataTypes = request.DataTypes
            .Select(dataType => _metadataResolver.ResolveDataType(context, dataType))
            .ToArray();
        var memberPaths = request.MemberPaths
            .Select(path => _metadataResolver.ResolveMemberPath(
                context,
                _metadataResolver.ResolveDataType(context, path.RootType),
                path.MemberPath))
            .ToArray();

        var accessDecisions = new List<MicroflowEntityAccessDecision>();
        foreach (var entity in entities)
        {
            accessDecisions.Add(await _entityAccessService.CanReadAsync(security, entity, cancellationToken));
            accessDecisions.Add(await _entityAccessService.CanCreateAsync(security, entity, cancellationToken));
        }

        foreach (var microflow in microflows)
        {
            accessDecisions.Add(await _entityAccessService.CanExecuteMicroflowAsync(security, microflow, cancellationToken));
        }

        return MicroflowOk(new MicroflowRuntimeMetadataResolveResponse
        {
            CatalogVersion = context.CatalogVersion,
            UpdatedAt = context.UpdatedAt,
            ResolutionReport = report,
            Entities = entities,
            Attributes = attributes,
            Associations = associations,
            Enumerations = enumerations,
            EnumerationValues = enumerationValues,
            Microflows = microflows,
            DataTypes = dataTypes,
            MemberPaths = memberPaths,
            EntityAccessDecisions = accessDecisions
        });
    }

    private async Task<MicroflowExecutionPlan> BuildPlanAsync(MicroflowRuntimeMetadataResolveRequest request, CancellationToken cancellationToken)
    {
        var options = new MicroflowExecutionPlanLoadOptions
        {
            ResourceId = request.ResourceId,
            WorkspaceId = request.SecurityContext?.WorkspaceId ?? _requestContextAccessor.Current.WorkspaceId,
            TenantId = request.SecurityContext?.TenantId ?? _requestContextAccessor.Current.TenantId,
            UserId = request.SecurityContext?.UserId ?? _requestContextAccessor.Current.UserId,
            Mode = MicroflowExecutionPlanMode.ValidateOnly,
            IncludeDiagnostics = true
        };

        if (request.Schema.HasValue && request.Schema.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            return await _executionPlanLoader.LoadFromSchemaAsync(request.Schema.Value, options, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceId))
        {
            return await _executionPlanLoader.LoadCurrentAsync(request.ResourceId!, options, cancellationToken);
        }

        return new MicroflowExecutionPlan
        {
            Id = "metadata-resolve-inline",
            SchemaId = "metadata-resolve-inline",
            ResourceId = request.ResourceId,
            MetadataRefs = request.Refs.Select(reference => new MicroflowExecutionMetadataRef
            {
                Kind = reference.Kind,
                QualifiedName = reference.QualifiedName,
                Required = reference.Required
            }).ToArray(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
