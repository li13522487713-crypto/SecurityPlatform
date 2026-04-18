namespace Atlas.Application.LowCode.Models;

/// <summary>
/// 版本 diff 视图（M14 S14-1）。后端产出 schema 字面 JSON diff 行；UI 红绿对比 + 按 path 顶段分组由 lowcode-versioning-client 处理。
/// </summary>
public sealed record AppVersionDiffDto(
    string FromVersionId,
    string ToVersionId,
    string FromLabel,
    string ToLabel,
    /// <summary>JSON Patch (RFC 6902) 风格 ops；低代码 schema diff 只产出 add/remove/replace 三类
    /// （不需要 move/copy/test，因为 schema 节点 id 稳定，比较以"路径"为主键即可）。</summary>
    IReadOnlyList<AppVersionDiffOp> Ops);

public sealed record AppVersionDiffOp(string Op, string Path, string? Before, string? After);

public sealed record AppVersionRollbackRequest(string? Note);

public sealed record AppFaqEntryDto(
    string Id,
    string Title,
    string Body,
    string? Tags,
    int Hits,
    DateTimeOffset UpdatedAt);

public sealed record AppFaqUpsertRequest(string? Id, string Title, string Body, string? Tags);

public sealed record AppResourceReferenceDto(
    string Id,
    string AppId,
    string? PageId,
    string? ComponentId,
    string ResourceType,
    string ResourceId,
    string ReferencePath,
    string? ResourceVersion,
    DateTimeOffset CreatedAt);
