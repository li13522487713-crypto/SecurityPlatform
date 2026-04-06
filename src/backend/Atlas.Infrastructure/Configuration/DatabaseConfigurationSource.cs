using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Configuration;

public sealed class DatabaseConfigurationSource : IConfigurationSource
{
    private readonly string _connectionString;
    private readonly string _platformTenantId;
    private readonly bool _encryptionEnabled;
    private readonly string _encryptionKey;
    private readonly string? _setupStateFilePath;

    public DatabaseConfigurationSource(
        string connectionString,
        string platformTenantId,
        bool encryptionEnabled,
        string encryptionKey,
        string? setupStateFilePath = null)
    {
        _connectionString = connectionString;
        _platformTenantId = platformTenantId;
        _encryptionEnabled = encryptionEnabled;
        _encryptionKey = encryptionKey;
        _setupStateFilePath = setupStateFilePath;
    }

    public DatabaseConfigurationProvider? Provider { get; private set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        Provider = new DatabaseConfigurationProvider(
            _connectionString,
            _platformTenantId,
            _encryptionEnabled,
            _encryptionKey,
            _setupStateFilePath);
        return Provider;
    }

    public DatabaseConfigurationProvider GetProviderOrThrow()
    {
        return Provider ?? throw new InvalidOperationException("DatabaseConfigurationProvider has not been built.");
    }
}
