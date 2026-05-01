using System.Reflection;
using Atlas.AppHost.Microflows.Controllers;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class DomainModelPermissionTests
{
    [Fact]
    public void MendixDomainModelController_Inherits_Authorize_From_Base()
    {
        var authorize = typeof(MicroflowApiControllerBase).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
    }

    [Theory]
    [InlineData(nameof(MendixDomainModelController.GetDocument), PermissionPolicies.LowcodeAppView)]
    [InlineData(nameof(MendixDomainModelController.SaveDocument), PermissionPolicies.LowcodeAppUpdate)]
    [InlineData(nameof(MendixDomainModelController.UpdateBindings), PermissionPolicies.LowcodeAppUpdate)]
    [InlineData(nameof(MendixDomainModelController.ImportTables), PermissionPolicies.LowcodeAppUpdate)]
    [InlineData(nameof(MendixDomainModelController.PreviewSync), PermissionPolicies.LowcodeAppView)]
    [InlineData(nameof(MendixDomainModelController.SyncDraft), PermissionPolicies.LowcodeAppUpdate)]
    [InlineData(nameof(MendixDomainModelController.RefreshMetadata), PermissionPolicies.LowcodeAppView)]
    public void DomainModel_Actions_Declare_Expected_Policy(string methodName, string expectedPolicy)
    {
        var method = typeof(MendixDomainModelController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize!.Policy);
    }

    [Fact]
    public void DomainModel_Actions_Do_Not_Allow_Anonymous()
    {
        var methods = typeof(MendixDomainModelController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }
}
