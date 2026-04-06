using Atlas.Core.Setup;
using Atlas.Domain.System.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 清理过期上传会话（普通分片 + Tus）以及临时文件。
/// </summary>
public sealed class FileUploadSessionCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FileUploadSessionCleanupHostedService> _logger;
    private readonly ISetupStateProvider _setupStateProvider;

    public FileUploadSessionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<FileUploadSessionCleanupHostedService> logger,
        ISetupStateProvider setupStateProvider)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
        _setupStateProvider = setupStateProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _setupStateProvider.WaitForReadyAsync(stoppingToken);

        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理文件上传会话失败");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var now = _timeProvider.GetUtcNow();

        var expiredChunkSessions = await db.Queryable<FileUploadSession>()
            .Where(x =>
                (x.Status == FileUploadSessionStatus.Pending || x.Status == FileUploadSessionStatus.Uploading)
                && x.ExpiresAt < now)
            .Select(x => new { x.Id, x.TempDirectory })
            .ToListAsync(cancellationToken);

        if (expiredChunkSessions.Count > 0)
        {
            var chunkIds = expiredChunkSessions.Select(x => x.Id).ToArray();
            await db.Updateable<FileUploadSession>()
                .SetColumns(x => x.Status == FileUploadSessionStatus.Expired)
                .SetColumns(x => x.UpdatedAt == now)
                .Where(x => SqlFunc.ContainsArray(chunkIds, x.Id))
                .ExecuteCommandAsync(cancellationToken);

            foreach (var session in expiredChunkSessions)
            {
                TryDeleteDirectory(session.TempDirectory);
            }
        }

        var expiredTusSessions = await db.Queryable<FileTusUploadSession>()
            .Where(x =>
                (x.Status == FileTusUploadSessionStatus.Pending || x.Status == FileTusUploadSessionStatus.Uploading)
                && x.ExpiresAt < now)
            .Select(x => new { x.Id, x.TempFilePath })
            .ToListAsync(cancellationToken);

        if (expiredTusSessions.Count > 0)
        {
            var tusIds = expiredTusSessions.Select(x => x.Id).ToArray();
            await db.Updateable<FileTusUploadSession>()
                .SetColumns(x => x.Status == FileTusUploadSessionStatus.Expired)
                .SetColumns(x => x.UpdatedAt == now)
                .Where(x => SqlFunc.ContainsArray(tusIds, x.Id))
                .ExecuteCommandAsync(cancellationToken);

            foreach (var session in expiredTusSessions)
            {
                TryDeleteFile(session.TempFilePath);
            }
        }
    }

    private static void TryDeleteDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}
