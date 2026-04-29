using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowEntityAccessServiceTests
{
    [Fact]
    public async Task CanReadAsync_Allows_WhenRoleMatchesOperationPolicy()
    {
        var sut = CreateService(options => options.EntityRequiredRoles["Sales.Order:read"] = ["OrderReader"]);
        var security = CreateSecurity(roles: ["OrderReader"], tenantId: "t1", workspaceId: "w1");
        var entity = new MicroflowResolvedEntity { Found = true, QualifiedName = "Sales.Order" };

        var decision = await sut.CanReadAsync(security, entity, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal(MicroflowEntityAccessDecisionSource.ExternalProvider, decision.Source);
    }

    [Fact]
    public async Task CanReadAsync_Denies_WhenRoleMissing()
    {
        var sut = CreateService(options => options.EntityRequiredRoles["Sales.Order:read"] = ["OrderReader"]);
        var security = CreateSecurity(roles: ["OrderWriter"], tenantId: "t1", workspaceId: "w1");
        var entity = new MicroflowResolvedEntity { Found = true, QualifiedName = "Sales.Order" };

        var decision = await sut.CanReadAsync(security, entity, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("RUNTIME_MICROFLOW_ACCESS_DENIED", decision.DiagnosticCode);
        Assert.Equal(["OrderReader"], decision.RequiredRoles);
        Assert.Equal(["OrderWriter"], decision.ActualRoles);
    }

    [Fact]
    public async Task CanUpdateAsync_Denies_WhenTenantOrWorkspaceOutOfScope()
    {
        var sut = CreateService(options =>
        {
            options.EntityRequiredRoles["Sales.Order:update"] = ["OrderMaintainer"];
            options.AllowedTenantIds.Add("tenant-allow");
            options.AllowedWorkspaceIds.Add("workspace-allow");
        });

        var security = CreateSecurity(roles: ["OrderMaintainer"], tenantId: "tenant-block", workspaceId: "workspace-block");
        var entity = new MicroflowResolvedEntity { Found = true, QualifiedName = "Sales.Order" };

        var decision = await sut.CanUpdateAsync(security, entity, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal(MicroflowEntityAccessOperation.Update, decision.Operation);
        Assert.Equal(MicroflowEntityAccessDecisionSource.ExternalProvider, decision.Source);
    }

    [Fact]
    public async Task CanExecuteMicroflowAsync_SystemContextBypass_OnlyForWhitelistedRole()
    {
        var sut = CreateService(options =>
        {
            options.MicroflowRequiredRoles["mf.approval.publish"] = ["MicroflowExecutor"];
            options.AllowedSystemBypassRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SystemTaskExecutor" };
        });

        var microflow = new MicroflowResolvedMicroflowRef { Found = true, Id = "mf-1", QualifiedName = "mf.approval.publish" };

        var bypassSecurity = CreateSecurity(isSystemContext: true, roles: ["SystemTaskExecutor"]);
        var blockedSecurity = CreateSecurity(isSystemContext: true, roles: ["System"]);

        var bypassDecision = await sut.CanExecuteMicroflowAsync(bypassSecurity, microflow, CancellationToken.None);
        var blockedDecision = await sut.CanExecuteMicroflowAsync(blockedSecurity, microflow, CancellationToken.None);

        Assert.True(bypassDecision.Allowed);
        Assert.Equal(MicroflowEntityAccessDecisionSource.SystemContext, bypassDecision.Source);

        Assert.False(blockedDecision.Allowed);
        Assert.Equal("RUNTIME_MICROFLOW_ACCESS_DENIED", blockedDecision.DiagnosticCode);
    }

    private static MicroflowEntityAccessService CreateService(Action<MicroflowEntityAccessOptions>? configure = null)
    {
        var options = new MicroflowEntityAccessOptions();
        configure?.Invoke(options);
        return new MicroflowEntityAccessService(options);
    }

    private static MicroflowRuntimeSecurityContext CreateSecurity(
        bool isSystemContext = false,
        IReadOnlyList<string>? roles = null,
        string? tenantId = null,
        string? workspaceId = null)
        => new()
        {
            ApplyEntityAccess = true,
            IsSystemContext = isSystemContext,
            Roles = roles ?? Array.Empty<string>(),
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            UserId = "u1"
        };
}
