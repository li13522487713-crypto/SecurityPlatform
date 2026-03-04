using Atlas.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

public sealed class AtlasWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "atlas-security-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, "atlas.integration.db");

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Security:EnforceHttps"] = "false",
                ["Security:BootstrapAdmin:Enabled"] = "true",
                ["Security:BootstrapAdmin:TenantId"] = IntegrationAuthHelper.DefaultTenantId,
                ["Security:BootstrapAdmin:Username"] = IntegrationAuthHelper.DefaultUsername,
                ["Security:BootstrapAdmin:Password"] = IntegrationAuthHelper.DefaultPassword,
                ["Database:ConnectionString"] = $"Data Source={databasePath}"
            };

            configurationBuilder.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            var hostedServiceDescriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();

            foreach (var descriptor in hostedServiceDescriptors)
            {
                if (descriptor.ImplementationType == typeof(DatabaseInitializerHostedService))
                {
                    continue;
                }

                services.Remove(descriptor);
            }
        });
    }

}
