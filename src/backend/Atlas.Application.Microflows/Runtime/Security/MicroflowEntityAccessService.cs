using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Metadata;

namespace Atlas.Application.Microflows.Runtime.Security;

public sealed class MicroflowEntityAccessService : IMicroflowEntityAccessService
{
    private const string DeniedCode = "RUNTIME_MICROFLOW_ACCESS_DENIED";
    private readonly MicroflowEntityAccessOptions _options;

    public MicroflowEntityAccessService(MicroflowEntityAccessOptions options)
    {
        _options = options;
    }

    public Task<MicroflowEntityAccessDecision> CanReadAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct)
        => Task.FromResult(Decide(security, entity, MicroflowEntityAccessOperation.Read));

    public Task<MicroflowEntityAccessDecision> CanCreateAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct)
        => Task.FromResult(Decide(security, entity, MicroflowEntityAccessOperation.Create));

    public Task<MicroflowEntityAccessDecision> CanUpdateAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct)
        => Task.FromResult(Decide(security, entity, MicroflowEntityAccessOperation.Update));

    public Task<MicroflowEntityAccessDecision> CanDeleteAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct)
        => Task.FromResult(Decide(security, entity, MicroflowEntityAccessOperation.Delete));

    public Task<MicroflowEntityAccessDecision> CanExecuteMicroflowAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedMicroflowRef microflow,
        CancellationToken ct)
    {
        if (!security.ApplyEntityAccess)
        {
            return Task.FromResult(Allow(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "Entity access is disabled for this runtime context.", MicroflowEntityAccessDecisionSource.Disabled));
        }

        if (TryBypassForWhitelistedSystemTask(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, out var bypassDecision))
        {
            return Task.FromResult(bypassDecision);
        }

        if (!microflow.Found)
        {
            return Task.FromResult(Deny(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "Microflow reference is unknown.", MicroflowEntityAccessDecisionSource.DenyUnknownEntity));
        }

        return Task.FromResult(EvaluateRoleAccess(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, ResolveRequiredRoles(_options.MicroflowRequiredRoles, microflow.QualifiedName ?? microflow.Id)));
    }

    private MicroflowEntityAccessDecision Decide(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        string operation)
    {
        if (!security.ApplyEntityAccess)
        {
            return Allow(security, operation, entity.QualifiedName, null, "Entity access is disabled for this runtime context.", MicroflowEntityAccessDecisionSource.Disabled);
        }

        if (TryBypassForWhitelistedSystemTask(security, operation, entity.QualifiedName, null, out var bypassDecision))
        {
            return bypassDecision;
        }

        if (string.Equals(_options.ResolveMode(), MicroflowEntityAccessMode.AllowAll, StringComparison.OrdinalIgnoreCase)
            && _options.EntityRequiredRoles.Count == 0)
        {
            return Allow(security, operation, entity.QualifiedName, null, "AllowAll mode allowed.", MicroflowEntityAccessDecisionSource.AllowAll);
        }

        if (!entity.Found)
        {
            return Deny(security, operation, entity.QualifiedName, null, "Entity metadata is unknown.", MicroflowEntityAccessDecisionSource.DenyUnknownEntity);
        }

        var required = ResolveRequiredRoles(_options.EntityRequiredRoles, $"{entity.QualifiedName}:{operation}");
        if (required.Length == 0)
        {
            required = ResolveRequiredRoles(_options.EntityRequiredRoles, entity.QualifiedName);
        }

        return EvaluateRoleAccess(security, operation, entity.QualifiedName, null, required);
    }

    private MicroflowEntityAccessDecision EvaluateRoleAccess(MicroflowRuntimeSecurityContext security, string operation, string? entityQualifiedName, string? microflowId, string[] requiredRoles)
    {
        if (!IsTenantWorkspaceAllowed(security, out var scopeReason))
        {
            return Deny(security, operation, entityQualifiedName, microflowId, scopeReason, MicroflowEntityAccessDecisionSource.ExternalProvider, requiredRoles);
        }

        if (requiredRoles.Length == 0)
        {
            return Deny(security, operation, entityQualifiedName, microflowId, "Permission mapping is missing for operation.", MicroflowEntityAccessDecisionSource.ExternalProvider);
        }

        if (!HasAnyRole(security.Roles, requiredRoles))
        {
            return Deny(security, operation, entityQualifiedName, microflowId, "Required role is missing.", MicroflowEntityAccessDecisionSource.ExternalProvider, requiredRoles);
        }

        return Allow(security, operation, entityQualifiedName, microflowId, "Role-based policy allowed.", MicroflowEntityAccessDecisionSource.ExternalProvider, requiredRoles);
    }

    private bool TryBypassForWhitelistedSystemTask(MicroflowRuntimeSecurityContext security, string operation, string? entityQualifiedName, string? microflowId, out MicroflowEntityAccessDecision decision)
    {
        if (security.IsSystemContext && HasAnyRole(security.Roles, _options.AllowedSystemBypassRoles.ToArray()))
        {
            decision = Allow(security, operation, entityQualifiedName, microflowId, "Whitelisted system task bypassed access checks.", MicroflowEntityAccessDecisionSource.SystemContext);
            return true;
        }

        decision = default!;
        return false;
    }

    private bool IsTenantWorkspaceAllowed(MicroflowRuntimeSecurityContext security, out string reason)
    {
        if (_options.AllowedTenantIds.Count > 0 && (string.IsNullOrWhiteSpace(security.TenantId) || !_options.AllowedTenantIds.Contains(security.TenantId)))
        {
            reason = "Tenant scope is not allowed.";
            return false;
        }

        if (_options.AllowedWorkspaceIds.Count > 0 && (string.IsNullOrWhiteSpace(security.WorkspaceId) || !_options.AllowedWorkspaceIds.Contains(security.WorkspaceId)))
        {
            reason = "Workspace scope is not allowed.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static MicroflowEntityAccessDecision Allow(
        MicroflowRuntimeSecurityContext security,
        string operation,
        string? entityQualifiedName,
        string? microflowId,
        string reason,
        string source,
        IReadOnlyList<string>? requiredRoles = null)
        => new()
        {
            Allowed = true,
            Operation = operation,
            EntityQualifiedName = entityQualifiedName,
            MicroflowId = microflowId,
            Reason = reason,
            RequiredRoles = requiredRoles ?? Array.Empty<string>(),
            ActualRoles = security.Roles,
            DiagnosticCode = "RUNTIME_ENTITY_ACCESS_ALLOWED",
            Severity = "info",
            Source = source
        };

    private static MicroflowEntityAccessDecision Deny(
        MicroflowRuntimeSecurityContext security,
        string operation,
        string? entityQualifiedName,
        string? microflowId,
        string reason,
        string source,
        IReadOnlyList<string>? requiredRoles = null)
        => new()
        {
            Allowed = false,
            Operation = operation,
            EntityQualifiedName = entityQualifiedName,
            MicroflowId = microflowId,
            Reason = reason,
            RequiredRoles = requiredRoles ?? Array.Empty<string>(),
            ActualRoles = security.Roles,
            DiagnosticCode = DeniedCode,
            Severity = "error",
            Source = source
        };

    private static string[] ResolveRequiredRoles(IReadOnlyDictionary<string, string[]> map, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<string>();
        }

        return map.TryGetValue(key!, out var roles) ? roles : Array.Empty<string>();
    }

    private static bool HasAnyRole(IReadOnlyList<string> actualRoles, IReadOnlyList<string> requiredRoles)
    {
        var actual = actualRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requiredRoles.Any(actual.Contains);
    }
}
