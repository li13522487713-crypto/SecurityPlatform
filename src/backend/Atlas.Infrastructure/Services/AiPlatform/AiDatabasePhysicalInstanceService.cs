using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiDatabasePhysicalInstanceService : IAiDatabasePhysicalInstanceService
{
    private readonly AiDatabasePhysicalInstanceRepository _repository;
    private readonly AiDatabaseHostProfileRepository _profileRepository;
    private readonly IAiDatabaseSecretProtector _secretProtector;
    private readonly ILogger<AiDatabasePhysicalInstanceService> _logger;

    public AiDatabasePhysicalInstanceService(
        AiDatabasePhysicalInstanceRepository repository,
        AiDatabaseHostProfileRepository profileRepository,
        IAiDatabaseSecretProtector secretProtector,
        ILogger<AiDatabasePhysicalInstanceService> logger)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public async Task<AiDatabasePhysicalInstanceDto?> GetDraftInstanceAsync(TenantId tenantId, string databaseId, CancellationToken cancellationToken)
        => await GetByDatabaseAsync(tenantId, databaseId, AiDatabaseRecordEnvironment.Draft, cancellationToken);

    public async Task<AiDatabasePhysicalInstanceDto?> GetOnlineInstanceAsync(TenantId tenantId, string databaseId, CancellationToken cancellationToken)
        => await GetByDatabaseAsync(tenantId, databaseId, AiDatabaseRecordEnvironment.Online, cancellationToken);

    public async Task<AiDatabasePhysicalInstanceDto?> GetInstanceSummaryAsync(TenantId tenantId, string instanceId, CancellationToken cancellationToken)
    {
        var id = ParseId(instanceId, nameof(instanceId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : await MapAsync(entity, cancellationToken);
    }

    public async Task<(AiDatabasePhysicalInstance Instance, string ConnectionString)> GetConnectionAsync(
        TenantId tenantId,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var id = ParseId(instanceId, nameof(instanceId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("物理实例不存在。", ErrorCodes.NotFound);
        if (entity.ProvisionState != AiDatabaseProvisionState.Ready)
        {
            throw new BusinessException("物理实例尚未就绪。", ErrorCodes.ValidationError);
        }

        return (entity, _secretProtector.Decrypt(entity.EncryptedConnection));
    }

    public async Task UpdateProvisionStateAsync(
        TenantId tenantId,
        string instanceId,
        AiDatabaseProvisionState state,
        string? message,
        CancellationToken cancellationToken)
    {
        var id = ParseId(instanceId, nameof(instanceId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("物理实例不存在。", ErrorCodes.NotFound);
        if (state == AiDatabaseProvisionState.Failed)
        {
            entity.MarkFailed(message ?? "Provision failed.");
        }
        else if (state == AiDatabaseProvisionState.Pending)
        {
            entity.MarkPending();
        }

        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<AiDatabaseConnectionTestResult> TestConnectionAsync(TenantId tenantId, string instanceId, CancellationToken cancellationToken)
    {
        var id = ParseId(instanceId, nameof(instanceId));
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("物理实例不存在。", ErrorCodes.NotFound);
        var connectionString = _secretProtector.Decrypt(entity.EncryptedConnection);
        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = DataSourceDriverRegistry.ResolveDbType(entity.DriverCode),
                IsAutoCloseConnection = true
            });
            await client.Ado.GetScalarAsync("SELECT 1");
            entity.MarkConnectionTest(true, "Connection succeeded.");
            await _repository.UpdateAsync(entity, cancellationToken);
            return new AiDatabaseConnectionTestResult(true, "Connection succeeded.", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            var message = _secretProtector.MaskConnectionString(ex.Message);
            _logger.LogWarning(ex, "AI database instance test failed instance={InstanceId}: {Message}", entity.Id, message);
            entity.MarkConnectionTest(false, message);
            await _repository.UpdateAsync(entity, cancellationToken);
            return new AiDatabaseConnectionTestResult(false, message, DateTime.UtcNow);
        }
    }

    private async Task<AiDatabasePhysicalInstanceDto?> GetByDatabaseAsync(
        TenantId tenantId,
        string databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var id = ParseId(databaseId, nameof(databaseId));
        var entity = await _repository.FindByDatabaseEnvironmentAsync(tenantId, id, environment, cancellationToken);
        return entity is null ? null : await MapAsync(entity, cancellationToken);
    }

    private async Task<AiDatabasePhysicalInstanceDto> MapAsync(AiDatabasePhysicalInstance entity, CancellationToken cancellationToken)
    {
        var profile = await _profileRepository.FindByIdAsync(entity.TenantId, entity.HostProfileId, cancellationToken);
        var connectionString = _secretProtector.Decrypt(entity.EncryptedConnection);
        return new AiDatabasePhysicalInstanceDto(
            entity.Id.ToString(),
            entity.AiDatabaseId.ToString(),
            entity.Environment,
            entity.DriverCode,
            entity.HostProfileId.ToString(),
            profile?.Name,
            string.IsNullOrWhiteSpace(entity.PhysicalDatabaseName) ? null : entity.PhysicalDatabaseName,
            string.IsNullOrWhiteSpace(entity.PhysicalSchemaName) ? null : entity.PhysicalSchemaName,
            string.IsNullOrWhiteSpace(entity.StoragePath) ? null : entity.StoragePath,
            entity.ProvisionState,
            entity.ProvisionError,
            entity.DriverVersion,
            entity.Charset,
            entity.Collation,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.LastConnectedAt,
            entity.LastConnectionTestMessage,
            _secretProtector.MaskConnectionString(connectionString));
    }

    private static long ParseId(string value, string name)
        => long.TryParse(value, out var id) && id > 0
            ? id
            : throw new BusinessException($"{name} 必须是有效字符串 ID。", ErrorCodes.ValidationError);
}
