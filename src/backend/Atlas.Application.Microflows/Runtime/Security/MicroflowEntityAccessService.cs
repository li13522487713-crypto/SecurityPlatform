using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Metadata;

namespace Atlas.Application.Microflows.Runtime.Security;

public sealed class MicroflowEntityAccessService : IMicroflowEntityAccessService
{
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

        if (security.IsSystemContext)
        {
            return Task.FromResult(Allow(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "System context bypassed EntityAccess stub.", MicroflowEntityAccessDecisionSource.SystemContext));
        }

        if (!microflow.Found)
        {
            return Task.FromResult(Deny(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "Microflow reference is unknown.", MicroflowEntityAccessDecisionSource.DenyUnknownEntity));
        }

        var options = _options;
        var mode = options.ResolveMode();
        if (string.Equals(mode, MicroflowEntityAccessMode.RoleBasedStub, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, MicroflowEntityAccessMode.Strict, StringComparison.OrdinalIgnoreCase))
        {
            var required = ResolveRequiredRoles(options.MicroflowRequiredRoles, microflow.QualifiedName ?? microflow.Id);
            if (required.Length == 0 && string.Equals(mode, MicroflowEntityAccessMode.Strict, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Deny(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "Strict EntityAccess stub denies unconfigured microflow permissions.", MicroflowEntityAccessDecisionSource.RoleBasedStub));
            }

            if (required.Length > 0 && !HasAnyRole(security.Roles, required))
            {
                return Task.FromResult(Deny(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "Required role is missing.", MicroflowEntityAccessDecisionSource.RoleBasedStub, required));
            }

            return Task.FromResult(Allow(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "RoleBasedStub allowed the microflow execution.", MicroflowEntityAccessDecisionSource.RoleBasedStub, required));
        }

        return Task.FromResult(Allow(security, MicroflowEntityAccessOperation.ExecuteMicroflow, null, microflow.Id, "AllowAll EntityAccess stub allowed the microflow execution.", MicroflowEntityAccessDecisionSource.AllowAll));
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

        if (security.IsSystemContext)
        {
            return Allow(security, operation, entity.QualifiedName, null, "System context bypassed EntityAccess stub.", MicroflowEntityAccessDecisionSource.SystemContext);
        }

        var options = _options;
        var mode = options.ResolveMode();
        if (!entity.Found)
        {
            return Deny(security, operation, entity.QualifiedName, null, "Entity metadata is unknown.", MicroflowEntityAccessDecisionSource.DenyUnknownEntity);
        }

        if (string.Equals(mode, MicroflowEntityAccessMode.DenyUnknownEntity, StringComparison.OrdinalIgnoreCase))
        {
            return Allow(security, operation, entity.QualifiedName, null, "DenyUnknownEntity allowed a resolved entity.", MicroflowEntityAccessDecisionSource.DenyUnknownEntity);
        }

        if (string.Equals(mode, MicroflowEntityAccessMode.RoleBasedStub, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, MicroflowEntityAccessMode.Strict, StringComparison.OrdinalIgnoreCase))
        {
            var required = ResolveRequiredRoles(options.EntityRequiredRoles, entity.QualifiedName);
            if (required.Length == 0 && string.Equals(mode, MicroflowEntityAccessMode.Strict, StringComparison.OrdinalIgnoreCase))
            {
                return Deny(security, operation, entity.QualifiedName, null, "Strict EntityAccess stub denies unconfigured entity permissions.", MicroflowEntityAccessDecisionSource.RoleBasedStub);
            }

            if (required.Length > 0 && !HasAnyRole(security.Roles, required))
            {
                return Deny(security, operation, entity.QualifiedName, null, "Required role is missing.", MicroflowEntityAccessDecisionSource.RoleBasedStub, required);
            }

            return Allow(security, operation, entity.QualifiedName, null, "RoleBasedStub allowed the entity operation.", MicroflowEntityAccessDecisionSource.RoleBasedStub, required);
        }

        return Allow(security, operation, entity.QualifiedName, null, "AllowAll EntityAccess stub allowed the entity operation.", MicroflowEntityAccessDecisionSource.AllowAll);
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
            DiagnosticCode = RuntimeErrorCode.RuntimeEntityAccessDenied,
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
