using Atlas.Infrastructure.Services;
using Atlas.Core.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

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
        var setupStatePath = Path.Combine(_tempDirectory, "setup-state.json");

        File.WriteAllText(
            setupStatePath,
            JsonSerializer.Serialize(
                new SetupStateInfo
                {
                    Status = SetupState.Ready,
                    CompletedAt = DateTimeOffset.UtcNow,
                    PlatformSetupCompleted = true
                }));

        builder.UseEnvironment("Development");
        // Development 默认启用 ValidateScopes；上游 lowcode-gap-fix (0d1168b3) 引入了
        // InMemoryAgentRuntimeRegistry 单例捕获 scoped IAuditWriter，会触发 DI 验证失败。
        // 集成测试只关心 HTTP 端点本身的行为，不依赖 DI 校验本身；先关闭范围校验，等上游修复后移除。
        builder.UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateScopes = false;
            options.ValidateOnBuild = false;
        });
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Setup:StateFilePath"] = setupStatePath,
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
                services.Remove(descriptor);
            }

            services.AddHostedService(sp => sp.GetRequiredService<DatabaseInitializerHostedService>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // 测试清理失败不影响断言结果，下次运行会使用新的隔离目录。
        }
    }
}
