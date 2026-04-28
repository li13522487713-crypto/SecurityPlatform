using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Security;

public static class MicroflowEntityAccessMode
{
    public const string AllowAll = "AllowAll";
    public const string RoleBasedStub = "RoleBasedStub";
    public const string DenyUnknownEntity = "DenyUnknownEntity";
    public const string Strict = "Strict";
}

public static class MicroflowEntityAccessOperation
{
    public const string Read = "read";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string ExecuteMicroflow = "executeMicroflow";
}

public static class MicroflowEntityAccessDecisionSource
{
    public const string AllowAll = "allowAll";
    public const string RoleBasedStub = "roleBasedStub";
    public const string DenyUnknownEntity = "denyUnknownEntity";
    public const string ExternalProvider = "externalProvider";
    public const string SystemContext = "systemContext";
    public const string Disabled = "disabled";
    public const string Unknown = "unknown";
}

public sealed record MicroflowRuntimeSecurityContext
{
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    [JsonPropertyName("userName")]
    public string? UserName { get; init; }

    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; init; }

    [JsonPropertyName("locale")]
    public string? Locale { get; init; }

    [JsonPropertyName("applyEntityAccess")]
    public bool ApplyEntityAccess { get; init; } = true;

    [JsonPropertyName("isSystemContext")]
    public bool IsSystemContext { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");

    public static MicroflowRuntimeSecurityContext FromRequestContext(
        MicroflowRequestContext? requestContext,
        bool applyEntityAccess = true,
        bool isSystemContext = false)
    {
        if (requestContext is null)
        {
            return System(applyEntityAccess);
        }

        return new MicroflowRuntimeSecurityContext
        {
            UserId = requestContext.UserId,
            UserName = requestContext.UserName,
            Roles = requestContext.Roles ?? Array.Empty<string>(),
            WorkspaceId = requestContext.WorkspaceId,
            TenantId = requestContext.TenantId,
            Locale = requestContext.Locale,
            ApplyEntityAccess = applyEntityAccess,
            IsSystemContext = isSystemContext,
            TraceId = string.IsNullOrWhiteSpace(requestContext.TraceId) ? Guid.NewGuid().ToString("N") : requestContext.TraceId
        };
    }

    public static MicroflowRuntimeSecurityContext System(bool applyEntityAccess = false)
        => new()
        {
            UserId = "system",
            UserName = "system",
            Roles = ["System"],
            ApplyEntityAccess = applyEntityAccess,
            IsSystemContext = true
        };
}

public sealed record MicroflowEntityAccessDecision
{
    [JsonPropertyName("allowed")]
    public bool Allowed { get; init; }

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("microflowId")]
    public string? MicroflowId { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("requiredRoles")]
    public IReadOnlyList<string> RequiredRoles { get; init; } = Array.Empty<string>();

    [JsonPropertyName("actualRoles")]
    public IReadOnlyList<string> ActualRoles { get; init; } = Array.Empty<string>();

    [JsonPropertyName("diagnosticCode")]
    public string DiagnosticCode { get; init; } = RuntimeErrorCode.RuntimeEntityAccessDenied;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("source")]
    public string Source { get; init; } = MicroflowEntityAccessDecisionSource.Unknown;
}

public sealed class MicroflowEntityAccessOptions
{
    public string? EntityAccessMode { get; set; }
    public bool DenyUnknownEntity { get; set; } = true;
    public Dictionary<string, string[]> EntityRequiredRoles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string[]> MicroflowRequiredRoles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ResolveMode()
    {
        if (!string.IsNullOrWhiteSpace(EntityAccessMode))
        {
            return EntityAccessMode!;
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase)
            ? MicroflowEntityAccessMode.DenyUnknownEntity
            : MicroflowEntityAccessMode.AllowAll;
    }
}
