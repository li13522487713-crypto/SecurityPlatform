using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiDatabaseSecretProtector
{
    string Encrypt(string? secret);

    string Decrypt(string? encryptedSecret);

    string MaskConnectionString(string? connectionString);

    string MaskSecret(string? secret);
}

public interface IAiDatabaseHostProfileService
{
    Task<IReadOnlyList<AiDatabaseHostProfileDto>> ListProfilesAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<AiDatabaseHostProfileDto?> GetProfileAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken);

    Task<AiDatabaseHostProfileDto> CreateProfileAsync(
        TenantId tenantId,
        AiDatabaseHostProfileCreateRequest request,
        string? operatorId,
        CancellationToken cancellationToken);

    Task<AiDatabaseHostProfileDto> UpdateProfileAsync(
        TenantId tenantId,
        string profileId,
        AiDatabaseHostProfileUpdateRequest request,
        string? operatorId,
        CancellationToken cancellationToken);

    Task DeleteProfileAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken);

    Task<AiDatabaseConnectionTestResult> TestProfileConnectionAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken);

    Task SetDefaultProfileAsync(TenantId tenantId, string profileId, CancellationToken cancellationToken);

    Task<AiDatabaseHostProfileDto> ResolveDefaultProfileAsync(TenantId tenantId, string driverCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<AiDatabaseDriverDto>> GetAvailableDriversAsync(CancellationToken cancellationToken);
}

public interface IAiDatabaseProvisioningService
{
    Task ProvisionAsync(AiDatabase database, CancellationToken cancellationToken);

    Task ProvisionDraftAsync(AiDatabase database, CancellationToken cancellationToken);

    Task ProvisionOnlineAsync(AiDatabase database, CancellationToken cancellationToken);

    Task ReProvisionDraftAsync(AiDatabase database, CancellationToken cancellationToken);

    Task<AiDatabaseConnectionTestResult> TestInstanceConnectionAsync(
        TenantId tenantId,
        string instanceId,
        CancellationToken cancellationToken);

    Task DropInstanceAsync(TenantId tenantId, string instanceId, CancellationToken cancellationToken);
}

public interface IAiDatabasePhysicalInstanceService
{
    Task<AiDatabasePhysicalInstanceDto?> GetDraftInstanceAsync(TenantId tenantId, string databaseId, CancellationToken cancellationToken);

    Task<AiDatabasePhysicalInstanceDto?> GetOnlineInstanceAsync(TenantId tenantId, string databaseId, CancellationToken cancellationToken);

    Task<AiDatabasePhysicalInstanceDto?> GetInstanceSummaryAsync(TenantId tenantId, string instanceId, CancellationToken cancellationToken);

    Task<(AiDatabasePhysicalInstance Instance, string ConnectionString)> GetConnectionAsync(
        TenantId tenantId,
        string instanceId,
        CancellationToken cancellationToken);

    Task UpdateProvisionStateAsync(
        TenantId tenantId,
        string instanceId,
        AiDatabaseProvisionState state,
        string? message,
        CancellationToken cancellationToken);

    Task<AiDatabaseConnectionTestResult> TestConnectionAsync(TenantId tenantId, string instanceId, CancellationToken cancellationToken);
}

public interface IDatabaseManagementService
{
    Task<IReadOnlyList<DatabaseCenterSourceDto>> ListSourcesAsync(
        TenantId tenantId,
        string? keyword,
        string? workspaceId,
        string? driver,
        string? status,
        AiDatabaseRecordEnvironment? environment,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DatabaseCenterSchemaDto>> ListSchemasAsync(TenantId tenantId, string sourceId, CancellationToken cancellationToken);

    Task<DatabaseCenterInstanceSummaryDto> GetInstanceSummaryAsync(TenantId tenantId, string sourceId, CancellationToken cancellationToken);

    Task<AiDatabaseConnectionTestResult> TestSourceAsync(TenantId tenantId, string sourceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DatabaseCenterConnectionLogDto>> ListConnectionLogsAsync(
        TenantId tenantId,
        string sourceId,
        CancellationToken cancellationToken);

    Task<DatabaseCenterParameterizedSqlResult> ExecuteParameterizedAsync(
        TenantId tenantId,
        string sourceId,
        string sql,
        IReadOnlyList<DatabaseSqlParameter> parameters,
        DatabaseCenterSqlExecuteOptions options,
        CancellationToken cancellationToken);
}

public sealed record DatabaseSqlParameter(string Name, object? Value);

public sealed record DatabaseCenterSqlExecuteOptions
{
    public string? SchemaName { get; init; }
    public AiDatabaseRecordEnvironment Environment { get; init; } = AiDatabaseRecordEnvironment.Draft;
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRows { get; init; } = 1000;
    public DatabaseSqlExecuteMode Mode { get; init; } = DatabaseSqlExecuteMode.Auto;
}

public enum DatabaseSqlExecuteMode { Auto, SelectOnly, DmlOnly }

public sealed record DatabaseCenterParameterizedSqlResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = Array.Empty<IReadOnlyDictionary<string, object?>>();
    public int? AffectedRows { get; init; }
    public long ElapsedMs { get; init; }
    public bool Truncated { get; init; }
}
