using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public interface IAiDatabaseClientFactory
{
    Task<(AiDatabase Database, SqlSugarClient Client)> CreateClientAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken);
}

public sealed class AiDatabaseClientFactory : IAiDatabaseClientFactory
{
    private readonly AiDatabaseRepository _repository;
    private readonly IAiDatabaseProvisioner _provisioner;
    private readonly DatabaseEncryptionOptions _encryptionOptions;

    public AiDatabaseClientFactory(
        AiDatabaseRepository repository,
        IAiDatabaseProvisioner provisioner,
        IOptions<DatabaseEncryptionOptions> encryptionOptions)
    {
        _repository = repository;
        _provisioner = provisioner;
        _encryptionOptions = encryptionOptions.Value;
    }

    public async Task<(AiDatabase Database, SqlSugarClient Client)> CreateClientAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var database = await _repository.FindByIdAsync(tenantId, databaseId, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);

        await _provisioner.EnsureProvisionedAsync(database, cancellationToken);

        var encrypted = environment == AiDatabaseRecordEnvironment.Online
            ? database.EncryptedOnlineConnection
            : database.EncryptedDraftConnection;

        if (string.IsNullOrWhiteSpace(encrypted))
        {
            throw new BusinessException("数据库连接尚未初始化。", ErrorCodes.ValidationError);
        }

        var connectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(encrypted, _encryptionOptions.Key)
            : encrypted;

        var client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DataSourceDriverRegistry.ResolveDbType(database.DriverCode),
            IsAutoCloseConnection = true
        });
        return (database, client);
    }
}
