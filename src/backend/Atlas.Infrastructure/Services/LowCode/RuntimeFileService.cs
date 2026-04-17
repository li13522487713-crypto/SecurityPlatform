using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 低代码运行时文件服务（M10 S10-1 / S10-2 / S10-4）。
///
/// 设计：
///  - 复用 IFileStorageService（已有秒传 / 分片 / 签名 URL / 多文件能力）
///  - prepareAsync：颁发短期 token + 预签名直传 URL（兜底为 complete 端点本身）
///  - completeAsync：把上传内容委托给 IFileStorageService.UploadAsync，并把 token 状态变为 completed
///  - mime / 大小白名单服务端二次校验
///  - 全部经 IAuditWriter 审计
/// </summary>
public sealed class RuntimeFileService : IRuntimeFileService
{
    private static readonly HashSet<string> AllowedMimePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/", "video/", "audio/", "text/", "application/pdf", "application/json",
        "application/vnd.openxmlformats-officedocument.", "application/msword", "application/vnd.ms-excel", "application/vnd.ms-powerpoint"
    };

    private const long DefaultMaxSizeBytes = 200L * 1024 * 1024;
    private static readonly TimeSpan AssetGcWindow = TimeSpan.FromDays(7);

    private readonly ILowCodeAssetUploadSessionRepository _sessionRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<RuntimeFileService> _logger;

    public RuntimeFileService(
        ILowCodeAssetUploadSessionRepository sessionRepo,
        IFileStorageService fileStorage,
        IIdGeneratorAccessor idGen,
        IAuditWriter auditWriter,
        ILogger<RuntimeFileService> logger)
    {
        _sessionRepo = sessionRepo;
        _fileStorage = fileStorage;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<RuntimeFilePrepareUploadResponse> PrepareAsync(TenantId tenantId, long currentUserId, string currentUserName, RuntimeFilePrepareUploadRequest request, CancellationToken cancellationToken)
    {
        ValidateMime(request.ContentType);
        ValidateSize(request.Size);

        // 秒传：若提供 sha256，先做 IFileStorageService.CheckInstantUploadAsync
        if (!string.IsNullOrWhiteSpace(request.Sha256))
        {
            var hit = await _fileStorage.CheckInstantUploadAsync(tenantId, request.Sha256!, request.Size, cancellationToken);
            if (hit?.Exists == true && hit.FileId.HasValue)
            {
                return new RuntimeFilePrepareUploadResponse(
                    Token: $"instant_{hit.FileId.Value}",
                    UploadUrl: string.Empty,
                    InstantHit: true,
                    FileHandle: hit.FileId.Value.ToString());
            }
        }

        var token = $"upd_{_idGen.NextId()}_{Guid.NewGuid():N}";
        var session = new LowCodeAssetUploadSession(tenantId, _idGen.NextId(), token, request.FileName, request.ContentType, request.Size, request.Sha256, currentUserId);
        await _sessionRepo.InsertAsync(session, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.file.prepare", "success", $"token:{token}:size:{request.Size}", null, null), cancellationToken);

        return new RuntimeFilePrepareUploadResponse(
            Token: token,
            UploadUrl: $"/api/runtime/files:complete-upload",
            InstantHit: false,
            FileHandle: null);
    }

    public async Task<RuntimeFileCompleteUploadResponse> CompleteAsync(TenantId tenantId, long currentUserId, string currentUserName, string token, Stream content, long contentLength, CancellationToken cancellationToken)
    {
        var session = await _sessionRepo.FindByTokenAsync(tenantId, token, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"上传会话不存在：{token}");
        if (session.Status != "pending") throw new BusinessException(ErrorCodes.Conflict, $"会话状态非法：{session.Status}");
        if (session.ExpiresAt < DateTimeOffset.UtcNow)
        {
            session.MarkExpired();
            await _sessionRepo.UpdateAsync(session, cancellationToken);
            throw new BusinessException("ASSET_SESSION_EXPIRED", "上传会话已过期，请重新 prepare-upload");
        }
        ValidateMime(session.ContentType);
        ValidateSize(contentLength);

        var upload = await _fileStorage.UploadAsync(tenantId, currentUserId, currentUserName, session.FileName, session.ContentType, content, contentLength, cancellationToken);
        var fileHandle = upload.Id.ToString();
        session.Complete(fileHandle);
        await _sessionRepo.UpdateAsync(session, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.file.complete", "success", $"file:{fileHandle}:size:{contentLength}", null, null), cancellationToken);

        var imageId = session.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? fileHandle : null;
        var url = $"/api/runtime/files/{fileHandle}";
        return new RuntimeFileCompleteUploadResponse(fileHandle, url, session.ContentType, contentLength, imageId);
    }

    public async Task CancelAsync(TenantId tenantId, long currentUserId, string token, CancellationToken cancellationToken)
    {
        var session = await _sessionRepo.FindByTokenAsync(tenantId, token, cancellationToken);
        if (session is null) return;
        if (session.Status == "pending")
        {
            session.Cancel();
            await _sessionRepo.UpdateAsync(session, cancellationToken);
        }
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.file.cancel", "success", $"token:{token}", null, null), cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, string fileHandle, CancellationToken cancellationToken)
    {
        if (!long.TryParse(fileHandle, out var fileId))
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"fileHandle 格式无效：{fileHandle}");
        }
        await _fileStorage.DeleteAsync(tenantId, fileId, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.file.delete", "success", $"file:{fileHandle}", null, null), cancellationToken);
    }

    public async Task<int> RunGarbageCollectionAsync(CancellationToken cancellationToken)
    {
        // 7 天窗口：把过期的 pending 会话标 expired；FileRecord 软删除由 IFileStorageService 维护，这里仅扫会话。
        var cutoff = DateTimeOffset.UtcNow.Subtract(AssetGcWindow);
        var expired = await _sessionRepo.ExpireOlderThanAsync(cutoff, cancellationToken);
        _logger.LogInformation("LowCode asset GC: expired {Count} pending sessions older than {Cutoff}", expired, cutoff);
        return expired;
    }

    private static void ValidateMime(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) throw new BusinessException(ErrorCodes.ValidationError, "contentType 不可为空");
        if (!AllowedMimePrefixes.Any(p => contentType.StartsWith(p, StringComparison.OrdinalIgnoreCase) || string.Equals(contentType, p, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("ASSET_MIME_DENIED", $"mime 类型未在白名单：{contentType}");
        }
    }

    private static void ValidateSize(long size)
    {
        if (size <= 0) throw new BusinessException(ErrorCodes.ValidationError, "文件大小必须 > 0");
        if (size > DefaultMaxSizeBytes) throw new BusinessException("ASSET_TOO_LARGE", $"文件超出 {DefaultMaxSizeBytes} 字节上限");
    }
}
