using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiDatabaseDriverDto(
    string DriverCode,
    string DisplayName,
    bool SupportsProvisioning,
    IReadOnlyList<AiDatabaseProvisionMode> ProvisionModes);

public sealed record AiDatabaseHostProfileDto(
    string Id,
    string Name,
    string DriverCode,
    AiDatabaseProvisionMode ProvisionMode,
    string? Host,
    int? Port,
    string? AdminDatabase,
    string? Username,
    string? DefaultCharset,
    string? DefaultCollation,
    string? DefaultSchema,
    string? SqliteRootPath,
    int? MaxDatabaseCount,
    bool IsDefault,
    bool IsEnabled,
    AiDatabaseConnectionTestStatus TestStatus,
    DateTime? LastTestAt,
    string? LastTestMessage,
    string MaskedConnectionSummary,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

public sealed record AiDatabaseHostProfileCreateRequest(
    string Name,
    string DriverCode,
    AiDatabaseProvisionMode ProvisionMode,
    string? Host,
    int? Port,
    string? AdminDatabase,
    string? Username,
    string? Password,
    string? AdminConnection,
    string? DefaultCharset,
    string? DefaultCollation,
    string? DefaultSchema,
    string? SqliteRootPath,
    int? MaxDatabaseCount,
    bool IsDefault = false,
    bool IsEnabled = true);

public sealed record AiDatabaseHostProfileUpdateRequest(
    string Name,
    string DriverCode,
    AiDatabaseProvisionMode ProvisionMode,
    string? Host,
    int? Port,
    string? AdminDatabase,
    string? Username,
    string? Password,
    string? AdminConnection,
    string? DefaultCharset,
    string? DefaultCollation,
    string? DefaultSchema,
    string? SqliteRootPath,
    int? MaxDatabaseCount,
    bool IsDefault = false,
    bool IsEnabled = true);

public sealed record AiDatabaseConnectionTestResult(
    bool Success,
    string Message,
    DateTime TestedAt,
    string? TraceId = null);

public sealed record AiDatabasePhysicalInstanceDto(
    string Id,
    string AiDatabaseId,
    AiDatabaseRecordEnvironment Environment,
    string DriverCode,
    string HostProfileId,
    string? HostProfileName,
    string? PhysicalDatabaseName,
    string? PhysicalSchemaName,
    string? StoragePath,
    AiDatabaseProvisionState ProvisionState,
    string? ProvisionError,
    string? DriverVersion,
    string? Charset,
    string? Collation,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastConnectedAt,
    string? LastConnectionTestMessage,
    string MaskedConnectionSummary);

public sealed record DatabaseCenterSourceDto(
    string Id,
    string SourceKind,
    string Name,
    string DriverCode,
    string? DriverIcon,
    string? Address,
    string Status,
    AiDatabaseRecordEnvironment Environment,
    bool ReadOnly,
    string? HostProfileId,
    string? HostProfileName,
    string? DatabaseName,
    string? SchemaName,
    DateTime? UpdatedAt,
    string? AiDatabaseId = null,
    string? WorkspaceId = null);

public sealed record DatabaseCenterSchemaDto(
    string Name,
    string DisplayName,
    bool IsSystem,
    bool ReadOnly,
    IReadOnlyList<DatabaseCenterSchemaGroupDto> Groups);

public sealed record DatabaseCenterSchemaGroupDto(
    string Type,
    string DisplayName,
    int Count,
    IReadOnlyList<DatabaseCenterSchemaObjectDto> Objects);

public sealed record DatabaseCenterSchemaObjectDto(
    string Name,
    string ObjectType,
    string? Schema,
    bool CanPreview,
    bool CanDrop);

public sealed record DatabaseCenterInstanceSummaryDto(
    string SourceId,
    string Name,
    AiDatabaseRecordEnvironment Environment,
    string Status,
    string DriverCode,
    string? Charset,
    string? Collation,
    string? DatabaseName,
    string? SchemaName,
    string? Creator,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastTestAt,
    string? HostProfileId,
    string? HostProfileName,
    string MaskedConnectionSummary,
    bool ReadOnly);

public sealed record DatabaseCenterConnectionLogDto(
    string Id,
    string SourceId,
    bool Success,
    string Message,
    DateTime CreatedAt);
