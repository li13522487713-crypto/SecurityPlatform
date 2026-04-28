using System.Security.Cryptography;
using System.Text;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 发布产物构建流水线（P2-2 抽象）。
///
/// 默认实现 <see cref="NoopPublishBuildPipeline"/> 不做真实 build —— 仅按 kind 生成稳定 URL，
/// 与 FINAL 报告"模型/外部依赖延后项"一致；生产部署时由宿主用 <c>services.Replace(...)</c> 注入
/// MinIO + CDN 实现（如 <c>MinioPublishBuildPipeline</c>，调用 <c>IFileObjectStore</c> 上传 JS/CSS/Schema 包，
/// 然后调 CDN 刷新 API）。
/// </summary>
public interface IPublishBuildPipeline
{
    /// <summary>
    /// 构建产物：返回最终的 PublicUrl。
    /// 抛 <see cref="BusinessException"/> 视为构建失败，调用方将 <see cref="AppPublishArtifact.MarkFailed"/>。
    /// </summary>
    Task<string> BuildAsync(
        TenantId tenantId,
        AppPublishArtifact artifact,
        AppDefinition app,
        string versionedSchemaJson,
        CancellationToken cancellationToken);
}

/// <summary>
/// 默认 NoopPublishBuildPipeline：不做真实 build，仅按 kind 拼装稳定 URL。
/// 与 FINAL 报告一致：MinIO/CDN 真实接入由生产环境通过 services.Replace 注入。
/// </summary>
public sealed class NoopPublishBuildPipeline : IPublishBuildPipeline
{
    public Task<string> BuildAsync(TenantId tenantId, AppPublishArtifact artifact, AppDefinition app, string versionedSchemaJson, CancellationToken cancellationToken)
    {
        var url = artifact.Kind switch
        {
            "hosted" => $"https://apps.atlas.local/{app.Code}",
            "embedded-sdk" => $"https://cdn.atlas.local/lowcode-sdk/v1/atlas-lowcode.umd.js?app={app.Code}&fp={artifact.Fingerprint}",
            "preview" => $"https://preview.atlas.local/{app.Code}?v={artifact.Fingerprint}",
            _ => string.Empty
        };
        return Task.FromResult(url);
    }
}

public sealed class AppPublishService : IAppPublishService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "hosted", "embedded-sdk", "preview" };

    private readonly IAppDefinitionRepository _appRepo;
    private readonly IAppPublishArtifactRepository _artifactRepo;
    private readonly IAppVersionArchiveRepository _versionRepo;
    private readonly IRuntimeWebviewDomainService _webview;
    private readonly IPublishBuildPipeline _buildPipeline;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public AppPublishService(
        IAppDefinitionRepository appRepo,
        IAppPublishArtifactRepository artifactRepo,
        IAppVersionArchiveRepository versionRepo,
        IRuntimeWebviewDomainService webview,
        IPublishBuildPipeline buildPipeline,
        IIdGeneratorAccessor idGen,
        IAuditWriter auditWriter)
    {
        _appRepo = appRepo;
        _artifactRepo = artifactRepo;
        _versionRepo = versionRepo;
        _webview = webview;
        _buildPipeline = buildPipeline;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<PublishArtifactDto> PublishAsync(TenantId tenantId, long currentUserId, long appId, PublishRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(request.Kind))
            throw new BusinessException(ErrorCodes.ValidationError, $"产物类型仅允许 hosted / embedded-sdk / preview：{request.Kind}");
        var app = await _appRepo.FindByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");

        // P2-2 修复（PLAN §M17 S17-2 + lowcode-publish-spec.md "发布 hosted 必须先验证域名"）：
        // hosted 类型必须保证当前租户至少有一个 verified webview 域名（用于 hosted app 的最终 CNAME 指向）；
        // 否则禁止发布并写审计。embedded-sdk / preview 不强制（preview 由内部域名提供）。
        if (string.Equals(request.Kind, "hosted", StringComparison.OrdinalIgnoreCase))
        {
            var domains = await _webview.ListAsync(tenantId, cancellationToken);
            var hasVerified = domains.Any(d => d.Verified);
            if (!hasVerified)
            {
                await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.publish", "failed", $"app:{appId}:kind:hosted:reason:no-verified-domain", null, null), cancellationToken);
                throw new BusinessException("WEBVIEW_DOMAIN_REQUIRED", "发布 hosted 类型前必须先在 webview-domains 完成至少一个域名归属验证。");
            }
        }

        var effectiveCurrentVersionId = app.CurrentVersionId.GetValueOrDefault() > 0
            ? app.CurrentVersionId
            : null;

        long versionId = !string.IsNullOrWhiteSpace(request.VersionId) && long.TryParse(request.VersionId, out var vId)
            ? vId
            : (effectiveCurrentVersionId ?? await CreateImplicitSnapshotAsync(tenantId, currentUserId, appId, app.DraftSchemaJson, cancellationToken));

        var fingerprint = ComputeFingerprint(app.DraftSchemaJson, request.Kind, versionId);
        var artifactId = _idGen.NextId();
        var matrix = string.IsNullOrWhiteSpace(request.RendererMatrixJson) ? "{\"web\":true}" : request.RendererMatrixJson!;
        var entity = new AppPublishArtifact(tenantId, artifactId, appId, versionId, request.Kind, fingerprint, matrix, currentUserId);

        entity.MarkBuilding();
        await _artifactRepo.InsertAsync(entity, cancellationToken);

        // P2-2：构建流水线（默认 NoopPipeline 仅返回 URL；生产用 MinioPipeline 注入）
        string publicUrl;
        try
        {
            publicUrl = await _buildPipeline.BuildAsync(tenantId, entity, app, app.DraftSchemaJson, cancellationToken);
        }
        catch (Exception ex)
        {
            entity.MarkFailed(ex.Message);
            await _artifactRepo.UpdateAsync(entity, cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.publish", "failed", $"app:{appId}:kind:{request.Kind}:artifact:{artifactId}:reason:{ex.GetType().Name}", null, null), cancellationToken);
            throw;
        }

        entity.MarkReady(publicUrl);
        await _artifactRepo.UpdateAsync(entity, cancellationToken);

        if (request.Kind == "hosted")
        {
            app.MarkPublished(versionId, currentUserId);
            await _appRepo.UpdateAsync(app, cancellationToken);
        }

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.publish", "success", $"app:{appId}:kind:{request.Kind}:artifact:{artifactId}:fp:{fingerprint}", null, null), cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PublishArtifactDto>> ListAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var list = await _artifactRepo.ListByAppAsync(tenantId, appId, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task RevokeAsync(TenantId tenantId, long currentUserId, long appId, string artifactId, string? reason, CancellationToken cancellationToken)
    {
        if (!long.TryParse(artifactId, out var id)) throw new BusinessException(ErrorCodes.ValidationError, $"artifactId 无效：{artifactId}");
        var entity = await _artifactRepo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"产物不存在：{artifactId}");
        if (entity.AppId != appId) throw new BusinessException(ErrorCodes.ValidationError, "产物与应用不匹配");
        entity.Revoke(reason);
        await _artifactRepo.UpdateAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.publish.revoke", "success", $"artifact:{artifactId}:reason:{reason}", null, null), cancellationToken);
    }

    private async Task<long> CreateImplicitSnapshotAsync(TenantId tenantId, long currentUserId, long appId, string schemaJson, CancellationToken cancellationToken)
    {
        var versionId = _idGen.NextId();
        var label = $"publish-implicit-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var archive = new AppVersionArchive(tenantId, versionId, appId, label, schemaJson, "{}", note: "发布时隐式快照", createdByUserId: currentUserId, isSystemSnapshot: true);
        await _versionRepo.InsertAsync(archive, cancellationToken);
        return versionId;
    }

    private static string ComputeFingerprint(string schemaJson, string kind, long versionId)
    {
        var bytes = Encoding.UTF8.GetBytes($"{kind}|{versionId}|{schemaJson}");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static PublishArtifactDto ToDto(AppPublishArtifact a) => new(
        a.Id.ToString(), a.AppId.ToString(), a.VersionId.ToString(), a.Kind, a.Status, a.Fingerprint, a.PublicUrl, a.RendererMatrixJson, a.ErrorMessage, a.PublishedByUserId.ToString(), a.CreatedAt, a.UpdatedAt);
}
