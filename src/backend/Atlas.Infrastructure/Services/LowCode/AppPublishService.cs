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

public sealed class AppPublishService : IAppPublishService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "hosted", "embedded-sdk", "preview" };

    private readonly IAppDefinitionRepository _appRepo;
    private readonly IAppPublishArtifactRepository _artifactRepo;
    private readonly IAppVersionArchiveRepository _versionRepo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public AppPublishService(IAppDefinitionRepository appRepo, IAppPublishArtifactRepository artifactRepo, IAppVersionArchiveRepository versionRepo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _appRepo = appRepo;
        _artifactRepo = artifactRepo;
        _versionRepo = versionRepo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<PublishArtifactDto> PublishAsync(TenantId tenantId, long currentUserId, long appId, PublishRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(request.Kind))
            throw new BusinessException(ErrorCodes.ValidationError, $"产物类型仅允许 hosted / embedded-sdk / preview：{request.Kind}");
        var app = await _appRepo.FindByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");

        long versionId = !string.IsNullOrWhiteSpace(request.VersionId) && long.TryParse(request.VersionId, out var vId)
            ? vId
            : (app.CurrentVersionId ?? await CreateImplicitSnapshotAsync(tenantId, currentUserId, appId, app.DraftSchemaJson, cancellationToken));

        var fingerprint = ComputeFingerprint(app.DraftSchemaJson, request.Kind, versionId);
        var artifactId = _idGen.NextId();
        var matrix = string.IsNullOrWhiteSpace(request.RendererMatrixJson) ? "{\"web\":true}" : request.RendererMatrixJson!;
        var entity = new AppPublishArtifact(tenantId, artifactId, appId, versionId, request.Kind, fingerprint, matrix, currentUserId);

        // 模拟 build 流水线：直接 ready，URL 按 kind 生成。
        entity.MarkBuilding();
        await _artifactRepo.InsertAsync(entity, cancellationToken);

        var publicUrl = request.Kind switch
        {
            "hosted" => $"https://apps.atlas.local/{app.Code}",
            "embedded-sdk" => $"https://cdn.atlas.local/sdk/{fingerprint}/atlas-lowcode.umd.js",
            "preview" => $"https://preview.atlas.local/{app.Code}?v={fingerprint}",
            _ => null
        };
        entity.MarkReady(publicUrl ?? string.Empty);
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
