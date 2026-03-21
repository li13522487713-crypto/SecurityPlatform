namespace Atlas.Application.System.Models;

public sealed record SystemConfigDto(
    long Id,
    string ConfigKey,
    string ConfigValue,
    string ConfigName,
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
    string? TargetJson = null);

public sealed record SystemConfigUpdateRequest(
    string ConfigValue,
    string ConfigName,
    string? Remark,
    string? TargetJson = null);
