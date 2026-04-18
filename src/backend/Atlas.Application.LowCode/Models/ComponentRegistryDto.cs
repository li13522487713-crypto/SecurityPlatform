namespace Atlas.Application.LowCode.Models;

/// <summary>
/// 组件注册表 DTO（M06 S06-1）。
///
/// 由 <see cref="ComponentRegistryDto"/> 聚合：
///  - 静态 manifest（前端构建时输出）。
///  - 租户级覆盖项（自定义组件 / 隐藏组件 / 默认 props 覆盖）。
/// </summary>
public sealed record ComponentRegistryDto(
    IReadOnlyList<ComponentMetaDto> Components,
    IReadOnlyList<ComponentTenantOverrideDto> Overrides);

public sealed record ComponentMetaDto(
    string Type,
    string DisplayName,
    string Category,
    string? Group,
    string Version,
    IReadOnlyList<string> RuntimeRenderer,
    IReadOnlyList<string> BindableProps,
    IReadOnlyList<string>? ContentParams,
    IReadOnlyList<string> SupportedEvents,
    ChildPolicyDto ChildPolicy);

public sealed record ChildPolicyDto(string Arity, IReadOnlyList<string>? AllowTypes);

public sealed record ComponentTenantOverrideDto(
    string Type,
    bool Hidden,
    /// <summary>覆盖默认 props 的 JSON。</summary>
    string? DefaultPropsJson);

public sealed record ComponentOverrideUpsertRequest(
    string Type,
    bool Hidden,
    string? DefaultPropsJson);
