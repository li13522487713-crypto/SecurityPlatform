namespace Atlas.Application.LowCode.Models;

/// <summary>
/// 应用完整 Schema 快照（设计态拉取整个 AppSchema 时使用）。
/// 不包含运行时所需的依赖资源版本快照——后者由 <see cref="AppVersionArchiveListItem"/> 与 M14 详情接口承载。
/// </summary>
public sealed record AppSchemaSnapshotDto(
    string AppId,
    string Code,
    string SchemaVersion,
    AppDefinitionDetail App,
    IReadOnlyList<PageDefinitionDetail> Pages,
    IReadOnlyList<AppVariableDto> Variables,
    IReadOnlyList<AppContentParamDto> ContentParams);
