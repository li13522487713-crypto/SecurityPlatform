using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 持久化数据库连接配置到 <c>appsettings.runtime.json</c>（M9/C5）。
///
/// - 与 <c>SetupController.PersistRuntimeConfigAsync</c> 共享同一格式：
///   <c>{ "Database": { "ConnectionString": "...", "DbType": "..." } }</c>
/// - 由 <see cref="OrmDataMigrationService.CutoverJobAsync"/> 在切主成功时调用；
///   AppHost 重启后会从 <c>appsettings.runtime.json</c> 自动回灌新连接串。
/// </summary>
public sealed class RuntimeConfigPersistor
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<RuntimeConfigPersistor> _logger;

    public RuntimeConfigPersistor(IHostEnvironment environment, ILogger<RuntimeConfigPersistor> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task PersistDatabaseConfigAsync(
        string connectionString,
        string dbType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(dbType);

        var runtimeConfigPath = Path.Combine(_environment.ContentRootPath, "appsettings.runtime.json");
        var json = JsonSerializer.Serialize(new
        {
            Database = new { ConnectionString = connectionString, DbType = dbType }
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(runtimeConfigPath, json, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "[SetupConsole] runtime database config persisted to {Path} (dbType={DbType})",
            runtimeConfigPath,
            dbType);
    }
}
