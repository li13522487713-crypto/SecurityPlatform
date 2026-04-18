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

/// <summary>
/// 指定版本的 Schema 快照（M14 完整支持；用于版本回看 / 灰度回滚预览 / 离线快照对比）。
///
/// 数据来源：app_version_archive.schema_snapshot_json（不可变快照）+ resource_snapshot_json（依赖资源版本）。
/// 与 <see cref="AppSchemaSnapshotDto"/> 区别：
///  - 不再展开为 Pages/Variables/ContentParams（避免基于历史 JSON 反向重建）；
///  - 直接返回历史 SchemaJson 原文供前端 RuntimeRenderer / Studio Preview 反序列化；
///  - ResourceSnapshot 用于运行时按版本绑定历史资源。
/// </summary>
public sealed record AppVersionedSchemaSnapshotDto(
    string AppId,
    string VersionId,
    string VersionLabel,
    string SchemaJson,
    string ResourceSnapshotJson,
    DateTimeOffset CreatedAt);
