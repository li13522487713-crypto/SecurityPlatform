using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Configuration;

public sealed class DatabaseConfigurationSource : IConfigurationSource
{
    private readonly string _connectionString;
    private readonly string _platformTenantId;
    private readonly bool _encryptionEnabled;
    private readonly string _encryptionKey;

    public DatabaseConfigurationSource(
        string connectionString,
        string platformTenantId,
        bool encryptionEnabled,
        string encryptionKey)
    {
        _connectionString = connectionString;
        _platformTenantId = platformTenantId;
        _encryptionEnabled = encryptionEnabled;
        _encryptionKey = encryptionKey;
    }

    public DatabaseConfigurationProvider? Provider { get; private set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        Provider = new DatabaseConfigurationProvider(
            _connectionString,
            _platformTenantId,
            _encryptionEnabled,
            _encryptionKey);
        return Provider;
    }

    public DatabaseConfigurationProvider GetProviderOrThrow()
    {
        return Provider ?? throw new InvalidOperationException("DatabaseConfigurationProvider has not been built.");
    }
}
