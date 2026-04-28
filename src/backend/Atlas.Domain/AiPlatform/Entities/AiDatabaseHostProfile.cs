using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiDatabaseHostProfile : TenantEntity
{
    public AiDatabaseHostProfile()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        DriverCode = "SQLite";
        ProvisionMode = AiDatabaseProvisionMode.SQLiteFile;
        Host = string.Empty;
        AdminDatabase = string.Empty;
        Username = string.Empty;
        EncryptedPassword = string.Empty;
        EncryptedAdminConnection = string.Empty;
        DefaultCharset = string.Empty;
        DefaultCollation = string.Empty;
        DefaultSchema = string.Empty;
        SqliteRootPath = string.Empty;
        TestStatus = AiDatabaseConnectionTestStatus.Unknown;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    public AiDatabaseHostProfile(
        TenantId tenantId,
        long id,
        string name,
        string driverCode,
        AiDatabaseProvisionMode provisionMode,
        string? createdBy)
        : base(tenantId)
    {
        SetId(id);
        Name = name.Trim();
        DriverCode = driverCode.Trim();
        ProvisionMode = provisionMode;
        Host = string.Empty;
        AdminDatabase = string.Empty;
        Username = string.Empty;
        EncryptedPassword = string.Empty;
        EncryptedAdminConnection = string.Empty;
        DefaultCharset = string.Empty;
        DefaultCollation = string.Empty;
        DefaultSchema = string.Empty;
        SqliteRootPath = string.Empty;
        TestStatus = AiDatabaseConnectionTestStatus.Unknown;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedBy = CreatedBy;
    }

    public string Name { get; private set; }

    public string DriverCode { get; private set; }

    public AiDatabaseProvisionMode ProvisionMode { get; private set; }

    public string Host { get; private set; }

    [SugarColumn(IsNullable = true)]
    public int? Port { get; private set; }

    public string AdminDatabase { get; private set; }

    public string Username { get; private set; }

    [SugarColumn(Length = 4096)]
    public string EncryptedPassword { get; private set; }

    [SugarColumn(Length = 4096)]
    public string EncryptedAdminConnection { get; private set; }

    public string DefaultCharset { get; private set; }

    public string DefaultCollation { get; private set; }

    public string DefaultSchema { get; private set; }

    public string SqliteRootPath { get; private set; }

    [SugarColumn(IsNullable = true)]
    public int? MaxDatabaseCount { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public AiDatabaseConnectionTestStatus TestStatus { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastTestAt { get; private set; }

    [SugarColumn(Length = 2048, IsNullable = true)]
    public string? LastTestMessage { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; }

    public string UpdatedBy { get; private set; }

    public void Update(
        string name,
        string driverCode,
        AiDatabaseProvisionMode provisionMode,
        string? host,
        int? port,
        string? adminDatabase,
        string? username,
        string encryptedPassword,
        string encryptedAdminConnection,
        string? defaultCharset,
        string? defaultCollation,
        string? defaultSchema,
        string? sqliteRootPath,
        int? maxDatabaseCount,
        bool isEnabled,
        string? updatedBy)
    {
        Name = name.Trim();
        DriverCode = driverCode.Trim();
        ProvisionMode = provisionMode;
        Host = host?.Trim() ?? string.Empty;
        Port = port;
        AdminDatabase = adminDatabase?.Trim() ?? string.Empty;
        Username = username?.Trim() ?? string.Empty;
        EncryptedPassword = encryptedPassword;
        EncryptedAdminConnection = encryptedAdminConnection;
        DefaultCharset = defaultCharset?.Trim() ?? string.Empty;
        DefaultCollation = defaultCollation?.Trim() ?? string.Empty;
        DefaultSchema = defaultSchema?.Trim() ?? string.Empty;
        SqliteRootPath = sqliteRootPath?.Trim() ?? string.Empty;
        MaxDatabaseCount = maxDatabaseCount;
        IsEnabled = isEnabled;
        UpdatedBy = updatedBy ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkTestResult(bool success, string? message)
    {
        TestStatus = success ? AiDatabaseConnectionTestStatus.Success : AiDatabaseConnectionTestStatus.Failed;
        LastTestAt = DateTime.UtcNow;
        LastTestMessage = message;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class AiDatabasePhysicalInstance : TenantEntity
{
    public AiDatabasePhysicalInstance()
        : base(TenantId.Empty)
    {
        Environment = AiDatabaseRecordEnvironment.Draft;
        DriverCode = "SQLite";
        PhysicalDatabaseName = string.Empty;
        PhysicalSchemaName = string.Empty;
        StoragePath = string.Empty;
        EncryptedConnection = string.Empty;
        ProvisionState = AiDatabaseProvisionState.Pending;
        Charset = string.Empty;
        Collation = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public AiDatabasePhysicalInstance(
        TenantId tenantId,
        long id,
        long aiDatabaseId,
        AiDatabaseRecordEnvironment environment,
        string driverCode,
        long hostProfileId)
        : base(tenantId)
    {
        SetId(id);
        AiDatabaseId = aiDatabaseId;
        Environment = environment;
        DriverCode = driverCode.Trim();
        HostProfileId = hostProfileId;
        PhysicalDatabaseName = string.Empty;
        PhysicalSchemaName = string.Empty;
        StoragePath = string.Empty;
        EncryptedConnection = string.Empty;
        ProvisionState = AiDatabaseProvisionState.Pending;
        Charset = string.Empty;
        Collation = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AiDatabaseId { get; private set; }

    public AiDatabaseRecordEnvironment Environment { get; private set; }

    public string DriverCode { get; private set; }

    public long HostProfileId { get; private set; }

    public string PhysicalDatabaseName { get; private set; }

    public string PhysicalSchemaName { get; private set; }

    public string StoragePath { get; private set; }

    [SugarColumn(Length = 4096)]
    public string EncryptedConnection { get; private set; }

    public AiDatabaseProvisionState ProvisionState { get; private set; }

    [SugarColumn(Length = 2048, IsNullable = true)]
    public string? ProvisionError { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? DriverVersion { get; private set; }

    public string Charset { get; private set; }

    public string Collation { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastConnectedAt { get; private set; }

    [SugarColumn(Length = 2048, IsNullable = true)]
    public string? LastConnectionTestMessage { get; private set; }

    public void Configure(
        string physicalDatabaseName,
        string? physicalSchemaName,
        string? storagePath,
        string encryptedConnection,
        string? charset,
        string? collation)
    {
        PhysicalDatabaseName = physicalDatabaseName.Trim();
        PhysicalSchemaName = physicalSchemaName?.Trim() ?? string.Empty;
        StoragePath = storagePath?.Trim() ?? string.Empty;
        EncryptedConnection = encryptedConnection;
        Charset = charset?.Trim() ?? string.Empty;
        Collation = collation?.Trim() ?? string.Empty;
        ProvisionState = AiDatabaseProvisionState.Ready;
        ProvisionError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPending()
    {
        ProvisionState = AiDatabaseProvisionState.Pending;
        ProvisionError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        ProvisionState = AiDatabaseProvisionState.Failed;
        ProvisionError = error;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkConnectionTest(bool success, string? message)
    {
        LastConnectedAt = success ? DateTime.UtcNow : LastConnectedAt;
        LastConnectionTestMessage = message;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiDatabaseProvisionMode
{
    SQLiteFile = 0,
    MySqlDatabase = 1,
    PostgreSqlSchema = 2,
    PostgreSqlDatabase = 3,
    ExistingDatabase = 4
}

public enum AiDatabaseEnvironmentMode
{
    DraftOnline = 0,
    DraftOnly = 1
}

public enum AiDatabaseConnectionTestStatus
{
    Unknown = 0,
    Success = 1,
    Failed = 2
}
