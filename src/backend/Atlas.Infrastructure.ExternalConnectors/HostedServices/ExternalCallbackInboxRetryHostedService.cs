using Atlas.Application.ExternalConnectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.HostedServices;

/// <summary>
/// External 连接器回调死信重试后台：
/// 周期扫 ExternalCallbackEvent.Status = Failed AND NextRetryAt &lt;= now，重新喂给 ConnectorCallbackInboxService.ApplyEventAsync。
/// 与 ApprovalExternalCallbackRetryHostedService（出站回调）形成对偶，覆盖入站事件死信场景。
/// </summary>
public sealed class ExternalCallbackInboxRetryHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExternalCallbackInboxRetryHostedService> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 50;

    public ExternalCallbackInboxRetryHostedService(
        IServiceProvider serviceProvider,
        ILogger<ExternalCallbackInboxRetryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExternalCallbackInboxRetryHostedService started; scan interval={Interval}.", _scanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RetryOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ExternalCallbackInboxRetryHostedService scan failed.");
            }

            try
            {
                await Task.Delay(_scanInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ExternalCallbackInboxRetryHostedService stopped.");
    }

    private async Task RetryOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var inboxService = scope.ServiceProvider.GetService<IConnectorCallbackInboxService>();
        if (inboxService is null)
        {
            return;
        }

        var processed = await inboxService.ProcessPendingRetriesAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        if (processed > 0)
        {
            _logger.LogInformation("ExternalCallbackInbox retry processed {Processed} events.", processed);
        }
    }
}
