using System.Reflection;
using Atlas.AppHost.Microflows.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowAuthorizationTests
{
    [Fact]
    public void MicroflowApiControllerBase_RequiresAuthorize()
    {
        var authorize = typeof(MicroflowApiControllerBase).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
    }

    [Fact]
    public void MicroflowHealthEndpoints_AreExplicitlyAllowAnonymous()
    {
        var endpoints = new[]
        {
            nameof(MicroflowResourceController.GetHealth),
            nameof(MicroflowResourceController.GetRuntimeHealth),
            nameof(MicroflowResourceController.GetStorageHealth),
        };

        foreach (var endpoint in endpoints)
        {
            var method = typeof(MicroflowResourceController).GetMethod(endpoint);
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        var metadataHealth = typeof(MicroflowMetadataController).GetMethod(nameof(MicroflowMetadataController.GetHealth));
        Assert.NotNull(metadataHealth);
        Assert.NotNull(metadataHealth!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void NonHealthMicroflowControllerActions_DoNotBypassAuthorization()
    {
        var controllerTypes = typeof(MicroflowApiControllerBase).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true }
                           && type.IsSubclassOf(typeof(MicroflowApiControllerBase)))
            .ToArray();

        Assert.NotEmpty(controllerTypes);

        foreach (var controllerType in controllerTypes)
        {
            var publicActions = controllerType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .ToArray();

            foreach (var action in publicActions)
            {
                var allowAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
                if (allowAnonymous)
                {
                    Assert.Contains("Health", action.Name, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
