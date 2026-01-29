using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services;

public sealed class DatabaseBackupHostedService : BackgroundService
{
    private readonly DatabaseOptions _databaseOptions;
    private readonly DatabaseBackupOptions _backupOptions;
    private readonly ILogger<DatabaseBackupHostedService> _logger;
    private readonly TimeProvider _timeProvider;

    public DatabaseBackupHostedService(
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<DatabaseBackupOptions> backupOptions,
        ILogger<DatabaseBackupHostedService> logger,
        TimeProvider timeProvider)
    {
        _databaseOptions = databaseOptions.Value;
        _backupOptions = backupOptions.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_backupOptions.Enabled)
        {
            return;
        }

        if (!TryGetDatabaseFilePath(_databaseOptions.ConnectionString, out var dbPath))
        {
            _logger.LogWarning("无法解析SQLite数据库文件路径，已跳过备份。");
            return;
        }

        var backupDirectory = Path.GetFullPath(_backupOptions.BackupDirectory);
        Directory.CreateDirectory(backupDirectory);

        try
        {
            BackupDatabase(dbPath, backupDirectory);
            CleanupOldBackups(backupDirectory);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, _backupOptions.IntervalHours)));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                BackupDatabase(dbPath, backupDirectory);
                CleanupOldBackups(backupDirectory);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主正在停止（如停止调试/应用关闭/热重载重启）属于正常路径；无需记录为错误。
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库备份失败");
        }
    }

    private void BackupDatabase(string dbPath, string backupDirectory)
    {
        if (!File.Exists(dbPath))
        {
            _logger.LogWarning("数据库文件不存在，跳过备份: {DbPath}", dbPath);
            return;
        }

        var timestamp = _timeProvider.GetLocalNow().ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(backupDirectory, $"atlas_{timestamp}.db");
        File.Copy(dbPath, backupFile, overwrite: false);
        _logger.LogInformation("数据库备份完成: {BackupFile}", backupFile);
    }

    private void CleanupOldBackups(string backupDirectory)
    {
        var retentionDays = Math.Max(1, _backupOptions.RetentionDays);
        var cutoff = _timeProvider.GetLocalNow().AddDays(-retentionDays);

        foreach (var file in Directory.GetFiles(backupDirectory, "*.db"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoff.UtcDateTime)
            {
                try
                {
                    info.Delete();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除过期备份失败: {File}", file);
                }
            }
        }
    }

    private static bool TryGetDatabaseFilePath(string connectionString, out string dbPath)
    {
        dbPath = string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
            {
                var value = part.Split('=', 2)[1].Trim();
                dbPath = Path.GetFullPath(value);
                return true;
            }
        }

        return false;
    }
}
