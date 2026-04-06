using System.Diagnostics;
using System.Security.Cryptography;
using Atlas.Application.Setup;
using Atlas.Infrastructure.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Setup;

public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly DatabaseOptions _databaseOptions;
    private readonly DatabaseBackupOptions _backupOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseMaintenanceService> _logger;

    public DatabaseMaintenanceService(
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<DatabaseBackupOptions> backupOptions,
        IHostEnvironment environment,
        ILogger<DatabaseMaintenanceService> logger)
    {
        _databaseOptions = databaseOptions.Value;
        _backupOptions = backupOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<DatabaseConnectionStatus> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqliteConnection(_databaseOptions.ConnectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            await cmd.ExecuteScalarAsync(cancellationToken);
            sw.Stop();
            return new DatabaseConnectionStatus(true, "OK", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "[DatabaseMaintenance] 连接测试失败");
            return new DatabaseConnectionStatus(false, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    public Task<BackupResult> BackupNowAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetDatabaseFilePath(_databaseOptions.ConnectionString, out var dbPath))
            {
                return Task.FromResult(new BackupResult(false, null, "无法解析数据库文件路径", null));
            }

            var backupDir = Path.GetFullPath(
                Path.IsPathRooted(_backupOptions.BackupDirectory)
                    ? _backupOptions.BackupDirectory
                    : Path.Combine(_environment.ContentRootPath, _backupOptions.BackupDirectory));

            Directory.CreateDirectory(backupDir);

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
            var dbName = Path.GetFileNameWithoutExtension(dbPath);
            var backupFile = Path.Combine(backupDir, $"{dbName}_{timestamp}.db");

            BackupWithSqliteApi(dbPath, backupFile);
            WriteSha256(backupFile);

            var fi = new FileInfo(backupFile);
            _logger.LogInformation("[DatabaseMaintenance] 手动备份完成: {File}", backupFile);
            return Task.FromResult(new BackupResult(true, fi.Name, "OK", fi.Length));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DatabaseMaintenance] 手动备份失败");
            return Task.FromResult(new BackupResult(false, null, ex.Message, null));
        }
    }

    public Task RestoreFromBackupAsync(string backupFileName, CancellationToken cancellationToken)
    {
        if (!TryGetDatabaseFilePath(_databaseOptions.ConnectionString, out var dbPath))
        {
            throw new InvalidOperationException("无法解析数据库文件路径");
        }

        var backupDir = Path.GetFullPath(
            Path.IsPathRooted(_backupOptions.BackupDirectory)
                ? _backupOptions.BackupDirectory
                : Path.Combine(_environment.ContentRootPath, _backupOptions.BackupDirectory));

        var backupPath = Path.Combine(backupDir, backupFileName);
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"备份文件不存在: {backupFileName}");
        }

        // 先做完整性校验
        VerifyIntegrity(backupPath);

        // 覆盖恢复
        BackupWithSqliteApi(backupPath, dbPath);

        _logger.LogWarning("[DatabaseMaintenance] 数据库已从备份恢复: {File}", backupFileName);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken cancellationToken)
    {
        var backupDir = Path.GetFullPath(
            Path.IsPathRooted(_backupOptions.BackupDirectory)
                ? _backupOptions.BackupDirectory
                : Path.Combine(_environment.ContentRootPath, _backupOptions.BackupDirectory));

        if (!Directory.Exists(backupDir))
        {
            return Task.FromResult<IReadOnlyList<BackupFileInfo>>(Array.Empty<BackupFileInfo>());
        }

        var files = Directory.GetFiles(backupDir, "*.db")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f =>
            {
                string? sha256 = null;
                var checksumPath = $"{f.FullName}.sha256";
                if (File.Exists(checksumPath))
                {
                    var content = File.ReadAllText(checksumPath).Trim();
                    sha256 = content.Split(' ')[0];
                }
                return new BackupFileInfo(f.Name, f.Length, new DateTimeOffset(f.CreationTimeUtc, TimeSpan.Zero), sha256);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<BackupFileInfo>>(files);
    }

    public Task<DatabaseInfo> GetDatabaseInfoAsync(CancellationToken cancellationToken)
    {
        long? fileSize = null;
        string? journalMode = null;
        long? pageCount = null;
        long? pageSize = null;

        if (TryGetDatabaseFilePath(_databaseOptions.ConnectionString, out var dbPath) && File.Exists(dbPath))
        {
            fileSize = new FileInfo(dbPath).Length;
        }

        try
        {
            using var conn = new SqliteConnection(_databaseOptions.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode;";
            journalMode = cmd.ExecuteScalar()?.ToString();

            cmd.CommandText = "PRAGMA page_count;";
            pageCount = Convert.ToInt64(cmd.ExecuteScalar());

            cmd.CommandText = "PRAGMA page_size;";
            pageSize = Convert.ToInt64(cmd.ExecuteScalar());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DatabaseMaintenance] 获取数据库 PRAGMA 信息失败");
        }

        return Task.FromResult(new DatabaseInfo(
            _databaseOptions.DbType ?? "SQLite",
            MaskConnectionString(_databaseOptions.ConnectionString),
            fileSize,
            journalMode,
            pageCount,
            pageSize));
    }

    private static void VerifyIntegrity(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var result = cmd.ExecuteScalar()?.ToString();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"备份文件完整性校验失败: {result}");
        }
    }

    private static bool TryGetDatabaseFilePath(string connectionString, out string dbPath)
    {
        dbPath = string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
            {
                dbPath = Path.GetFullPath(part.Split('=', 2)[1].Trim());
                return true;
            }
        }
        return false;
    }

    private static void BackupWithSqliteApi(string sourcePath, string targetPath)
    {
        using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly");
        using var target = new SqliteConnection($"Data Source={targetPath}");
        source.Open();
        target.Open();
        source.BackupDatabase(target);
        target.Close();
        source.Close();
        SqliteConnection.ClearPool(target);
        SqliteConnection.ClearPool(source);
    }

    private static void WriteSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath);
        File.WriteAllText($"{filePath}.sha256", $"{hash}  {fileName}{Environment.NewLine}");
    }

    private static string MaskConnectionString(string cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return cs;
        if (cs.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            return System.Text.RegularExpressions.Regex.Replace(
                cs, @"Password=[^;]*", "Password=****", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return cs;
    }
}
