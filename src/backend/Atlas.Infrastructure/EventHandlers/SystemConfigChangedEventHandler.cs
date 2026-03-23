using Atlas.Application.System.Events;
using Atlas.Core.Events;
using Atlas.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.EventHandlers;

public sealed class SystemConfigChangedEventHandler : IDomainEventHandler<SystemConfigChangedEvent>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SystemConfigChangedEventHandler> _logger;

    public SystemConfigChangedEventHandler(
        IServiceProvider serviceProvider,
        ILogger<SystemConfigChangedEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task HandleAsync(SystemConfigChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        var provider = _serviceProvider.GetService<DatabaseConfigurationProvider>();
        if (provider is null)
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(domainEvent.AppId))
        {
            // 应用级配置不进入全局 IConfiguration。
            return Task.CompletedTask;
        }

        provider.ReloadFromDatabase();
        _logger.LogInformation(
            "SystemConfig changed and reloaded: key={ConfigKey}, tenant={TenantId}, old={OldValue}, new={NewValue}",
            domainEvent.ConfigKey,
            domainEvent.TenantId.Value,
            domainEvent.OldValue,
            domainEvent.NewValue);

        return Task.CompletedTask;
    }
}
