using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiDatabaseProvisionService : IAiDatabaseProvisioner
{
    private readonly AiDatabaseRepository _repository;
    private readonly AiDatabaseHostingOptions _hostingOptions;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AiDatabaseProvisionService> _logger;

    public AiDatabaseProvisionService(
        AiDatabaseRepository repository,
        IOptions<AiDatabaseHostingOptions> hostingOptions,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        IHostEnvironment hostEnvironment,
        ILogger<AiDatabaseProvisionService> logger)
    {
        _repository = repository;
        _hostingOptions = hostingOptions.Value;
        _encryptionOptions = encryptionOptions.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task EnsureProvisionedAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        if (database.StorageMode == AiDatabaseStorageMode.Standalone &&
            database.ProvisionState == AiDatabaseProvisionState.Ready &&
            !string.IsNullOrWhiteSpace(database.EncryptedDraftConnection) &&
            !string.IsNullOrWhiteSpace(database.EncryptedOnlineConnection))
        {
            return;
        }

        var driverCode = string.IsNullOrWhiteSpace(database.DriverCode) ? "SQLite" : DataSourceDriverRegistry.NormalizeDriverCode(database.DriverCode);
        if (!string.Equals(driverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Automatic provisioning for {driverCode} requires admin connection configuration and is not enabled in this build.");
        }

        var root = ResolveSqliteRoot();
        Directory.CreateDirectory(root);
        var databaseSegment = $"atlas_aidb_{database.TenantId.Value:N}_{database.Id}";
        var draftPath = Path.Combine(root, $"{databaseSegment}_draft.db");
        var onlinePath = Path.Combine(root, $"{databaseSegment}_online.db");

        TouchSqliteFile(draftPath);
        TouchSqliteFile(onlinePath);

        var draftConnection = $"Data Source={draftPath};";
        var onlineConnection = $"Data Source={onlinePath};";
        database.ConfigureStandaloneStorage(
            "SQLite",
            Protect(draftConnection),
            Protect(onlineConnection),
            databaseSegment);
        await _repository.UpdateAsync(database, cancellationToken);

        _logger.LogInformation(
            "Provisioned standalone AI database {DatabaseId} for tenant {TenantId} using SQLite files under {Root}.",
            database.Id,
            database.TenantId.Value,
            root);
    }

    public async Task DropAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        if (!string.Equals(database.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DeleteSqliteFile(Unprotect(database.EncryptedDraftConnection));
        DeleteSqliteFile(Unprotect(database.EncryptedOnlineConnection));
        await Task.CompletedTask;
    }

    private string ResolveSqliteRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_hostingOptions.SqliteRoot)
            ? "data/ai-db"
            : _hostingOptions.SqliteRoot.Trim();
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_hostEnvironment.ContentRootPath, configured);
    }

    private string Protect(string connectionString)
        => _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Encrypt(connectionString, _encryptionOptions.Key)
            : connectionString;

    private string Unprotect(string encrypted)
        => _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(encrypted, _encryptionOptions.Key)
            : encrypted;

    private static void TouchSqliteFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    }

    private static void DeleteSqliteFile(string connectionString)
    {
        var prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = connectionString[prefix.Length..].Trim().TrimEnd(';');
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
