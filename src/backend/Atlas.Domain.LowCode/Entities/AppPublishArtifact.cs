using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 应用发布产物（M01 落地，对应 docx §10.7 PublishedArtifact）。
/// 三类产物（Hosted App / Embedded SDK / Preview Artifact）的统一持久化模型；
/// 完整发布流程与 SDK API 契约由 <c>docs/lowcode-publish-spec.md</c> 与 M17 落地。
/// </summary>
public sealed class AppPublishArtifact : TenantEntity
{
#pragma warning disable CS8618
    public AppPublishArtifact()
        : base(TenantId.Empty)
    {
        Kind = "hosted";
        Status = "pending";
        Fingerprint = string.Empty;
        RendererMatrixJson = "{}";
    }
#pragma warning restore CS8618

    public AppPublishArtifact(
        TenantId tenantId,
        long id,
        long appId,
        long versionId,
        string kind,
        string fingerprint,
        string rendererMatrixJson,
        long publishedByUserId)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        VersionId = versionId;
        Kind = kind;
        Status = "pending";
        Fingerprint = fingerprint;
        RendererMatrixJson = string.IsNullOrWhiteSpace(rendererMatrixJson) ? "{}" : rendererMatrixJson;
        PublishedByUserId = publishedByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AppId { get; private set; }

    /// <summary>关联版本归档 ID（强外键到 <see cref="AppVersionArchive"/>）。</summary>
    public long VersionId { get; private set; }

    /// <summary>产物类型：hosted / embedded-sdk / preview。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Kind { get; private set; }

    /// <summary>状态：pending / building / ready / failed / revoked。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>产物指纹（SHA256），与版本绑定（docx §10.8 安全治理 #5）。</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Fingerprint { get; private set; }

    /// <summary>CDN / 对象存储 URL（hosted = 独立 URL，embedded-sdk = JS 入口 URL，preview = 临时 URL）。</summary>
    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? PublicUrl { get; private set; }

    /// <summary>渲染器矩阵 JSON（web / mini-wx / mini-douyin / h5 是否包含）。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string RendererMatrixJson { get; private set; }

    /// <summary>错误信息（构建失败时记录）。</summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; private set; }

    public long PublishedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void MarkBuilding()
    {
        Status = "building";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkReady(string publicUrl)
    {
        Status = "ready";
        PublicUrl = publicUrl;
        ErrorMessage = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = "failed";
        ErrorMessage = error;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Revoke(string? reason)
    {
        Status = "revoked";
        ErrorMessage = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
