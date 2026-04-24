namespace Atlas.Application.LowCode.Models;

/// <summary>应用列表项（用于列表查询返回）。</summary>
public sealed record AppDefinitionListItem(
    string Id,
    string Code,
    string DisplayName,
    string? Description,
    string SchemaVersion,
    string TargetTypes,
    string DefaultLocale,
    string Status,
    long? CurrentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? WorkspaceId = null,
    string? FolderId = null);

/// <summary>应用详情（含主题，但不含 schema 完整 JSON——schema 通过专用端点拉取以避免过大列表负载）。</summary>
public sealed record AppDefinitionDetail(
    string Id,
    string Code,
    string DisplayName,
    string? Description,
    string SchemaVersion,
    string TargetTypes,
    string DefaultLocale,
    AppThemeConfigDto? Theme,
    string Status,
    string? CurrentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? WorkspaceId = null);

/// <summary>主题配置 DTO，与领域 <c>AppThemeConfig</c> 镜像。</summary>
public sealed record AppThemeConfigDto(
    string? PrimaryColor,
    int? BorderRadius,
    string? DarkMode,
    Dictionary<string, string>? CssVariables);

/// <summary>创建应用请求。</summary>
public sealed record AppDefinitionCreateRequest(
    string Code,
    string DisplayName,
    string? Description,
    string TargetTypes,
    string? DefaultLocale,
    AppThemeConfigDto? Theme,
    string? WorkspaceId = null);

/// <summary>更新应用元数据请求（不含 schema）。</summary>
public sealed record AppDefinitionUpdateRequest(
    string DisplayName,
    string? Description,
    string TargetTypes,
    string DefaultLocale,
    AppThemeConfigDto? Theme,
    string? WorkspaceId = null);

/// <summary>替换 schema 草稿请求（完整 AppSchema JSON 字符串，由前端 zod 校验后提交）。</summary>
public sealed record AppDraftReplaceRequest(string SchemaJson, string? DraftSessionId = null);

/// <summary>草稿快照（autosave）请求 —— 与 <see cref="AppDraftReplaceRequest"/> 同结构，单独命名以便审计区分。</summary>
public sealed record AppDraftAutoSaveRequest(string SchemaJson, string? DraftSessionId = null);

/// <summary>当前草稿响应（含 schema JSON 与 ETag）。</summary>
public sealed record AppDraftResponse(
    string AppId,
    string SchemaVersion,
    string SchemaJson,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy);

/// <summary>创建版本快照请求。</summary>
public sealed record AppVersionSnapshotRequest(
    string VersionLabel,
    string? Note,
    string? ResourceSnapshotJson);

/// <summary>版本归档摘要。</summary>
public sealed record AppVersionArchiveListItem(
    string Id,
    string AppId,
    string VersionLabel,
    string? Note,
    bool IsSystemSnapshot,
    long CreatedByUserId,
    DateTimeOffset CreatedAt);
