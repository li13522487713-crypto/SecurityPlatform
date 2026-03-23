namespace Atlas.Application.System.Models;

public sealed record SystemConfigDto(
    long Id,
    string ConfigKey,
    string ConfigValue,
    string ConfigName,
    string? AppId,
    string? GroupName,
    bool IsEncrypted,
    int Version,
    bool IsBuiltIn,
    string ConfigType,
    string? TargetJson,
    string? Remark);

public sealed record SystemConfigCreateRequest(
    string ConfigKey,
    string ConfigValue,
    string ConfigName,
    string? Remark,
    string ConfigType = "Text",
    string? TargetJson = null,
    string? AppId = null,
    string? GroupName = null,
    bool IsEncrypted = false);

public sealed record SystemConfigUpdateRequest(
    string ConfigValue,
    string ConfigName,
    string? Remark,
    string? TargetJson = null,
    string? GroupName = null,
    bool? IsEncrypted = null,
    int? Version = null);

public sealed record SystemConfigBatchUpsertRequest(
    IReadOnlyList<SystemConfigBatchUpsertItem> Items,
    string? AppId = null,
    string? GroupName = null);

public sealed record SystemConfigBatchUpsertItem(
    string ConfigKey,
    string ConfigValue,
    string ConfigName,
    string? Remark = null,
    string ConfigType = "Text",
    string? TargetJson = null,
    string? AppId = null,
    string? GroupName = null,
    bool? IsEncrypted = null,
    int? Version = null);
