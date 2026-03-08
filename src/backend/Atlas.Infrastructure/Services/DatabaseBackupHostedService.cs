using Atlas.Infrastructure.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

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

        var databaseFiles = ResolveDatabaseFiles(dbPath);
        try
        {
            BackupDatabases(databaseFiles, backupDirectory);
            CleanupOldBackups(backupDirectory);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, _backupOptions.IntervalHours)));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                BackupDatabases(databaseFiles, backupDirectory);
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

    private void BackupDatabases(IReadOnlyList<string> databaseFiles, string backupDirectory)
    {
        var timestamp = _timeProvider.GetLocalNow().ToString("yyyyMMdd_HHmmss");
        foreach (var dbPath in databaseFiles)
        {
            if (!File.Exists(dbPath))
            {
                _logger.LogWarning("数据库文件不存在，跳过备份: {DbPath}", dbPath);
                continue;
            }

            var dbName = Path.GetFileNameWithoutExtension(dbPath);
            var backupFile = Path.Combine(backupDirectory, $"{dbName}_{timestamp}.db");
            BackupWithSqliteApi(dbPath, backupFile);
            WriteSha256File(backupFile);
            _logger.LogInformation("数据库备份完成: {BackupFile}", backupFile);
        }
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
                    var checksumPath = $"{file}.sha256";
                    if (File.Exists(checksumPath))
                    {
                        File.Delete(checksumPath);
                    }
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

    private static IReadOnlyList<string> ResolveDatabaseFiles(string atlasDbPath)
    {
        var fileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            atlasDbPath
        };

        var currentDirectory = Directory.GetCurrentDirectory();
        var hangfireInCurrent = Path.GetFullPath(Path.Combine(currentDirectory, "hangfire.db"));
        fileSet.Add(hangfireInCurrent);

        var atlasDirectory = Path.GetDirectoryName(atlasDbPath);
        if (!string.IsNullOrWhiteSpace(atlasDirectory))
        {
            var hangfireInAtlasDir = Path.GetFullPath(Path.Combine(atlasDirectory, "hangfire.db"));
            fileSet.Add(hangfireInAtlasDir);
        }

        return fileSet.ToArray();
    }

    private static void BackupWithSqliteApi(string sourcePath, string backupPath)
    {
        var sourceConnectionString = $"Data Source={sourcePath};Mode=ReadOnly";
        var backupConnectionString = $"Data Source={backupPath}";
        using var source = new SqliteConnection(sourceConnectionString);
        using var backup = new SqliteConnection(backupConnectionString);
        source.Open();
        backup.Open();
        source.BackupDatabase(backup);
    }

    private static void WriteSha256File(string backupFile)
    {
        using var stream = File.OpenRead(backupFile);
        var hashBytes = SHA256.HashData(stream);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var fileName = Path.GetFileName(backupFile);
        File.WriteAllText($"{backupFile}.sha256", $"{hash}  {fileName}{Environment.NewLine}");
    }
}
