using Atlas.Core.Tenancy;
using Atlas.WorkflowCore;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Services.DefaultProviders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.WorkflowCore.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWorkflowCore_should_register_default_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddWorkflowCore();

        using var provider = services.BuildServiceProvider();
        var persistence = provider.GetRequiredService<IPersistenceProvider>();
        persistence.Should().BeOfType<InMemoryPersistenceProvider>();

        var tenantProvider = provider.GetRequiredService<ITenantProvider>();
        tenantProvider.Should().BeOfType<DefaultTenantProvider>();

        var options = provider.GetRequiredService<WorkflowOptions>();
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(1));
        options.EnablePolling.Should().BeTrue();
        options.EnableWorkflows.Should().BeTrue();
        options.EnableEvents.Should().BeTrue();

        var backgroundTasks = provider.GetServices<IBackgroundTask>().ToList();
        backgroundTasks.Count.Should().Be(4);

        provider.GetRequiredService<IWorkflowHost>().Should().NotBeNull();
        provider.GetRequiredService<IWorkflowRegistry>().Should().NotBeNull();
        provider.GetRequiredService<IWorkflowController>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowCore_should_apply_custom_options()
    {
        var services = new ServiceCollection();

        services.AddWorkflowCore(builder =>
            builder.ConfigureOptions(opts => opts.PollInterval = TimeSpan.FromSeconds(5)));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<WorkflowOptions>();
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddWorkflowCore_should_use_custom_tenant_provider()
    {
        var services = new ServiceCollection();

        services.AddWorkflowCore(builder => builder.UseTenantProvider<TestTenantProvider>());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ITenantProvider>().Should().BeOfType<TestTenantProvider>();
    }

    [Fact]
    public void AddWorkflowCore_should_use_custom_persistence_provider()
    {
        var services = new ServiceCollection();

        services.AddWorkflowCore(builder => builder.UsePersistenceProvider<InMemoryPersistenceProvider>());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPersistenceProvider>().Should().BeOfType<InMemoryPersistenceProvider>();
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public TenantId GetTenantId() => TenantId.Empty;
    }
}
